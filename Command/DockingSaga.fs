module Command.DockingSaga

open System
open Akka.Actor
open Akkling
open Akkling.Actors
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
    OriginalSender: IActorRef option
}

/// Response messages sent back to the original caller
type DockingSagaResponse =
    | DockingSagaCompleted of sagaId: Guid
    | DockingSagaFailed of sagaId: Guid * error: string

type DockingSagaStateResponse =
    | SagaState of DockingSagaState
    | SagaNotStarted

// Typed protocol for Akkling - saga-specific messages only (responses handled via Ask)
type DockingSagaProtocol =
    | StartDocking of vesselId: Guid * portId: Guid * metadata: EventMetadata
    | ReservationTimeout
    | GetSagaState of replyTo: IActorRef

/// <summary>
/// Sole purpose of this actor is to coordinate docking process between vessels and ports.
/// There are potential cases for race conditions if multiple vessels try to dock the same port,
/// and reservations with ACKS and confirmations make this more safe.
/// </summary>
let createDockingSaga (sagaId: Guid) (ctx: Actor<DockingSagaProtocol>) =
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
        async {
            let vesselPath = ActorPaths.vesselActorPath vesselId
            let vesselActor = context.ActorSelection(vesselPath)
            logger.Information("Sending command to vessel {VesselId}", vesselId)

            let! response =
                vesselActor.Ask<VesselActor.VesselCommandResponse>(
                    VesselActor.VesselProtocol.ExecuteCommand(command),
                    (TimeSpan.FromSeconds 30.0)
                )
                |> Async.AwaitTask

            return response
        }

    let sendCommandToPort (portId: Guid) (command: PortCommand) (context: IActorContext) =
        async {
            let portPath = ActorPaths.portActorPath portId
            let portActor = context.ActorSelection(portPath)
            logger.Information("Sending command to port {PortId}", portId)

            let! response =
                portActor.Ask<PortActor.PortCommandResponse>(
                    PortActor.PortProtocol.ExecuteCommand(command),
                    (TimeSpan.FromSeconds 30.0)
                )
                |> Async.AwaitTask

            return response
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
                ()

            return updateState (Some state) (Failed reason) None
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

            let! response = sendCommandToPort state.PortId confirmCmd context

            match response with
            | PortActor.PortCommandSuccess _ ->
                logger.Information("Docking completed successfully!")
                return updateState (Some state) Completed None

            | PortActor.PortCommandFailure error ->
                logger.Warning("Docking confirmation failed: {Error}", error)
                return! compensate state (error.ToString()) context
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

            let! response = sendCommandToVessel state.VesselId arriveCmd context

            match response with
            | VesselActor.VesselCommandSuccess _ ->
                logger.Information("Vessel arrival succeeded, proceeding to docking confirmation")
                let stateWithArrival = updateState (Some state) AwaitingVesselArrival None
                return! sendDockingConfirmation (Option.get stateWithArrival) context

            | VesselActor.VesselCommandFailure error ->
                logger.Warning("Vessel arrival failed: {Error}", error)
                return! compensate state (error.ToString()) context
        }

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

            let! response = sendCommandToPort state.PortId reserveCmd context

            match response with
            | PortActor.PortCommandSuccess _ ->
                logger.Information("Port reservation succeeded, proceeding to vessel arrival")
                let stateWithReservation =
                    updateState (Some state) AwaitingReservation (Some reservationId)

                return! sendVesselArrival (Option.get stateWithReservation) context

            | PortActor.PortCommandFailure error ->
                logger.Warning("Reservation failed: {Error}", error)
                return! compensate state (error.ToString()) context
        }

    let handleMessage
        (message: DockingSagaProtocol)
        (state: DockingSagaState option)
        (context: IActorContext)
        =
        async {
            match message, state with
            // Start docking - only when state is None
            | StartDocking(vesselId, portId, metadata), None ->
                let originalSender = ctx.Sender()
                let newState = {
                    SagaId = sagaId
                    VesselId = vesselId
                    PortId = portId
                    ReservationId = Guid.Empty
                    CurrentStep = Initial
                    StartedAt = DateTimeOffset.UtcNow
                    Metadata = metadata
                    OriginalSender = originalSender |> untyped |> Some
                }
                return! requestReservation newState context

            | ReservationTimeout, Some s when s.CurrentStep = AwaitingReservation ->
                logger.Warning("Reservation request timed out")
                return! compensate s "Reservation timeout" context

            | GetSagaState replyTo, Some s ->
                typed replyTo <! SagaState s
                return Some s

            | GetSagaState replyTo, None ->
                typed replyTo <! SagaNotStarted
                return None

            | _, Some s ->
                logger.Warning(
                    "Unhandled message in state {State}: {Message}",
                    s.CurrentStep,
                    message
                )
                return Some s

            | _, None ->
                logger.Warning("Unhandled message when state is None: {Message}", message)
                return None
        }

    let rec loop (state: DockingSagaState option) =
        actor {
            let! message = ctx.Receive()
            let context = ctx.UntypedContext
            let newState = handleMessage message state context |> Async.RunSynchronously
            match newState with
            | Some s when s.CurrentStep.IsCompleted ->
                logger.Information
                    "Saga completed successfully - notifying caller and stopping actor"
                // Notify original sender of completion
                match s.OriginalSender with
                | Some sender -> typed sender <! DockingSagaResponse.DockingSagaCompleted s.SagaId
                | None -> logger.Warning "No original sender to notify of completion"
                context.Stop(untyped ctx.Self)

            | Some s when s.CurrentStep.IsFailed ->
                let reason =
                    match s.CurrentStep with
                    | Failed r -> r
                    | _ -> "Unknown"
                logger.Warning(
                    "Saga failed: {Reason} - notifying caller and stopping actor",
                    reason
                )
                // Notify original sender of failure
                match s.OriginalSender with
                | Some sender ->
                    typed sender <! DockingSagaResponse.DockingSagaFailed(s.SagaId, reason)
                | None -> logger.Warning "No original sender to notify of failure"
                context.Stop(untyped ctx.Self)
            | _ -> return! loop newState
        }

    logger.Information("Starting docking saga {SagaId} (in-memory)", sagaId)
    loop None

/// Spawn function to create a typed DockingSaga
let spawn
    (system: Akka.Actor.ActorSystem)
    (name: string)
    (sagaId: Guid)
    : IActorRef<DockingSagaProtocol> =
    Akkling.Spawn.spawn system name (props (createDockingSaga sagaId))
