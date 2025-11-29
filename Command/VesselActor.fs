module Command.VesselActor

open System
open Akka.Actor
open Akka.FSharp
open Marten
open Domain.EventMetadata
open Domain.VesselAggregate
open Domain.VesselErrors
open Serilog

type VesselCommandResponse =
    | VesselCommandSuccess of eventCount: int
    | VesselCommandFailure of error: VesselError

type VesselActorState = { State: VesselState option; Version: int64 }

type VesselActorMessage =
    | ExecuteCommand of command: VesselCommand
    | GetState

type VesselStateResponse =
    | VesselExists of state: VesselState
    | VesselNotFound

let private createVesselActor
    (vesselId: Guid)
    (documentStore: IDocumentStore)
    (mailbox: Actor<obj>)
    =
    let logger = Log.ForContext("VesselId", vesselId)

    let initialState = { State = None; Version = 0L }

    let persistEvents (events: VesselEvent list) (state: VesselActorState) =
        async {
            use session = documentStore.LightweightSession()

            // NOTE: Not sure i like unwrapping DU cases like this. Marten projection events is not liking it
            let unwrappedEvents =
                events
                |> List.map (fun e ->
                    match e with
                    | VesselRegistered evt -> box evt
                    | VesselPositionUpdated evt -> box evt
                    | VesselArrived evt -> box evt
                    | VesselDeparted evt -> box evt
                    | VesselOperationalStatusUpdated evt -> box evt
                    | VesselDecommissioned evt -> box evt
                )
                |> List.toArray

            if state.Version = 0L then
                session.Events.StartStream(vesselId, unwrappedEvents) |> ignore
                logger.Information(
                    "Started new stream for vessel {VesselId} with {EventCount} events",
                    vesselId,
                    events.Length
                )
            else
                session.Events.Append(vesselId, unwrappedEvents) |> ignore
                logger.Information(
                    "Appended {EventCount} events to vessel {VesselId} stream",
                    events.Length,
                    vesselId
                )

            do! session.SaveChangesAsync() |> Async.AwaitTask

            return { state with Version = state.Version + int64 events.Length } // dangerous and as of now, no need for versioning
        }

    let loadEvents (fromVersion: int64) =
        async {
            use session = documentStore.QuerySession()
            let! stream = session.Events.FetchStreamAsync(vesselId, fromVersion) |> Async.AwaitTask

            // Still dont like this
            let events =
                stream
                |> Seq.choose (fun e ->
                    match e.Data with
                    | :? VesselRegisteredEvt as evt -> Some(VesselRegistered evt)
                    | :? VesselPositionUpdatedEvt as evt -> Some(VesselPositionUpdated evt)
                    | :? VesselArrivedEvt as evt -> Some(VesselArrived evt)
                    | :? VesselDepartedEvt as evt -> Some(VesselDeparted evt)
                    | :? VesselOperationalStatusUpdatedEvt as evt ->
                        Some(VesselOperationalStatusUpdated evt)
                    | :? VesselDecommissionedEvt as evt -> Some(VesselDecommissioned evt)
                    | _ -> None
                )
                |> Seq.toList

            logger.Information(
                "Loaded {EventCount} events for vessel {VesselId} from version {FromVersion}",
                events.Length,
                vesselId,
                fromVersion
            )

            return events
        }

    let recoverState () =
        async {
            try
                let! events = loadEvents 0L

                let state = events |> List.fold (fun s evt -> evolve s evt) None

                logger.Information(
                    "Vessel {VesselId} recovered from {EventCount} events",
                    vesselId,
                    events.Length
                )

                return { State = state; Version = int64 events.Length }
            with ex ->
                logger.Error(ex, "Failed to recover vessel {VesselId}, starting fresh", vesselId)
                return initialState
        }

    let handleCommand (sender: IActorRef) (command: VesselCommand) (state: VesselActorState) =
        async {
            logger.Information("Processing command for vessel {VesselId}", vesselId)

            match decide state.State command with
            | Ok events ->
                try
                    let! newState = persistEvents events state

                    let updatedState =
                        events |> List.fold (fun s evt -> evolve s evt) newState.State
                    let finalState = { newState with State = updatedState }

                    sender <! VesselCommandSuccess(events.Length)

                    return finalState

                with ex ->
                    logger.Error(ex, "Failed to persist events for vessel {VesselId}", vesselId)
                    sender <! VesselCommandFailure(PersistenceError ex.Message)
                    return state

            | Error error ->
                logger.Warning("Command failed for vessel {VesselId}: {Error}", vesselId, error)
                sender <! VesselCommandFailure(error)
                return state
        }

    let rec loop (state: VesselActorState) =
        actor {
            let! message = mailbox.Receive()

            match message with
            | :? VesselActorMessage as msg ->
                match msg with
                | ExecuteCommand command ->
                    let sender = mailbox.Sender()
                    let newState = handleCommand sender command state |> Async.RunSynchronously // These should be fine as synchronously since the actor handles single messages
                    return! loop newState

                | GetState ->
                    let sender = mailbox.Sender()
                    match state.State with
                    | Some s -> sender <! VesselExists s
                    | None -> sender <! VesselNotFound
                    return! loop state

            | _ ->
                logger.Warning("Unknown message type: {MessageType}", message.GetType().Name)
                return! loop state
        }

    logger.Information("Starting vessel actor {VesselId}", vesselId)
    let recoveredState = recoverState () |> Async.RunSynchronously

    match recoveredState.State with
    | Some s ->
        logger.Information(
            "Vessel {VesselId} ready. Name: {Name}, Version: {Version}",
            vesselId,
            s.Name,
            recoveredState.Version
        )
    | None -> logger.Information("Vessel {VesselId} ready (new)", vesselId)

    loop recoveredState

/// <summary>
/// Used in the creation of a new vesselActor
/// </summary>
/// <example>
/// <code>
/// let actorName = sprintf "vessel-%s" (vesselId.ToString())
/// let vesselActor = actorSystem.ActorOf(VesselActor.props vesselId documentStore, actorName)
/// </code>
/// </example>
let props (vesselId: Guid) (documentStore: IDocumentStore) =
    Props.Create(fun () -> FunActor(createVesselActor vesselId documentStore))
