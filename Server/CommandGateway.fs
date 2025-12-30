module CommandGateway

open System
open System.Threading.Tasks
open Akka.Actor
open Akkling
open Command
open FsToolkit.ErrorHandling
open Marten
open Domain.VesselAggregate
open Domain.PortAggregate
open Domain.CargoAggregate
open Serilog
open Microsoft.FSharp.Reflection

type CommandGateway(actorSystem: ActorSystem, documentStore: IDocumentStore) =
    let logger = Log.ForContext<CommandGateway>()
    let commandTimeout = TimeSpan.FromSeconds 30.0

    let getOrCreateVesselActor (vesselId: Guid) : IActorRef<VesselActor.VesselProtocol> =
        let actorName = ActorPaths.vesselActorName vesselId
        let actorPath = actorSystem.ActorSelection(ActorPaths.vesselActorPath vesselId)

        try
            let actor = actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result |> typed
            logger.Debug("Found existing VesselActor for {VesselId}", vesselId)
            actor
        with _ ->
            logger.Information("Creating new VesselActor for {VesselId}", vesselId)
            VesselActor.spawn actorSystem actorName vesselId documentStore

    let getOrCreatePortActor (portId: Guid) : IActorRef<PortActor.PortProtocol> =
        let actorName = ActorPaths.portActorName portId
        let actorPath = actorSystem.ActorSelection(ActorPaths.portActorPath portId)

        try
            let actor = actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result |> typed
            logger.Debug("Found existing PortActor for {PortId}", portId)
            actor
        with _ ->
            logger.Information("Creating new PortActor for {PortId}", portId)
            PortActor.spawn actorSystem actorName portId documentStore

    let getOrCreateCargoActor (cargoId: Guid) : IActorRef<CargoActor.CargoProtocol> =
        let actorName = ActorPaths.cargoActorName cargoId
        let actorPath = actorSystem.ActorSelection(ActorPaths.cargoActorPath cargoId)

        try
            let actor = actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result |> typed
            logger.Debug("Found existing CargoActor for {CargoId}", cargoId)
            actor
        with _ ->
            logger.Information("Creating new CargoActor for {CargoId}", cargoId)
            CargoActor.spawn actorSystem actorName cargoId documentStore

    let createDockingSaga (vesselId: Guid) (portId: Guid) =
        let sagaId = Guid.NewGuid()
        let sagaName = ActorPaths.dockingSagaName sagaId

        logger.Information(
            "Creating docking saga {SagaId} for vessel {VesselId} at port {PortId}",
            sagaId,
            vesselId,
            portId
        )

        let sagaActor = DockingSaga.spawn actorSystem sagaName sagaId

        (sagaId, sagaActor)

    member _.SendVesselCommand
        (vesselId: Guid, command: VesselCommand)
        : Async<Result<int, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            try
                let commandName =
                    FSharpValue.GetUnionFields(command, command.GetType()) |> fst |> _.Name

                logger.Information("Sending vessel command: {Command} to {VesselId}", commandName, vesselId)

                let vesselActor = getOrCreateVesselActor vesselId
                let message = VesselActor.VesselProtocol.ExecuteCommand(command)

                let! response = vesselActor.Ask<VesselActor.VesselCommandResponse>(message, Some commandTimeout)

                match response with
                | VesselActor.VesselCommandSuccess eventCount -> return eventCount
                | VesselActor.VesselCommandFailure error ->
                    logger.Warning("Vessel command failed: {Error}", error)
                    return! Error(error)
            with
            | :? TaskCanceledException ->
                logger.Error("Vessel command to {VesselId} timed out", vesselId)
                return! Error(Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("responding", "timed out"))
            | ex ->
                logger.Error(ex, "Vessel command to {VesselId} failed with exception", vesselId)
                return! Error(Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("operational", ex.Message))
        }

    member this.RegisterVessel
        (
            id: Guid,
            name: string,
            mmsi: int,
            imo: int option,
            flag: string,
            position: Shared.Api.Shared.LatLong,
            length: float option,
            beam: float option,
            draught: float option,
            vesselType: Shared.Api.Vessel.VesselType,
            crewSize: int,
            actor: string option
        ) : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                RegisterVessel
                    { Id = id
                      Name = name
                      Mmsi = mmsi
                      Imo = imo
                      Flag = flag
                      Position = position
                      Length = length
                      Beam = beam
                      Draught = draught
                      VesselType = vesselType
                      CrewSize = crewSize
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(id, command)
            return id
        }

    member this.UpdateVesselPosition
        (vesselId: Guid, position: Shared.Api.Shared.LatLong, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                UpdatePosition
                    { AggregateId = vesselId
                      Position = position
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(vesselId, command)
            return vesselId
        }

    member private this.DepartFromPort
        (vesselId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                DepartFromPort
                    { AggregateId = vesselId
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(vesselId, command)
            return vesselId
        }

    member this.UpdateOperationalStatus
        (vesselId: Guid, status: Shared.Api.Vessel.OperationalStatus, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                UpdateOperationalStatus
                    { AggregateId = vesselId
                      Status = status
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(vesselId, command)
            return vesselId
        }

    member this.DecommissionVessel
        (vesselId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                DecommissionVessel
                    { AggregateId = vesselId
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(vesselId, command)
            return vesselId
        }

    member this.AdvanceRouteWaypoint
        (vesselId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                AdvanceRouteWaypoint
                    { AggregateId = vesselId
                      Metadata = metadata }

            let! _ = this.SendVesselCommand(vesselId, command)
            return vesselId
        }

    member _.SendPortCommand
        (portId: Guid, command: PortCommand)
        : Async<Result<int, Shared.Api.Port.PortCommandErrors>> =
        asyncResult {
            try
                let commandName =
                    FSharpValue.GetUnionFields(command, command.GetType()) |> fst |> _.Name

                logger.Information("Sending port command: {Command} to {PortId}", commandName, portId)

                let portActor = getOrCreatePortActor portId
                let message = PortActor.PortProtocol.ExecuteCommand(command)

                let! response = portActor.Ask<PortActor.PortCommandResponse>(message, Some commandTimeout)

                match response with
                | PortActor.PortCommandSuccess eventCount -> return eventCount
                | PortActor.PortCommandFailure error ->
                    logger.Warning("Port command failed: {Error}", error)
                    return! Error(error)
            with
            | :? TaskCanceledException ->
                logger.Error("Port command to {PortId} timed out", portId)
                return! Error(Shared.Api.Port.PortCommandErrors.CommandFailed("Command timed out"))
            | ex ->
                logger.Error(ex, "Port command to {PortId} failed with exception", portId)
                return! Error(Shared.Api.Port.PortCommandErrors.CommandFailed(ex.Message))
        }

    member this.RegisterPort
        (
            id: Guid,
            name: string,
            locode: string option,
            country: string,
            position: Shared.Api.Shared.LatLong,
            timezone: string option,
            maxDocks: int,
            actor: string option
        ) : Async<Result<Guid, Shared.Api.Port.PortCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                RegisterPort
                    { Id = id
                      Name = name
                      Locode = locode
                      Country = country
                      Position = position
                      Timezone = timezone
                      MaxDocks = maxDocks
                      Metadata = metadata }

            let! _ = this.SendPortCommand(id, command)
            return id
        }

    member private this.UndockVessel
        (portId: Guid, vesselId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Port.PortCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                UndockVessel
                    { AggregateId = portId
                      VesselId = vesselId
                      Metadata = metadata }

            let! _ = this.SendPortCommand(portId, command)
            return portId
        }

    member this.OpenPort(portId: Guid, actor: string option) : Async<Result<Guid, Shared.Api.Port.PortCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                OpenPort
                    { AggregateId = portId
                      Metadata = metadata }

            let! _ = this.SendPortCommand(portId, command)
            return portId
        }

    member this.ClosePort(portId: Guid, actor: string option) : Async<Result<Guid, Shared.Api.Port.PortCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                ClosePort
                    { AggregateId = portId
                      Metadata = metadata }

            let! _ = this.SendPortCommand(portId, command)
            return portId
        }

    member _.StartDockingSaga
        (vesselId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            try
                logger.Information("Starting docking saga for vessel {VesselId}", vesselId)

                let vesselActor = getOrCreateVesselActor vesselId

                // First, get vessel state to extract destination port from route
                let! vesselStateResponse =
                    vesselActor.Ask<VesselActor.VesselStateResponse>(
                        VesselActor.VesselProtocol.GetState,
                        Some commandTimeout
                    )

                match vesselStateResponse with
                | VesselActor.VesselNotFound ->
                    logger.Warning("Cannot start docking saga - Vessel {VesselId} has not been registered", vesselId)
                    return! Error Shared.Api.Vessel.VesselCommandErrors.VesselNotFound

                | VesselActor.VesselExists vesselState ->
                    // Extract destination port from route
                    match vesselState.State with
                    | Shared.Api.Vessel.OperationalStatus.InRoute route ->
                        let portId = route.DestinationPortId
                        logger.Information("Vessel '{VesselName}' in route to port {PortId}", vesselState.Name, portId)

                        // Validate vessel is not already docked
                        do!
                            vesselState.State.IsDocked
                            |> Result.requireFalse Shared.Api.Vessel.VesselCommandErrors.VesselIsAlreadyArrived

                        match vesselState.State with
                        | Shared.Api.Vessel.InRoute route when route.CurrentWaypointIndex + 1 < route.Waypoints.Length ->
                            return! Error Shared.Api.Vessel.VesselCommandErrors.RouteNotFinished
                        | _ -> ()
                        // Get port actor and validate port state
                        let portActor = getOrCreatePortActor portId

                        logger.Information(
                            "Actors initialized - Vessel: {VesselPath}, Port: {PortPath}",
                            vesselActor.Path.ToString(),
                            portActor.Path.ToString()
                        )

                        let! portStateResponse =
                            portActor.Ask<PortActor.PortStateResponse>(
                                PortActor.PortProtocol.GetState,
                                Some commandTimeout
                            )

                        match portStateResponse with
                        | PortActor.PortNotFound ->
                            logger.Warning("Cannot start docking saga - Port {PortId} has not been registered", portId)
                            return! Error Shared.Api.Vessel.VesselCommandErrors.PortNotFound

                        | PortActor.PortExists portState ->
                            // Validate port can accept reservations
                            if portState.Status <> Shared.Api.Port.PortStatus.Open then
                                logger.Warning(
                                    "Cannot start docking saga - Port {PortId} is {Status}",
                                    portId,
                                    portState.Status
                                )

                                return!
                                    Error(
                                        Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                                            "Open port",
                                            $"Port '{portState.Name}' is currently {portState.Status}"
                                        )
                                    )
                            elif portState.AvailableDocks <= 0 then
                                logger.Warning(
                                    "Cannot start docking saga - Port {PortId} has no available docks",
                                    portId
                                )

                                return! Error Shared.Api.Vessel.VesselCommandErrors.NoDockingAvailableAtPort
                            else
                                logger.Information(
                                    "Starting saga - Vessel '{VesselName}' docking at Port '{PortName}'",
                                    vesselState.Name,
                                    portState.Name
                                )

                                let _sagaId, sagaActor = createDockingSaga vesselId portId

                                let metadata = Domain.EventMetadata.createInitialMetadata actor

                                let startMessage =
                                    DockingSaga.DockingSagaProtocol.StartDocking(vesselId, portId, metadata)

                                let! response =
                                    sagaActor.Ask<DockingSaga.DockingSagaResponse>(startMessage, Some commandTimeout)

                                match response with
                                | DockingSaga.DockingSagaResponse.DockingSagaCompleted completedSagaId ->
                                    logger.Information("Docking saga {SagaId} completed successfully", completedSagaId)
                                    return! Ok completedSagaId
                                | DockingSaga.DockingSagaResponse.DockingSagaFailed(failedSagaId, error) ->
                                    logger.Error("Docking saga {SagaId} failed: {Error}", failedSagaId, error)

                                    return!
                                        Error(
                                            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                                                "saga completion",
                                                error
                                            )
                                        )

                    | _ ->
                        logger.Warning(
                            "Cannot start docking saga - Vessel {VesselId} is not in route (current state: {State})",
                            vesselId,
                            vesselState.State
                        )

                        return!
                            Error(
                                Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                                    "in route",
                                    $"vessel is {vesselState.State}"
                                )
                            )

            with
            | :? TaskCanceledException ->
                logger.Error("Failed to start docking saga for vessel {VesselId} - timed out", vesselId)

                return!
                    Error(
                        Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("responding", "Saga start timed out")
                    )
            | ex ->
                logger.Error(ex, "Failed to start docking saga for vessel {VesselId}", vesselId)

                return! Error(Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("operational", ex.Message))
        }

    member this.StartUndockingSaga
        (vesselId: Guid, portId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            logger.Information("Starting undocking for vessel {VesselId} from port {PortId}", vesselId, portId)

            let vesselActor = getOrCreateVesselActor vesselId
            let portActor = getOrCreatePortActor portId

            let! vesselState =
                vesselActor.Ask<VesselActor.VesselStateResponse>(
                    VesselActor.VesselProtocol.GetState,
                    Some commandTimeout
                )
            //|> Async.AwaitTask

            let! portState =
                portActor.Ask<PortActor.PortStateResponse>(PortActor.PortProtocol.GetState, Some commandTimeout)

            match vesselState, portState with
            | VesselActor.VesselExists v, PortActor.PortExists p ->
                do!
                    v.CurrentPortId <> Some portId
                    |> Result.requireFalse (
                        Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                            "docked at port",
                            $"vessel not at port {portId}"
                        )
                    )

                do!
                    p.DockedVessels.Contains(vesselId)
                    |> Result.requireTrue (
                        Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                            "docked",
                            "vessel not in port's docked list"
                        )
                    )
            | VesselActor.VesselNotFound, _ -> return! Error Shared.Api.Vessel.VesselCommandErrors.VesselNotFound
            | _, PortActor.PortNotFound ->
                return! Error(Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("port exists", "port not found"))

            let! _ =
                this.DepartFromPort(vesselId, actor)
                |> AsyncResult.orElseWith (fun error ->
                    match error with
                    | Shared.Api.Vessel.VesselCommandErrors.VesselAlreadyDeparted ->
                        logger.Warning("Vessel {VesselId} already departed, continuing", vesselId)
                        AsyncResult.ok vesselId // weird i have to map these explicitly o.O
                    | _ -> AsyncResult.error error)

            let! undockResult =
                this.UndockVessel(portId, vesselId, actor)
                |> AsyncResult.mapError (fun portError ->
                    logger.Error(
                        "CRITICAL: Vessel {VesselId} departed but port {PortId} undock failed: {Error}. Manual intervention may be required.",
                        vesselId,
                        portId,
                        portError
                    )

                    Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(
                        "synchronized",
                        $"Vessel departed but port undock failed: {portError}"
                    ))

            logger.Information("Successfully undocked vessel {VesselId} from port {PortId}", vesselId, portId)
            return undockResult
        }

    member _.SendCargoCommand
        (cargoId: Guid, command: CargoCommand)
        : Async<Result<int, Shared.Api.Cargo.CargoCommandErrors>> =
        asyncResult {
            try
                let commandName =
                    FSharpValue.GetUnionFields(command, command.GetType()) |> fst |> _.Name

                logger.Information("Sending cargo command: {Command} to {CargoId}", commandName, cargoId)

                let cargoActor = getOrCreateCargoActor cargoId
                let message = CargoActor.CargoProtocol.ExecuteCommand(command)

                let! response = cargoActor.Ask<CargoActor.CargoCommandResponse>(message, Some commandTimeout)

                match response with
                | CargoActor.CargoCommandSuccess eventCount -> return eventCount
                | CargoActor.CargoCommandFailure error ->
                    logger.Warning("Cargo command failed: {Error}", error)

                    return! Error(error)
            with
            | :? TaskCanceledException ->
                logger.Error("Cargo command to {CargoId} timed out", cargoId)
                return! Error(Shared.Api.Cargo.CargoCommandErrors.PersistenceError("Command timed out"))
            | ex ->
                logger.Error(ex, "Cargo command to {CargoId} failed with exception", cargoId)
                return! Error(Shared.Api.Cargo.CargoCommandErrors.PersistenceError(ex.Message))
        }

    member this.CreateCargo
        (id: Guid, spec: Shared.Api.Cargo.CargoSpec, originPortId: Guid, destinationPortId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Cargo.CargoCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                CreateCargo
                    { Id = id
                      Spec = spec
                      OriginPortId = originPortId
                      DestinationPortId = destinationPortId
                      Metadata = metadata }

            let! _ = this.SendCargoCommand(id, command)
            return id
        }

    member this.LoadCargoOntoVessel
        (cargoId: Guid, vesselId: Guid, portId: Guid, cargoSpec: Shared.Api.Cargo.CargoSpec, actor: string option)
        : Async<Result<Guid, Shared.Api.Cargo.CargoCommandErrors>> =
        asyncResult {
            try
                logger.Information(
                    "Starting cargo loading saga for cargo {CargoId} onto vessel {VesselId}",
                    cargoId,
                    vesselId
                )

                let portActor = getOrCreatePortActor portId
                let vesselActor = getOrCreateVesselActor vesselId
                let cargoActor = getOrCreateCargoActor cargoId

                // First, get vessel state to extract destination port from route
                let! vesselStateResponse =
                    vesselActor.Ask<VesselActor.VesselStateResponse>(
                        VesselActor.VesselProtocol.GetState,
                        Some commandTimeout
                    )

                let! portStateResponse =
                    portActor.Ask<PortActor.PortStateResponse>(PortActor.PortProtocol.GetState, Some commandTimeout)

                let! cargoStateResponse =
                    cargoActor.Ask<CargoActor.CargoStateResponse>(
                        CargoActor.CargoProtocol.GetState,
                        Some commandTimeout
                    )

                // Validate actors are alive before reaching them. Will start them if not alive
                match vesselStateResponse, portStateResponse, cargoStateResponse with
                | VesselActor.VesselExists _vesselState,
                  PortActor.PortExists _portState,
                  CargoActor.CargoExists cargoState ->
                    let sagaId = Guid.NewGuid()
                    let sagaName = ActorPaths.cargoLoadingSagaName sagaId

                    let sagaActor = CargoLoadingSaga.spawn actorSystem sagaName

                    let startMessage =
                        CargoLoadingSaga.StartLoading(
                            cargoId,
                            vesselId,
                            portId,
                            cargoState.DestinationPortId,
                            cargoSpec
                        )

                    sagaActor <! startMessage

                    logger.Information(
                        "Cargo loading saga {SagaId} started for cargo from {OriginPort} to {DestinationPort}",
                        sagaId,
                        portId,
                        cargoState.DestinationPortId
                    )

                    return sagaId
                | _ -> return! Error Shared.Api.Cargo.CargoNotFound

            with ex ->
                logger.Error(ex, "Failed to start cargo loading saga")
                return! Error(Shared.Api.Cargo.CargoCommandErrors.PersistenceError(ex.Message))
        }

    member this.UnloadCargoFromVessel
        (cargoId: Guid, vesselId: Guid, portId: Guid, isDestinationPort: bool, actor: string option)
        : Async<Result<Guid, Shared.Api.Cargo.CargoCommandErrors>> =
        asyncResult {
            try
                logger.Information(
                    "Starting cargo unloading saga for cargo {CargoId} from vessel {VesselId} at port {PortId}",
                    cargoId,
                    vesselId,
                    portId
                )

                let sagaId = Guid.NewGuid()
                let sagaName = ActorPaths.cargoUnloadingSagaName sagaId

                let sagaActor = CargoUnloadingSaga.spawn actorSystem sagaName

                let startMessage =
                    CargoUnloadingSaga.StartUnloading(cargoId, vesselId, portId, isDestinationPort)

                sagaActor <! startMessage

                logger.Information("Cargo unloading saga {SagaId} started", sagaId)
                return sagaId

            with ex ->
                logger.Error(ex, "Failed to start cargo unloading saga")
                return! Error(Shared.Api.Cargo.CargoCommandErrors.PersistenceError(ex.Message))
        }

    member this.CancelCargo
        (cargoId: Guid, actor: string option)
        : Async<Result<Guid, Shared.Api.Cargo.CargoCommandErrors>> =
        asyncResult {
            let metadata = Domain.EventMetadata.createInitialMetadata actor

            let command =
                Domain.CargoAggregate.CargoCommand.CancelCargo
                    { AggregateId = cargoId
                      Reason = "Cancel reason not given :)"
                      Metadata = metadata }

            let! _ = this.SendCargoCommand(cargoId, command)
            return cargoId
        }

    member this.GetVesselState(vesselId: Guid) : Async<Result<Domain.VesselAggregate.VesselState option, string>> =
        async {
            try
                let vesselActor = getOrCreateVesselActor vesselId

                let! response =
                    vesselActor.Ask<VesselActor.VesselStateResponse>(
                        VesselActor.VesselProtocol.GetState,
                        Some commandTimeout
                    )

                match response with
                | VesselActor.VesselExists state -> return Ok(Some state)
                | VesselActor.VesselNotFound -> return Ok None
            with ex ->
                logger.Error(ex, "Failed to get vessel state for {VesselId}", vesselId)
                return Error(ex.Message)
        }
