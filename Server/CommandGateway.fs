module CommandGateway

open System
open System.Threading.Tasks
open Akka.Actor
open Command
open FsToolkit.ErrorHandling
open Marten
open Domain.VesselAggregate
open Domain.PortAggregate
open Serilog
open Microsoft.FSharp.Reflection
open Shared.Api.Vessel

type CommandGateway(actorSystem: ActorSystem, documentStore: IDocumentStore) =
    let logger = Log.ForContext<CommandGateway>()
    let commandTimeout = TimeSpan.FromSeconds 30.0

    let getOrCreateVesselActor (vesselId: Guid) =
        let actorName = sprintf "vessel-%s" (vesselId.ToString())
        let actorPath = actorSystem.ActorSelection($"/user/{actorName}")

        try
            actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result
        with _ ->
            actorSystem.ActorOf(VesselActor.props vesselId documentStore, actorName)

    let getOrCreatePortActor (portId: Guid) =
        let actorName = sprintf "port-%s" (portId.ToString())
        let actorPath = actorSystem.ActorSelection($"/user/{actorName}")

        try
            actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result
        with _ ->
            actorSystem.ActorOf(PortActor.props portId documentStore, actorName)

    let getSagaCoordinator () =
        let actorName = "saga-coordinator"
        let actorPath = actorSystem.ActorSelection($"/user/{actorName}")

        // Try resolve or create the saga-coordinator
        try
            actorPath.ResolveOne(TimeSpan.FromSeconds 1.0).Result
        with _ ->
            actorSystem.ActorOf(SagaCoordinator.props documentStore, actorName)

    // TODO: Decide if this is fine, or we should just use shared errors throughout solution due to remoting.
    let mapVesselError (error: Domain.VesselErrors.VesselError) : Shared.Api.Vessel.VesselCommandErrors =
        match error with
        | Domain.VesselErrors.VesselNotFound -> Shared.Api.Vessel.VesselCommandErrors.VesselNotFound
        | Domain.VesselErrors.VesselAlreadyExists -> Shared.Api.Vessel.VesselCommandErrors.VesselIdAlreadyExists
        | Domain.VesselErrors.VesselAlreadyDecommissioned ->
            Shared.Api.Vessel.VesselCommandErrors.VesselAlreadyDecommissioned
        | Domain.VesselErrors.VesselAlreadyDeparted -> Shared.Api.Vessel.VesselCommandErrors.VesselAlreadyDeparted
        | Domain.VesselErrors.VesselAlreadyArrived -> Shared.Api.Vessel.VesselCommandErrors.VesselIsAlreadyArrived
        | Domain.VesselErrors.InvalidStateTransition(expected, actual) ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState(expected, actual)
        | Domain.VesselErrors.PortNotAvailable -> Shared.Api.Vessel.VesselCommandErrors.PortNotFound
        | Domain.VesselErrors.NoDockingSpace -> Shared.Api.Vessel.VesselCommandErrors.NoDockingAvailableAtPort
        | Domain.VesselErrors.CargoNotFound -> Shared.Api.Vessel.VesselCommandErrors.CargoNotFound
        | Domain.VesselErrors.NotInRoute ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("in route", "not in route")
        | Domain.VesselErrors.NoMoreWaypoints ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("more waypoints", "no more waypoints")
        | Domain.VesselErrors.NoActiveRoute ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("active route", "no active route")
        | Domain.VesselErrors.RouteCalculationFailed msg ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("route calculated", msg)
        | Domain.VesselErrors.DestinationPortNotFound -> Shared.Api.Vessel.VesselCommandErrors.PortNotFound
        | Domain.VesselErrors.ValidationError msg ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("valid", msg)
        | Domain.VesselErrors.PersistenceError msg ->
            Shared.Api.Vessel.VesselCommandErrors.InvalidVesselState("operational", msg)

    let mapPortError (error: Domain.PortErrors.PortError) : Shared.Api.Port.PortCommandErrors =
        match error with
        | Domain.PortErrors.PortNotFound -> Shared.Api.Port.PortCommandErrors.PortNotFound
        | Domain.PortErrors.PortAlreadyExists -> Shared.Api.Port.PortCommandErrors.PortAlreadyRegistered
        | Domain.PortErrors.PortAlreadyOpen -> Shared.Api.Port.PortCommandErrors.PortAlreadyOpen
        | Domain.PortErrors.PortAlreadyClosed -> Shared.Api.Port.PortCommandErrors.PortAlreadyClosed
        | Domain.PortErrors.NoDockingSpaceAvailable -> Shared.Api.Port.PortCommandErrors.NoDockingSpaceAvailable
        | Domain.PortErrors.VesselNotDocked -> Shared.Api.Port.PortCommandErrors.VesselNotDockedAtPort
        | Domain.PortErrors.VesselAlreadyDocked ->
            Shared.Api.Port.PortCommandErrors.InvalidPortState("vessel not docked", "vessel already docked")
        | Domain.PortErrors.ReservationNotFound -> Shared.Api.Port.PortCommandErrors.ReservationNotFound
        | Domain.PortErrors.ReservationAlreadyExists ->
            Shared.Api.Port.PortCommandErrors.InvalidPortState("new reservation", "reservation already exists")
        | Domain.PortErrors.InvalidStateTransition(expected, actual) ->
            Shared.Api.Port.PortCommandErrors.InvalidPortState(expected, actual)
        | Domain.PortErrors.ValidationError msg -> Shared.Api.Port.PortCommandErrors.CommandFailed(msg)
        | Domain.PortErrors.PersistenceError msg -> Shared.Api.Port.PortCommandErrors.CommandFailed(msg)

    member _.SendVesselCommand
        (vesselId: Guid, command: VesselCommand)
        : Async<Result<int, Shared.Api.Vessel.VesselCommandErrors>> =
        asyncResult {
            try
                let commandName =
                    FSharpValue.GetUnionFields(command, command.GetType()) |> fst |> _.Name

                logger.Information("Sending vessel command to {VesselId}: {Command}", vesselId, commandName)

                let vesselActor = getOrCreateVesselActor vesselId
                let message = VesselActor.VesselActorMessage.ExecuteCommand(command)

                let! response =
                    vesselActor.Ask<VesselActor.VesselCommandResponse>(message, commandTimeout)
                    |> Async.AwaitTask

                match response with
                | VesselActor.VesselCommandSuccess eventCount ->
                    logger.Information("Vessel command succeeded with {EventCount} events", eventCount)
                    return eventCount
                | VesselActor.VesselCommandFailure error ->
                    logger.Warning("Vessel command failed: {Error}", error)
                    return! Error(mapVesselError error)
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
            position: Shared.Api.Vessel.VesselPosition,
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
        (vesselId: Guid, position: Shared.Api.Vessel.VesselPosition, actor: string option)
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
                let portActor = getOrCreatePortActor portId
                let message = PortActor.PortActorMessage.ExecuteCommand(command)

                let! response =
                    portActor.Ask<PortActor.PortCommandResponse>(message, commandTimeout)
                    |> Async.AwaitTask

                match response with
                | PortActor.PortCommandSuccess eventCount ->
                    logger.Information("Port command succeeded with {EventCount} events", eventCount)
                    return eventCount
                | PortActor.PortCommandFailure error ->
                    logger.Warning("Port command failed: {Error}", error)
                    return! Error(mapPortError error)
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
            latitude: float,
            longitude: float,
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
                      Latitude = latitude
                      Longitude = longitude
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
                        VesselActor.VesselActorMessage.GetState,
                        commandTimeout
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
                        | InRoute route when route.CurrentWaypointIndex < route.Waypoints.Length ->
                            return! Error Shared.Api.Vessel.VesselCommandErrors.NoDockingAvailableAtPort
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
                                PortActor.PortActorMessage.GetState,
                                commandTimeout
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

                                let metadata = Domain.EventMetadata.createInitialMetadata actor
                                let sagaCoordinator = getSagaCoordinator ()

                                let message =
                                    SagaCoordinator.SagaCoordinatorMessage.StartDockingSaga(vesselId, portId, metadata)

                                let! sagaId = sagaCoordinator.Ask<Guid>(message, commandTimeout)
                                logger.Information("Docking saga {SagaId} started successfully", sagaId)
                                return! Ok sagaId

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
                vesselActor.Ask<VesselActor.VesselStateResponse>(VesselActor.GetState, commandTimeout)
                |> Async.AwaitTask

            let! portState =
                portActor.Ask<PortActor.PortStateResponse>(PortActor.GetState, commandTimeout)
                |> Async.AwaitTask

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
