module Command.CargoActor

open System
open Akkling
open Marten
open Domain.CargoAggregate
open Serilog

type CargoActorState = { State: CargoState option; Version: int64 }

type CargoCommandResponse =
    | CargoCommandSuccess of eventCount: int
    | CargoCommandFailure of error: Shared.Api.Cargo.CargoCommandErrors

type CargoStateResponse =
    | CargoExists of state: CargoState
    | CargoNotFound

type CargoProtocol =
    | ExecuteCommand of CargoCommand
    | GetState
    | CheckExpiredReservations

let private unwrapCargoEvent (event: CargoEvent) : obj =
    match event with
    | CargoCreated evt -> box evt
    | CargoReservedForVessel evt -> box evt
    | CargoReservationCancelled evt -> box evt
    | CargoLoadedOnVessel evt -> box evt
    | CargoMarkedInTransit evt -> box evt
    | CargoUnloadedFromVessel evt -> box evt
    | CargoDelivered evt -> box evt
    | CargoEvent.CargoCancelled evt -> box evt

let private wrapCargoEvent (data: obj) : CargoEvent option =
    match data with
    | :? CargoCreatedEvt as evt -> Some(CargoCreated evt)
    | :? CargoReservedForVesselEvt as evt -> Some(CargoReservedForVessel evt)
    | :? CargoReservationCancelledEvt as evt -> Some(CargoReservationCancelled evt)
    | :? CargoLoadedOnVesselEvt as evt -> Some(CargoLoadedOnVessel evt)
    | :? CargoMarkedInTransitEvt as evt -> Some(CargoMarkedInTransit evt)
    | :? CargoUnloadedFromVesselEvt as evt -> Some(CargoUnloadedFromVessel evt)
    | :? CargoDeliveredEvt as evt -> Some(CargoDelivered evt)
    | :? CargoCancelledEvt as evt -> Some(CargoEvent.CargoCancelled evt)
    | _ -> None

let private persistEvents
    (logger: ILogger)
    (store: IDocumentStore)
    (cargoId: Guid)
    (events: CargoEvent list)
    (state: CargoActorState)
    =
    async {
        use session = store.LightweightSession()

        let unwrappedEvents = events |> List.map unwrapCargoEvent |> List.toArray

        if state.Version = 0L then
            session.Events.StartStream(cargoId, unwrappedEvents) |> ignore
            logger.Information("Started new stream with {EventCount} events", events.Length)
        else
            session.Events.Append(cargoId, unwrappedEvents) |> ignore

        do! session.SaveChangesAsync() |> Async.AwaitTask

        return { state with Version = state.Version + int64 events.Length }
    }

let private recoverState (logger: ILogger) (store: IDocumentStore) (cargoId: Guid) =
    async {
        try
            use session = store.QuerySession()
            let! stream = session.Events.FetchStreamAsync(cargoId, 0L) |> Async.AwaitTask

            let events = stream |> Seq.choose (fun e -> wrapCargoEvent e.Data) |> Seq.toList

            let state = events |> List.fold evolve None

            logger.Information("Recovered from {EventCount} events", events.Length)

            return { State = state; Version = int64 events.Length }
        with ex ->
            logger.Error(ex, "Failed to recover, starting fresh")
            return { State = None; Version = 0L }
    }

let private handleCommand
    (logger: ILogger)
    (store: IDocumentStore)
    (cargoId: Guid)
    (sender)
    (command: CargoCommand)
    (state: CargoActorState)
    =
    async {
        match decide state.State command with
        | Ok events ->
            try
                let! newState = persistEvents logger store cargoId events state

                let updatedState = events |> List.fold evolve newState.State
                let finalState = { newState with State = updatedState }

                sender <! CargoCommandSuccess(events.Length)

                return finalState

            with ex ->
                logger.Error(ex, "Failed to persist events")
                sender <! CargoCommandFailure(Shared.Api.Cargo.PersistenceError ex.Message)
                return state

        | Error error ->
            logger.Warning("Command failed: {Error}", error)
            sender <! CargoCommandFailure(error)
            return state
    }

let createCargoActor (cargoId: Guid) (documentStore: IDocumentStore) (ctx: Actor<CargoProtocol>) =
    let logger = Log.ForContext("CargoId", cargoId)

    let rec loop (state: CargoActorState) =
        actor {
            let! message = ctx.Receive()

            match message with
            | ExecuteCommand command ->
                let sender = ctx.Sender()
                let newState =
                    handleCommand logger documentStore cargoId sender command state
                    |> Async.RunSynchronously
                return! loop newState

            | GetState ->
                let sender = ctx.Sender()
                match state.State with
                | Some s -> sender <! CargoExists s
                | None -> sender <! CargoNotFound
                return! loop state

            | CheckExpiredReservations ->
                // Check if cargo has an expired reservation
                match state.State with
                | Some cargo ->
                    match cargo.Status with
                    | ReservedForVessel(vesselId, reservedAt) ->
                        // Check if reservation has expired (5 minutes from reservation time)
                        let expiresAt = reservedAt.AddMinutes(5.0)
                        if DateTimeOffset.UtcNow > expiresAt then
                            logger.Warning(
                                "Reservation expired for vessel {VesselId}, cancelling",
                                vesselId
                            )

                            let command =
                                CancelReservation {
                                    AggregateId = cargoId
                                    Reason = "Reservation expired"
                                    Metadata =
                                        Domain.EventMetadata.createInitialMetadata (
                                            Some "CargoActor.ExpirationCheck"
                                        )
                                }

                            // Send message to self to be handled
                            ctx.Self <! ExecuteCommand command
                            return! loop state
                        else
                            // Reservation still valid
                            return! loop state
                    | _ ->
                        // Not reserved, no action needed
                        return! loop state
                | None -> return! loop state
        }

    // Initialize and start
    logger.Information("Starting cargo actor")
    let recoveredState =
        recoverState logger documentStore cargoId |> Async.RunSynchronously

    match recoveredState.State with
    | Some s ->
        logger.Information(
            "Cargo ready. Status: {Status}, Version: {Version}",
            s.Status,
            recoveredState.Version
        )
    | None -> logger.Information("Cargo ready (new)")

    // Schedule periodic checks for expired reservations (every 1 minute)
    ctx.Schedule (TimeSpan.FromMinutes(1.0)) ctx.Self CheckExpiredReservations
    |> ignore

    loop recoveredState

/// Spawn function to create a typed CargoActor
let spawn
    (system: Akka.Actor.ActorSystem)
    (name: string)
    (cargoId: Guid)
    (documentStore: IDocumentStore)
    : IActorRef<CargoProtocol> =
    Akkling.Spawn.spawn system name (props (createCargoActor cargoId documentStore))
