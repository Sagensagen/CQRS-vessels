module Command.PortActor

open System
open Akka.Actor
open Akka.FSharp
open Marten
open Domain.EventMetadata
open Domain.PortAggregate
open Shared.Api.Port
open Serilog

type PortActorState = { State: PortState option; Version: int64 }

type PortCommandResponse =
    | PortCommandSuccess of eventCount: int
    | PortCommandFailure of error: PortCommandErrors

type PortActorMessage =
    | ExecuteCommand of command: PortCommand
    | GetState
    | CheckExpiredReservations

type PortStateResponse =
    | PortExists of state: PortState
    | PortNotFound

let createPortActor (portId: Guid) (documentStore: IDocumentStore) (mailbox: Actor<obj>) =
    let logger = Log.ForContext("PortId", portId)

    // Local state just for the actor. Projection is much more detailed
    let initialState = { State = None; Version = 0L }

    // Write all events to db
    let persistEvents (events: PortEvent list) (state: PortActorState) =
        async {
            use session = documentStore.LightweightSession()

            // Unwrap DU cases to get the underlying record types for Marten projections
            let unwrappedEvents =
                events
                |> List.map (fun e ->
                    match e with
                    | PortRegistered evt -> box evt
                    | VesselDockingReserved evt -> box evt
                    | DockingConfirmed evt -> box evt
                    | DockingReservationExpired evt -> box evt
                    | VesselUndocked evt -> box evt
                    | PortOpened evt -> box evt
                    | PortClosed evt -> box evt
                )
                |> List.toArray

            // Use StartStream for new streams (version 0), Append for existing streams
            if state.Version = 0L then
                session.Events.StartStream(portId, unwrappedEvents) |> ignore
                logger.Information(
                    "Started new stream for port {PortId} with {EventCount} events",
                    portId,
                    events.Length
                )
            else
                session.Events.Append(portId, unwrappedEvents) |> ignore
                logger.Information(
                    "Appended {EventCount} events to port {PortId} stream",
                    events.Length,
                    portId
                )

            do! session.SaveChangesAsync() |> Async.AwaitTask

            return { state with Version = state.Version + int64 events.Length }
        }

    let loadEvents (fromVersion: int64) =
        async {
            use session = documentStore.QuerySession()
            let! stream = session.Events.FetchStreamAsync(portId, fromVersion) |> Async.AwaitTask

            let events =
                stream
                |> Seq.choose (fun e ->
                    match e.Data with
                    | :? PortRegisteredEvt as evt -> Some(PortRegistered evt)
                    | :? VesselDockingReservedEvt as evt -> Some(VesselDockingReserved evt)
                    | :? DockingConfirmedEvt as evt -> Some(DockingConfirmed evt)
                    | :? DockingReservationExpiredEvt as evt -> Some(DockingReservationExpired evt)
                    | :? VesselUndockedEvt as evt -> Some(VesselUndocked evt)
                    | :? PortOpenedEvt as evt -> Some(PortOpened evt)
                    | :? PortClosedEvt as evt -> Some(PortClosed evt)
                    | :? PortEvent as pe -> Some pe
                    | _ -> None
                )
                |> Seq.toList

            logger.Information(
                "Loaded {EventCount} events for port {PortId} from version {FromVersion}",
                events.Length,
                portId,
                fromVersion
            )

            return events
        }

    let recoverState () =
        async {
            try
                // Load all events from the beginning
                let! events = loadEvents 0L

                let state = events |> List.fold (fun s evt -> evolve s evt) None

                logger.Information(
                    "Port {PortId} recovered from {EventCount} events",
                    portId,
                    events.Length
                )

                return { State = state; Version = int64 events.Length }
            with ex ->
                logger.Error(ex, "Failed to recover port {PortId}, starting fresh", portId)
                return initialState
        }

    let handleCommand (sender: IActorRef) (command: PortCommand) (state: PortActorState) =
        async {
            logger.Information("Processing command for port {PortId}", portId)

            match decide state.State command with
            | Ok events ->
                try
                    let! newState = persistEvents events state

                    // Apply events to state using fold
                    let updatedState =
                        events |> List.fold (fun s evt -> evolve s evt) newState.State
                    let finalState = { newState with State = updatedState }

                    sender <! PortCommandSuccess(events.Length)

                    return finalState

                with ex ->
                    logger.Error(ex, "Failed to persist events for port {PortId}", portId)
                    sender <! PortCommandFailure(PersistenceError ex.Message)
                    return state

            | Error error ->
                logger.Warning("Command failed for port {PortId}: {Error}", portId, error)
                sender <! PortCommandFailure(error)
                return state
        }

    let checkExpiredReservations (state: PortActorState) =
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
                        "Found {Count} expired reservations for port {PortId}",
                        expiredReservations.Length,
                        portId
                    )

                    // Process each expired reservation

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

                        // Send command to self and wait for it to be processed
                        mailbox.Self <! ExecuteCommand expireCmd
                    )

                    return state
                else
                    return state
            | None -> return state
        }

    let rec loop (state: PortActorState) =
        actor {
            let! message = mailbox.Receive()

            match message with
            | :? PortActorMessage as msg ->
                match msg with
                | ExecuteCommand command ->
                    let sender = mailbox.Sender()
                    let newState = handleCommand sender command state |> Async.RunSynchronously
                    return! loop newState

                | GetState ->
                    let sender = mailbox.Sender()
                    match state.State with
                    | Some s -> sender <! PortExists s
                    | None -> sender <! PortNotFound
                    return! loop state

                | CheckExpiredReservations ->
                    // State is not changed, but the messages from the function will updated the state in time when received.
                    let newState = checkExpiredReservations state |> Async.RunSynchronously
                    return! loop newState

            | _ ->
                logger.Warning("Unknown message type: {MessageType}", message.GetType().Name)
                return! loop state
        }

    // Recovery is synchronous to ensure actor starts in valid state
    // This only happens once at startup, so blocking is acceptable
    logger.Information("Starting port actor {PortId}", portId)
    let recoveredState = recoverState () |> Async.RunSynchronously

    match recoveredState.State with
    | Some s ->
        logger.Information(
            "Port {PortId} ready. Name: {Name}, Capacity: {Current}/{Max}",
            portId,
            s.Name,
            s.DockedVessels.Count,
            s.MaxDocks
        )
    | None -> logger.Information("Port {PortId} ready (new)", portId)

    // Schedule periodic reservation expiry checks
    mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(
        TimeSpan.FromSeconds 30.,
        TimeSpan.FromMinutes 1.,
        mailbox.Self,
        CheckExpiredReservations,
        ActorRefs.NoSender
    )

    loop recoveredState

let props (portId: Guid) (documentStore: IDocumentStore) =
    Props.Create(fun () -> FunActor(createPortActor portId documentStore))
