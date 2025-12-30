module Command.CargoLoadingSaga

open System
open Akka.Actor
open Akkling
open Akkling.Actors
open Command.VesselActor
open Command.PortActor
open Command.CargoActor
open Domain.EventMetadata
open Serilog

type CargoLoadingSagaState = {
    CargoId: Guid
    VesselId: Guid
    OriginPortId: Guid
    DestinationPortId: Guid
    CargoSpec: Shared.Api.Cargo.CargoSpec
    Step: CargoLoadingStep
}

and CargoLoadingStep =
    | Initial
    | AwaitingCargoReservation
    | AwaitingVesselLoading
    | AwaitingCargoUpdate
    | Completed
    | Failed of reason: string
    | Compensating

type CargoLoadingSagaProtocol =
    | StartLoading of
        cargoId: Guid *
        vesselId: Guid *
        originPortId: Guid *
        destinationPortId: Guid *
        cargoSpec: Shared.Api.Cargo.CargoSpec
    | Timeout

let createCargoLoadingSaga (ctx: Actor<CargoLoadingSagaProtocol>) =
    let logger = Log.ForContext("SagaType", "CargoLoading")

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
            | StartLoading(cargoId, vesselId, originPortId, destinationPortId, cargoSpec) ->
                logger.Information(
                    "CargoLoadingSaga: Starting cargo {CargoId} loading onto vessel {VesselId} at port {PortId} to destination {DestinationPortId}",
                    cargoId,
                    vesselId,
                    originPortId,
                    destinationPortId
                )

                let state = {
                    CargoId = cargoId
                    VesselId = vesselId
                    OriginPortId = originPortId
                    DestinationPortId = destinationPortId
                    CargoSpec = cargoSpec
                    Step = AwaitingCargoReservation
                }

                return! awaitingCargoReservation state

            | _ -> return! initial ()
        }

    and awaitingCargoReservation (state: CargoLoadingSagaState) =
        actor {
            // Step 0a: Reserve cargo in CargoAggregate (atomic, prevents race conditions)
            let context = ctx.UntypedContext
            let metadata = createInitialMetadata (Some "CargoLoadingSaga")

            // Reservation expires in 5 minutes
            let expiresAt = DateTimeOffset.UtcNow.AddMinutes(5.0)

            let reserveCommand =
                Domain.CargoAggregate.ReserveForVessel {
                    AggregateId = state.CargoId
                    VesselId = state.VesselId
                    PortId = state.OriginPortId
                    ExpiresAt = expiresAt
                    Metadata = metadata
                }

            let cargoResponse =
                sendCommandToCargo state.CargoId reserveCommand context
                |> Async.RunSynchronously

            match cargoResponse with
            | CargoCommandSuccess _ ->
                logger.Information("CargoLoadingSaga: Cargo reserved for vessel")

                return! awaitingVesselLoading { state with Step = AwaitingVesselLoading }
            | CargoCommandFailure error ->
                logger.Warning("CargoLoadingSaga: Cargo reservation failed - {Error}", error)
                return! failed state $"Cargo reservation failed: {error}"
        }

    and awaitingVesselLoading (state: CargoLoadingSagaState) =
        actor {
            // Step 2: Vessel loads cargo (validates capacity)
            let context = ctx.UntypedContext
            let metadata = createInitialMetadata (Some "CargoLoadingSaga")

            let loadCommand =
                Domain.VesselAggregate.LoadCargo {
                    AggregateId = state.VesselId
                    CargoId = state.CargoId
                    CargoSpec = state.CargoSpec
                    OriginPortId = state.OriginPortId
                    DestinationPortId = state.DestinationPortId
                    Metadata = metadata
                }

            let vesselResponse =
                sendCommandToVessel state.VesselId loadCommand context |> Async.RunSynchronously

            match vesselResponse with
            | VesselCommandSuccess _ ->
                logger.Information("CargoLoadingSaga: Vessel loaded cargo")
                return! awaitingCargoUpdate { state with Step = AwaitingCargoUpdate }
            | VesselCommandFailure error ->
                logger.Warning("CargoLoadingSaga: Vessel loading failed - {Error}", error)
                return! compensating state $"Vessel loading failed: {error}"
        }

    and awaitingCargoUpdate (state: CargoLoadingSagaState) =
        actor {
            // Step 3: Update cargo state to LoadedOnVessel
            let context = ctx.UntypedContext
            let metadata = createInitialMetadata (Some "CargoLoadingSaga")

            let loadOntoVesselCommand =
                Domain.CargoAggregate.LoadOntoVessel {
                    AggregateId = state.CargoId
                    VesselId = state.VesselId
                    Metadata = metadata
                }

            let cargoResponse =
                sendCommandToCargo state.CargoId loadOntoVesselCommand context
                |> Async.RunSynchronously

            match cargoResponse with
            | CargoCommandSuccess _ ->
                logger.Information(
                    "CargoLoadingSaga: Cargo state updated successfully - SAGA COMPLETED"
                )
                return! completed state
            | CargoCommandFailure error ->
                logger.Warning("CargoLoadingSaga: Cargo update failed - {Error}", error)
                return! compensating state $"Cargo update failed: {error}"
        }

    and compensating (state: CargoLoadingSagaState) (reason: string) =
        actor {
            logger.Warning("CargoLoadingSaga: Compensating due to: {Reason}", reason)

            try
                let context = ctx.UntypedContext
                let metadata = createInitialMetadata (Some "CargoLoadingSaga.Compensation")

                // Cancel cargo reservation if we got that far
                if state.Step >= AwaitingCargoReservation then
                    let cancelCommand =
                        Domain.CargoAggregate.CancelReservation {
                            AggregateId = state.CargoId
                            Reason = reason
                            Metadata = metadata
                        }

                    let cargoCancelResult =
                        sendCommandToCargo state.CargoId cancelCommand context
                        |> Async.RunSynchronously

                    match cargoCancelResult with
                    | CargoCommandSuccess _ ->
                        logger.Information("CargoLoadingSaga: Canceled cargo reservation")
                    | CargoCommandFailure error ->
                        logger.Warning(
                            "CargoLoadingSaga: Failed to cancel cargo reservation - {Error}",
                            error
                        )

                // Unload from vessel if it was loaded
                if state.Step >= AwaitingCargoUpdate then
                    let unloadCommand =
                        Domain.VesselAggregate.UnloadCargo {
                            AggregateId = state.VesselId
                            CargoId = state.CargoId
                            Metadata = metadata
                        }

                    let r =
                        sendCommandToVessel state.VesselId unloadCommand context
                        |> Async.RunSynchronously
                    ()

                logger.Information("CargoLoadingSaga: Compensation completed")
                ()
            with ex ->
                logger.Error(ex, "CargoLoadingSaga: Compensation failed (non-critical)")

            return! failed state reason
        }

    and completed (state: CargoLoadingSagaState) =
        actor {
            logger.Information(
                "CargoLoadingSaga: Completed successfully for cargo {CargoId}",
                state.CargoId
            )
            let context = ctx.UntypedContext
            context.Stop(untyped ctx.Self)
            return ignored ()
        }

    and failed (state: CargoLoadingSagaState) (reason: string) =
        actor {
            logger.Error("CargoLoadingSaga: Failed - {Reason}", reason)
            let context = ctx.UntypedContext
            context.Stop(untyped ctx.Self)
            return ignored ()
        }

    initial ()

/// Spawn function to create a CargoLoadingSaga
let spawn (system: Akka.Actor.ActorSystem) (name: string) : IActorRef<CargoLoadingSagaProtocol> =
    Akkling.Spawn.spawn system name (props createCargoLoadingSaga)
