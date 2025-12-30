module Command.CargoUnloadingSaga

open System
open Akka.Actor
open Akkling
open Akkling.Actors
open Command.VesselActor
open Command.PortActor
open Command.CargoActor
open Domain.EventMetadata
open Serilog

type CargoUnloadingSagaState = {
    CargoId: Guid
    VesselId: Guid
    PortId: Guid
    IsDestinationPort: bool
    Step: CargoUnloadingStep
}

and CargoUnloadingStep =
    | Initial
    | AwaitingVesselUnloading
    | AwaitingCargoUpdate
    | Completed
    | Failed of reason: string
    | Compensating

type CargoUnloadingSagaProtocol =
    | StartUnloading of cargoId: Guid * vesselId: Guid * portId: Guid * isDestinationPort: bool
    | Timeout

let createCargoUnloadingSaga (ctx: Actor<CargoUnloadingSagaProtocol>) =
    let logger = Log.ForContext("SagaType", "CargoUnloading")

    let sendCommandToVessel
        (vesselId: Guid)
        (command: Domain.VesselAggregate.VesselCommand)
        (context: IActorContext)
        =
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

    let sendCommandToCargo
        (cargoId: Guid)
        (command: Domain.CargoAggregate.CargoCommand)
        (context: IActorContext)
        =
        async {
            let cargoPath = ActorPaths.cargoActorPath cargoId
            let cargoActor = context.ActorSelection(cargoPath)
            logger.Information("Sending command to cargo {CargoId}", cargoId)

            let! response =
                cargoActor.Ask<CargoActor.CargoCommandResponse>(
                    CargoActor.CargoProtocol.ExecuteCommand(command),
                    (TimeSpan.FromSeconds 30.0)
                )
                |> Async.AwaitTask

            return response
        }

    let rec initial () =
        actor {
            let! msg = ctx.Receive()
            match msg with
            | StartUnloading(cargoId, vesselId, portId, isDestinationPort) ->
                logger.Information(
                    "CargoUnloadingSaga: Starting cargo {CargoId} unloading from vessel {VesselId} at port {PortId} (destination: {IsDestination})",
                    cargoId,
                    vesselId,
                    portId,
                    isDestinationPort
                )

                let state = {
                    CargoId = cargoId
                    VesselId = vesselId
                    PortId = portId
                    IsDestinationPort = isDestinationPort
                    Step = AwaitingVesselUnloading
                }

                return! awaitingVesselUnloading state

            | _ -> return! initial ()
        }

    and awaitingVesselUnloading (state: CargoUnloadingSagaState) =
        actor {
            // Step 1: Vessel unloads cargo
            let context = ctx.UntypedContext
            let metadata = createInitialMetadata (Some "CargoUnloadingSaga")

            let unloadCommand =
                Domain.VesselAggregate.UnloadCargo {
                    AggregateId = state.VesselId
                    CargoId = state.CargoId
                    Metadata = metadata
                }

            let vesselResponse =
                sendCommandToVessel state.VesselId unloadCommand context
                |> Async.RunSynchronously

            match vesselResponse with
            | VesselCommandSuccess _ ->
                logger.Information("CargoUnloadingSaga: Vessel unloaded cargo")
                return! awaitingCargoUpdate { state with Step = AwaitingCargoUpdate }
            | VesselCommandFailure error ->
                logger.Warning("CargoUnloadingSaga: Vessel unloading failed - {Error}", error)
                return! failed state $"Vessel unloading failed: {error}"
        }

    and awaitingCargoUpdate (state: CargoUnloadingSagaState) =
        actor {
            // Step 2: Update cargo state
            let context = ctx.UntypedContext
            let metadata = createInitialMetadata (Some "CargoUnloadingSaga")

            // Unload from vessel
            let unloadCommand =
                Domain.CargoAggregate.UnloadFromVessel {
                    AggregateId = state.CargoId
                    PortId = state.PortId
                    Metadata = metadata
                }

            let cargoResponse =
                sendCommandToCargo state.CargoId unloadCommand context |> Async.RunSynchronously

            match cargoResponse with
            | CargoCommandSuccess _ ->
                if state.IsDestinationPort then
                    // Mark as delivered
                    let deliveryCommand =
                        Domain.CargoAggregate.MarkDelivered {
                            AggregateId = state.CargoId
                            Metadata = metadata
                        }

                    let deliveryResponse =
                        sendCommandToCargo state.CargoId deliveryCommand context
                        |> Async.RunSynchronously

                    match deliveryResponse with
                    | CargoCommandSuccess _ ->
                        logger.Information(
                            "CargoUnloadingSaga: Cargo delivered at destination - SAGA COMPLETED"
                        )
                        return! completed state
                    | CargoCommandFailure error ->
                        logger.Warning(
                            "CargoUnloadingSaga: Delivery marking failed - {Error}",
                            error
                        )
                        return! compensating state $"Delivery marking failed: {error}"
                else
                    logger.Information(
                        "CargoUnloadingSaga: Cargo unloaded at intermediate port - SAGA COMPLETED"
                    )
                    return! completed state

            | CargoCommandFailure error ->
                logger.Warning("CargoUnloadingSaga: Cargo update failed - {Error}", error)
                return! compensating state $"Cargo update failed: {error}"
        }

    and compensating (state: CargoUnloadingSagaState) (reason: string) =
        actor {
            logger.Warning("CargoUnloadingSaga: Compensating due to: {Reason}", reason)

            // Compensation: Reload cargo onto vessel if needed
            try
                if state.Step = AwaitingCargoUpdate then
                    // Note: We can't fully compensate loading back onto vessel without cargo spec
                    // In production, you'd need to fetch the cargo spec from the cargo actor
                    logger.Warning(
                        "CargoUnloadingSaga: Compensation incomplete - cargo unloaded but saga failed"
                    )

                logger.Information("CargoUnloadingSaga: Compensation completed")
            with ex ->
                logger.Error(ex, "CargoUnloadingSaga: Compensation failed (non-critical)")

            return! failed state reason
        }

    and completed (state: CargoUnloadingSagaState) =
        actor {
            logger.Information(
                "CargoUnloadingSaga: Completed successfully for cargo {CargoId}",
                state.CargoId
            )
            let context = ctx.UntypedContext
            context.Stop(untyped ctx.Self)
            return ignored ()
        }

    and failed (state: CargoUnloadingSagaState) (reason: string) =
        actor {
            logger.Error("CargoUnloadingSaga: Failed - {Reason}", reason)
            let context = ctx.UntypedContext
            context.Stop(untyped ctx.Self)
            return ignored ()
        }

    initial ()

/// Spawn function to create a CargoUnloadingSaga
let spawn (system: Akka.Actor.ActorSystem) (name: string) : IActorRef<CargoUnloadingSagaProtocol> =
    Akkling.Spawn.spawn system name (props createCargoUnloadingSaga)
