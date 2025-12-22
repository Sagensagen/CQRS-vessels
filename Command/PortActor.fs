module Command.PortActor

open System
open Akka.Actor
open Akkling
open Akkling.Actors
open Marten
open Domain.EventMetadata
open Domain.PortAggregate
open Shared.Api.Port
open Serilog

type PortActorState = { State: PortState option; Version: int64 }

type PortCommandResponse =
    | PortCommandSuccess of eventCount: int
    | PortCommandFailure of error: PortCommandErrors

type PortStateResponse =
    | PortExists of state: PortState
    | PortNotFound

// Typed protocol for Akkling
type PortProtocol =
    | ExecuteCommand of PortCommand
    | GetState
    | CheckExpiredReservations

let private unwrapPortEvent (event: PortEvent) : obj =
    match event with
    | PortRegistered evt -> box evt
    | VesselDockingReserved evt -> box evt
    | DockingConfirmed evt -> box evt
    | DockingReservationExpired evt -> box evt
    | VesselUndocked evt -> box evt
    | PortOpened evt -> box evt
    | PortClosed evt -> box evt

let private wrapPortEvent (data: obj) : PortEvent option =
    match data with
    | :? PortRegisteredEvt as evt -> Some(PortRegistered evt)
    | :? VesselDockingReservedEvt as evt -> Some(VesselDockingReserved evt)
    | :? DockingConfirmedEvt as evt -> Some(DockingConfirmed evt)
    | :? DockingReservationExpiredEvt as evt -> Some(DockingReservationExpired evt)
    | :? VesselUndockedEvt as evt -> Some(VesselUndocked evt)
    | :? PortOpenedEvt as evt -> Some(PortOpened evt)
    | :? PortClosedEvt as evt -> Some(PortClosed evt)
    | :? PortEvent as pe -> Some pe
    | _ -> None

/// Persist events to Marten event store
let private persistEvents
    (logger: ILogger)
    (store: IDocumentStore)
    (portId: Guid)
    (events: PortEvent list)
    (state: PortActorState)
    =
    async {
        use session = store.LightweightSession()

        let unwrappedEvents = events |> List.map unwrapPortEvent |> List.toArray

        // Use StartStream for new streams (version 0), Append for existing streams
        if state.Version = 0L then
            session.Events.StartStream(portId, unwrappedEvents) |> ignore
            logger.Information("Started new stream with {EventCount} events", events.Length)
        else
            session.Events.Append(portId, unwrappedEvents) |> ignore

        do! session.SaveChangesAsync() |> Async.AwaitTask

        return { state with Version = state.Version + int64 events.Length }
    }

/// Recover port state from event stream
let private recoverState (logger: ILogger) (store: IDocumentStore) (portId: Guid) =
    async {
        try
            use session = store.QuerySession()
            let! stream = session.Events.FetchStreamAsync(portId, 0L) |> Async.AwaitTask

            let events = stream |> Seq.choose (fun e -> wrapPortEvent e.Data) |> Seq.toList

            let state = events |> List.fold evolve None

            logger.Information("Recovered from {EventCount} events", events.Length)

            return { State = state; Version = int64 events.Length }
        with ex ->
            logger.Error(ex, "Failed to recover, starting fresh")
            return { State = None; Version = 0L }
    }

/// Handle a command and persist events
let private handleCommand
    (logger: ILogger)
    (store: IDocumentStore)
    (portId: Guid)
    sender
    (command: PortCommand)
    (state: PortActorState)
    =
    async {
        match decide state.State command with
        | Ok events ->
            try
                let! newState = persistEvents logger store portId events state

                // Apply events to state using fold
                let updatedState = events |> List.fold evolve newState.State
                let finalState = { newState with State = updatedState }

                sender <! PortCommandSuccess(events.Length)

                return finalState

            with ex ->
                logger.Error(ex, "Failed to persist events")
                sender <! PortCommandFailure(PersistenceError ex.Message)
                return state

        | Error error ->
            logger.Warning("Command failed: {Error}", error)
            sender <! PortCommandFailure(error)
            return state
    }

/// Check for expired reservations and send expiration commands
let private checkExpiredReservations
    (logger: ILogger)
    (portId: Guid)
    selfRef
    (state: PortActorState)
    =
    async {
        match state.State with
        | Some portState ->
            let now = DateTimeOffset.UtcNow
            let expiredReservations =
                portState.PendingReservations
                |> Map.toList
                |> List.filter (fun (_, reservation) -> reservation.ExpiresAt <= now)

            if not expiredReservations.IsEmpty then
                logger.Information(
                    "Found {Count} expired reservations: {ReservationIds}",
                    expiredReservations.Length,
                    expiredReservations |> List.map fst
                )

                // Send expiration commands for each expired reservation
                expiredReservations
                |> List.iter (fun (reservationId, reservation) ->
                    let metadata = createInitialMetadata (Some "System.ReservationExpiry")
                    let expireCmd =
                        ExpireReservation {
                            AggregateId = portId
                            VesselId = reservation.VesselId
                            ReservationId = reservationId
                            Metadata = metadata
                        }

                    selfRef <! ExecuteCommand expireCmd
                )

                return state
            else
                return state
        | None -> return state
    }

let createPortActor (portId: Guid) (documentStore: IDocumentStore) (ctx: Actor<PortProtocol>) =
    let logger = Log.ForContext("PortId", portId)

    let rec loop (state: PortActorState) =
        actor {
            let! message = ctx.Receive()

            match message with
            | ExecuteCommand command ->
                let newState =
                    let sender = ctx.Sender()
                    handleCommand logger documentStore portId sender command state
                    |> Async.RunSynchronously
                return! loop newState

            | GetState ->
                let sender = ctx.Sender()
                match state.State with
                | Some s -> sender <! PortExists s
                | None -> sender <! PortNotFound
                return! loop state

            | CheckExpiredReservations ->
                let newState =
                    checkExpiredReservations logger portId ctx.Self state |> Async.RunSynchronously
                return! loop newState
        }

    // Recovery is synchronous to ensure actor starts in valid state
    logger.Information("Starting port actor")
    let recoveredState =
        recoverState logger documentStore portId |> Async.RunSynchronously

    match recoveredState.State with
    | Some s ->
        logger.Information(
            "Port ready. Name: {Name}, Capacity: {Current}/{Max}",
            s.Name,
            s.DockedVessels.Count,
            s.MaxDocks
        )
    | None -> logger.Information("Port ready (new)")

    // Schedule periodic reservation expiry checks
    ctx.System.Scheduler.ScheduleTellRepeatedly(
        TimeSpan.FromSeconds 30.,
        TimeSpan.FromMinutes 1.,
        untyped ctx.Self, // Need to untype this for not-akkling api to understand..
        CheckExpiredReservations,
        ActorRefs.NoSender
    )

    loop recoveredState

/// Spawn function to create a typed PortActor
let spawn
    (system: Akka.Actor.ActorSystem)
    (name: string)
    (portId: Guid)
    (documentStore: IDocumentStore)
    : IActorRef<PortProtocol> =
    Akkling.Spawn.spawn system name (props (createPortActor portId documentStore))
