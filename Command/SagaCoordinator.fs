module Command.SagaCoordinator

open System
open Akka.Actor
open Akka.FSharp
open Marten
open Domain.EventMetadata
open Command.DockingSaga
open Serilog

type SagaCoordinatorMessage =
    | StartDockingSaga of vesselId: Guid * portId: Guid * metadata: EventMetadata
// | StartUndockingSaga of vesselId: Guid * portId: Guid * metadata: EventMetadata // TODO: add undocking as part of saga?

type SagaInfo = {
    SagaId: Guid
    SagaType: string
    VesselId: Guid
    PortId: Guid
    StartedAt: DateTimeOffset
}

let createSagaCoordinator (documentStore: IDocumentStore) (mailbox: Actor<obj>) =
    let logger = Log.ForContext("Actor", "SagaCoordinator")

    let createDockingSaga
        (vesselId: Guid)
        (portId: Guid)
        (metadata: EventMetadata)
        (context: IActorContext)
        =
        let sagaId = Guid.NewGuid()
        let sagaName = ActorPaths.dockingSagaName sagaId

        logger.Information(
            "Creating docking saga {SagaId} for vessel {VesselId} at port {PortId}",
            sagaId,
            vesselId,
            portId
        )

        let sagaActor = context.ActorOf(DockingSaga.props sagaId documentStore, sagaName)

        sagaActor.Tell(DockingSagaMessage.StartDocking(vesselId, portId, metadata), mailbox.Self)

        sagaId

    let handleMessage (message: obj) (context: IActorContext) =
        match message with
        | :? SagaCoordinatorMessage as msg ->
            match msg with
            | StartDockingSaga(vesselId, portId, metadata) ->
                let sagaId = createDockingSaga vesselId portId metadata context
                let sender = mailbox.Sender()
                sender <! sagaId

        | _ -> logger.Warning("Unknown message type: {MessageType}", message.GetType().Name)

    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            let context = mailbox.Context
            handleMessage message context
            return! loop ()
        }

    logger.Information("Starting saga coordinator")
    loop ()

let props (documentStore: IDocumentStore) =
    Props.Create(fun () -> FunActor(createSagaCoordinator documentStore))
