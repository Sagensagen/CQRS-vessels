module Command.VesselActor

open System
open Akkling
open Marten
open Domain.VesselAggregate
open Shared.Api.Vessel
open Serilog

// Used for communicating across actors
type VesselCommandResponse =
    | VesselCommandSuccess of eventCount: int
    | VesselCommandFailure of error: VesselCommandErrors

type VesselActorState = { State: VesselState option; Version: int64 } // Needed for snapshots later

type VesselStateResponse =
    | VesselExists of state: VesselState
    | VesselNotFound

// Typed protocol for Akkling
type VesselProtocol =
    | ExecuteCommand of VesselCommand
    | GetState

let private unwrapVesselEvent (event: VesselEvent) : obj =
    match event with
    | VesselRegistered evt -> box evt
    | VesselPositionUpdated evt -> box evt
    | VesselArrived evt -> box evt
    | VesselDeparted evt -> box evt
    | VesselOperationalStatusUpdated evt -> box evt
    | VesselDecommissioned evt -> box evt
    | CargoLoaded evt -> box evt
    | CargoUnloaded evt -> box evt

let private wrapVesselEvent (data: obj) : VesselEvent option =
    match data with
    | :? VesselRegisteredEvt as evt -> Some(VesselRegistered evt)
    | :? VesselPositionUpdatedEvt as evt -> Some(VesselPositionUpdated evt)
    | :? VesselArrivedEvt as evt -> Some(VesselArrived evt)
    | :? VesselDepartedEvt as evt -> Some(VesselDeparted evt)
    | :? VesselOperationalStatusUpdatedEvt as evt -> Some(VesselOperationalStatusUpdated evt)
    | :? VesselDecommissionedEvt as evt -> Some(VesselDecommissioned evt)
    | :? CargoLoadedEvt as evt -> Some(CargoLoaded evt)
    | :? CargoUnloadedEvt as evt -> Some(CargoUnloaded evt)
    | _ -> None

/// <summary>
///  Persist events to Marten event store
/// </summary>
let private persistEvents
    (logger: ILogger)
    (store: IDocumentStore)
    (vesselId: Guid)
    (events: VesselEvent list)
    (state: VesselActorState)
    =
    async {
        use session = store.LightweightSession()

        let unwrappedEvents = events |> List.map unwrapVesselEvent |> List.toArray

        if state.Version = 0L then
            session.Events.StartStream(vesselId, unwrappedEvents) |> ignore
            logger.Information("Started new stream with {EventCount} events", events.Length)
        else
            session.Events.Append(vesselId, unwrappedEvents) |> ignore

        do! session.SaveChangesAsync() |> Async.AwaitTask

        return { state with Version = state.Version + int64 events.Length }
    }

/// Recover vessel state from event stream
let private recoverState (logger: ILogger) (store: IDocumentStore) (vesselId: Guid) =
    async {
        try
            use session = store.QuerySession()
            let! stream = session.Events.FetchStreamAsync(vesselId, 0L) |> Async.AwaitTask

            let events = stream |> Seq.choose (fun e -> wrapVesselEvent e.Data) |> Seq.toList

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
    (vesselId: Guid)
    (sender)
    (command: VesselCommand)
    (state: VesselActorState)
    =
    async {
        match decide state.State command with
        | Ok events ->
            try
                let! newState = persistEvents logger store vesselId events state

                let updatedState = events |> List.fold evolve newState.State
                let finalState = { newState with State = updatedState }

                sender <! VesselCommandSuccess(events.Length)

                return finalState

            with ex ->
                logger.Error(ex, "Failed to persist events")
                sender <! VesselCommandFailure(PersistenceError ex.Message)
                return state

        | Error error ->
            logger.Warning("Command failed: {Error}", error)
            sender <! VesselCommandFailure(error)
            return state
    }

let createVesselActor
    (vesselId: Guid)
    (documentStore: IDocumentStore)
    (ctx: Actor<VesselProtocol>)
    =
    let logger = Log.ForContext("VesselId", vesselId)

    let rec loop (state: VesselActorState) =
        actor {
            let! message = ctx.Receive()

            match message with
            | ExecuteCommand command ->
                let sender = ctx.Sender()
                let newState =
                    handleCommand logger documentStore vesselId sender command state
                    |> Async.RunSynchronously
                return! loop newState

            | GetState ->
                let sender = ctx.Sender()
                match state.State with
                | Some s -> sender <! VesselExists s
                | None -> sender <! VesselNotFound
                return! loop state
        }

    // Initialize and start
    logger.Information("Starting vessel actor")
    let recoveredState =
        recoverState logger documentStore vesselId |> Async.RunSynchronously

    match recoveredState.State with
    | Some s ->
        logger.Information(
            "Vessel ready. Name: {Name}, Version: {Version}",
            s.Name,
            recoveredState.Version
        )
    | None -> logger.Information("Vessel ready (new)")

    loop recoveredState

/// Spawn function to create a typed VesselActor
let spawn
    (system: Akka.Actor.ActorSystem)
    (name: string)
    (vesselId: Guid)
    (documentStore: IDocumentStore)
    : IActorRef<VesselProtocol> =
    Akkling.Spawn.spawn system name (props (createVesselActor vesselId documentStore))
