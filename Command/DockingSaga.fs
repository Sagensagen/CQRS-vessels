module Command.DockingSaga

open System
open Akka.Actor
open Akka.FSharp
open Marten
open Domain.EventMetadata
open Domain.VesselAggregate
open Domain.PortAggregate
open Serilog

type DockingSagaStep =
    | Initial
    | AwaitingReservation
    | AwaitingVesselArrival
    | AwaitingDockingConfirmation
    | Completed
    | Failed of reason: string
    | Compensating
    | Compensated

[<CLIMutable>]
type DockingSagaState = {
    SagaId: Guid
    VesselId: Guid
    PortId: Guid
    ReservationId: Guid
    CurrentStep: DockingSagaStep
    StartedAt: DateTimeOffset
    Metadata: EventMetadata
}

type DockingSagaMessage =
    | StartDocking of vesselId: Guid * portId: Guid * metadata: EventMetadata
    | ReservationSucceeded of reservationId: Guid
    | ReservationFailed of error: string
    | DockingConfirmed
    | DockingFailed of error: string
    | VesselArrivalFailed of error: string
    | ReservationTimeout
    | GetSagaState of replyTo: IActorRef

type DockingSagaStateResponse =
    | SagaState of DockingSagaState
    | SagaNotStarted

/// <summary>
/// Sole purpose of this actor is to coordinate docking process between vessels and ports.
/// There are potential cases for race conditions if multiple vessels try to dock the same port,
/// and reservations with ACKS and confirmations make this more safe.
/// </summary>
let private createDockingSaga (sagaId: Guid) (documentStore: IDocumentStore) (mailbox: Actor<obj>) =
    let logger = Log.ForContext("SagaId", sagaId)

    let updateState
        (state: DockingSagaState option)
        (step: DockingSagaStep)
        (reservationId: Guid option)
        =
        match state with
        | Some s ->
            let updatedState = { s with CurrentStep = step }
            match reservationId with
            | Some rid -> Some { updatedState with ReservationId = rid }
            | None -> Some updatedState
        | None -> state

    let sendCommandToVessel (vesselId: Guid) (command: VesselCommand) (context: IActorContext) =
        let vesselPath = ActorPaths.vesselActorPath vesselId
        let vesselActor = context.ActorSelection(vesselPath)
        vesselActor.Tell(VesselActor.VesselActorMessage.ExecuteCommand(command), mailbox.Self)
        logger.Information("Sent command to vessel {VesselId}", vesselId)

    let sendCommandToPort (portId: Guid) (command: PortCommand) (context: IActorContext) =
        let portPath = ActorPaths.portActorPath portId
        let portActor = context.ActorSelection(portPath)
        portActor.Tell(PortActor.PortActorMessage.ExecuteCommand(command), mailbox.Self)
        logger.Information("Sent command to port {PortId}", portId)

    let requestReservation (state: DockingSagaState) (context: IActorContext) =
        async {
            logger.Information(
                "Step 1: Requesting docking reservation at port {PortId}",
                state.PortId
            )

            let reservationId = Guid.NewGuid()
            let metadata =
                createMetadata
                    state.Metadata.CorrelationId
                    state.Metadata.EventId
                    (Some "DockingSaga")

            let reserveCmd =
                ReserveDocking {
                    AggregateId = state.PortId
                    VesselId = state.VesselId
                    ReservationId = reservationId
                    Metadata = metadata
                }

            sendCommandToPort state.PortId reserveCmd context

            // Set timeout
            context.System.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds 30.0,
                mailbox.Self,
                ReservationTimeout,
                ActorRefs.NoSender
            )

            return updateState (Some state) AwaitingReservation (Some reservationId)
        }

    let sendVesselArrival (state: DockingSagaState) (context: IActorContext) =
        async {
            logger.Information("Step 2: Sending vessel arrival command")

            let metadata =
                createMetadata
                    state.Metadata.CorrelationId
                    state.Metadata.EventId
                    (Some "DockingSaga")

            let arriveCmd =
                ArriveAtPort {
                    AggregateId = state.VesselId
                    PortId = state.PortId
                    ReservationId = state.ReservationId
                    Metadata = metadata
                }

            sendCommandToVessel state.VesselId arriveCmd context

            return updateState (Some state) AwaitingVesselArrival None
        }

    let sendDockingConfirmation (state: DockingSagaState) (context: IActorContext) =
        async {
            logger.Information("Step 3: Sending docking confirmation to port")

            let metadata =
                createMetadata
                    state.Metadata.CorrelationId
                    state.Metadata.EventId
                    (Some "DockingSaga")

            let confirmCmd =
                ConfirmDocking {
                    AggregateId = state.PortId
                    VesselId = state.VesselId
                    ReservationId = state.ReservationId
                    Metadata = metadata
                }

            sendCommandToPort state.PortId confirmCmd context

            return updateState (Some state) AwaitingDockingConfirmation None
        }

    let compensate (state: DockingSagaState) (reason: string) (context: IActorContext) =
        async {
            logger.Warning("Saga failed: {Reason}. Starting compensation", reason)

            if state.ReservationId <> Guid.Empty then
                logger.Information(
                    "Compensating: Expiring reservation {ReservationId}",
                    state.ReservationId
                )

                let metadata =
                    createMetadata
                        state.Metadata.CorrelationId
                        state.Metadata.EventId
                        (Some "DockingSaga.Compensation")

                let expireCmd =
                    ExpireReservation {
                        AggregateId = state.PortId
                        VesselId = state.VesselId
                        ReservationId = state.ReservationId
                        Metadata = metadata
                    }

                sendCommandToPort state.PortId expireCmd context

            return updateState (Some state) (Failed reason) None
        }

    let handleMessage (message: obj) (state: DockingSagaState option) (context: IActorContext) =
        async {
            match message, state with
            // DockingSagaMessage types
            | :? DockingSagaMessage as msg, None when
                (match msg with
                 | StartDocking _ -> true
                 | _ -> false)
                ->
                match msg with
                | StartDocking(vesselId, portId, metadata) ->
                    let newState = {
                        SagaId = sagaId
                        VesselId = vesselId
                        PortId = portId
                        ReservationId = Guid.Empty
                        CurrentStep = Initial
                        StartedAt = DateTimeOffset.UtcNow
                        Metadata = metadata
                    }

                    return! requestReservation newState context
                | _ -> return None

            | :? DockingSagaMessage as msg, Some s ->
                match msg with
                | ReservationTimeout when s.CurrentStep = AwaitingReservation ->
                    logger.Warning("Reservation request timed out")
                    return! compensate s "Reservation timeout" context

                | GetSagaState replyTo ->
                    replyTo <! SagaState s
                    return Some s

                | _ ->
                    logger.Warning(
                        "Unhandled DockingSagaMessage in state {State}: {Message}",
                        s.CurrentStep,
                        msg
                    )
                    return Some s

            | :? DockingSagaMessage as (GetSagaState replyTo), None ->
                replyTo <! SagaNotStarted
                return None

            // PortCommandResponse types
            | :? PortActor.PortCommandResponse as response, Some s ->
                match response, s.CurrentStep with
                | PortActor.PortCommandSuccess _, AwaitingReservation ->
                    logger.Information("Port reservation succeeded, proceeding to vessel arrival")
                    return! sendVesselArrival s context

                | PortActor.PortCommandFailure error, AwaitingReservation ->
                    logger.Warning("Reservation failed: {Error}", error)
                    return! compensate s (error.ToString()) context

                | PortActor.PortCommandSuccess _, AwaitingDockingConfirmation ->
                    logger.Information("Docking completed successfully!")
                    return updateState (Some s) Completed None

                | PortActor.PortCommandFailure error, AwaitingDockingConfirmation ->
                    logger.Warning("Docking confirmation failed: {Error}", error)
                    return! compensate s (error.ToString()) context

                | _ ->
                    logger.Warning(
                        "Unhandled PortCommandResponse in state {State}: {Response}",
                        s.CurrentStep,
                        response
                    )
                    return Some s

            // VesselCommandResponse types
            | :? VesselActor.VesselCommandResponse as response, Some s ->
                match response, s.CurrentStep with
                | VesselActor.VesselCommandSuccess _, AwaitingVesselArrival ->
                    logger.Information(
                        "Vessel arrival succeeded, proceeding to docking confirmation"
                    )
                    return! sendDockingConfirmation s context

                | VesselActor.VesselCommandFailure error, AwaitingVesselArrival ->
                    logger.Warning("Vessel arrival failed: {Error}", error)
                    return! compensate s (error.ToString()) context

                | _ ->
                    logger.Warning(
                        "Unhandled VesselCommandResponse in state {State}: {Response}",
                        s.CurrentStep,
                        response
                    )
                    return Some s

            | _ ->
                logger.Warning("Unknown message type: {MessageType}", message.GetType().Name)
                return state
        }

    let rec loop (state: DockingSagaState option) =
        actor {
            let! message = mailbox.Receive()
            let context = mailbox.Context
            let newState = handleMessage message state context |> Async.RunSynchronously
            match newState with
            | Some s when s.CurrentStep.IsCompleted ->
                logger.Error "Killing saga - Complete"
                context.Stop(mailbox.Self)
            | Some s when s.CurrentStep.IsFailed ->
                logger.Error "Killing saga -  Faillure"
                context.Stop(mailbox.Self)
            | _ -> return! loop newState
        }

    logger.Information("Starting docking saga {SagaId} (in-memory)", sagaId)
    loop None

let props (sagaId: Guid) (documentStore: IDocumentStore) =
    Props.Create(fun () -> FunActor(createDockingSaga sagaId documentStore))
