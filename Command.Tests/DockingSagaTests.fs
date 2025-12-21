module Command.Tests.DockingSagaTests

open System
open Akka.Actor
open Xunit
open Akka.TestKit.Xunit2
open Command.Tests.MartenFixture
open Shared.Api.Vessel
open Shared.Api.Shared

type DockingSagaTests(martenFixture: MartenFixture, output: Xunit.Abstractions.ITestOutputHelper) =
    inherit TestKit("akkaActorSystemName")
    interface IClassFixture<MartenFixture>

    /// <summary>
    /// Helper for creating a docking saga actor
    /// </summary>
    member private this.CreateDockingSaga(store) =
        let sagaId = Guid.NewGuid()
        let sagaName = ActorPaths.dockingSagaName sagaId
        let sagaActor = Command.DockingSaga.spawn this.Sys sagaName sagaId
        (sagaId, sagaActor)

    /// <summary>
    /// Helper to create and register a vessel, with full response verification
    /// </summary>
    member private this.CreateVessel(store, name, mmsi) =
        let vesselId = Guid.NewGuid()
        let vesselPosition = { Latitude = 59.0; Longitude = 10.0 }

        let vesselActor =
            Command.VesselActor.spawn this.Sys $"vessel-{vesselId}" vesselId store

        let cmd: Domain.VesselAggregate.RegisterVesselCmd =
            { Id = vesselId
              Name = name
              Mmsi = mmsi
              Imo = None
              Flag = "NO"
              Position = vesselPosition
              Length = None
              Beam = None
              Draught = None
              VesselType = VesselType.Fishing
              CrewSize = 10
              Metadata = Domain.EventMetadata.createInitialMetadata (Some "SagaTest") }


        vesselActor.Tell(Command.VesselActor.ExecuteCommand(Domain.VesselAggregate.RegisterVessel cmd), this.TestActor)

        let result =
            this.ExpectMsg<Command.VesselActor.VesselCommandResponse>(TimeSpan.FromSeconds(5.0))

        match result with
        | Command.VesselActor.VesselCommandResponse.VesselCommandSuccess _ -> (vesselId, vesselActor)
        | Command.VesselActor.VesselCommandResponse.VesselCommandFailure err ->
            failwith $"Failed to create vessel: {err}"

    /// <summary>
    /// Helper to create and register a port, with full response verification
    /// </summary>
    member private this.CreatePort(store, name, position, maxDocks) =
        let portId = Guid.NewGuid()

        let portActor = Command.PortActor.spawn this.Sys $"port-{portId}" portId store

        let registerCmd: Domain.PortAggregate.RegisterPortCmd =
            { Id = portId
              Name = name
              Locode = None
              Country = "Norway"
              Position = position
              Timezone = Some "Europe/Oslo"
              MaxDocks = maxDocks
              Metadata = Domain.EventMetadata.createInitialMetadata (Some "SagaTest") }

        portActor.Tell(
            Command.PortActor.PortProtocol.ExecuteCommand(Domain.PortAggregate.PortCommand.RegisterPort registerCmd),
            this.TestActor
        )

        let result =
            this.ExpectMsg<Command.PortActor.PortCommandResponse>(TimeSpan.FromSeconds(5.0))

        match result with
        | Command.PortActor.PortCommandResponse.PortCommandSuccess _ -> (portId, portActor)
        | Command.PortActor.PortCommandResponse.PortCommandFailure err -> failwith $"Failed to create port: {err}"

    /// <summary>
    /// Helper to set vessel in route to port - prerequisite for docking saga
    /// Creates a simple test route directly
    /// </summary>
    member private this.SetVesselInRouteToPort
        (vesselId, vesselActor: Akkling.ActorRefs.IActorRef<Command.VesselActor.VesselProtocol>, portId, portPosition)
        =
        // Create a simple test route with the vessel already at the final waypoint
        let routeInfo: RouteInfo =
            { RouteId = Guid.NewGuid()
              DestinationPortId = portId
              Waypoints = [| portPosition |]
              CurrentWaypointIndex = 0 // At the final (and only) waypoint
              StartedAt = DateTimeOffset.UtcNow
              DestinationCoordinates = portPosition
              StartCoordinates = portPosition }

        // Set vessel to InRoute status
        let statusCmd: Domain.VesselAggregate.UpdateOperationalStatusCmd =
            { AggregateId = vesselId
              Status = OperationalStatus.InRoute routeInfo
              Metadata = Domain.EventMetadata.createInitialMetadata (Some "SagaTest") }

        vesselActor.Tell(
            Command.VesselActor.VesselProtocol.ExecuteCommand(Domain.VesselAggregate.VesselCommand.UpdateOperationalStatus statusCmd),
            this.TestActor
        )

        let result = this.ExpectMsg<Command.VesselActor.VesselCommandResponse>(TimeSpan.FromSeconds(5.0))

        match result with
        | Command.VesselActor.VesselCommandResponse.VesselCommandSuccess _ -> ()
        | Command.VesselActor.VesselCommandResponse.VesselCommandFailure err -> failwith $"Failed to set vessel in route: {err}"

    /// <summary>
    /// Helper to verify saga reached expected state within timeout
    /// </summary>
    member private this.AwaitSagaState(sagaActor: IActorRef, expectedStep, timeout: TimeSpan) =
        this.AwaitAssert(
            (fun () ->
                sagaActor.Tell(Command.DockingSaga.DockingSagaProtocol.GetSagaState this.TestActor, this.TestActor)

                let response =
                    this.ExpectMsg<Command.DockingSaga.DockingSagaStateResponse>(TimeSpan.FromSeconds(1.0))

                match response with
                | Command.DockingSaga.DockingSagaStateResponse.SagaState state ->
                    Assert.Equal(expectedStep, state.CurrentStep)
                | Command.DockingSaga.DockingSagaStateResponse.SagaNotStarted ->
                    failwith "Saga should have been started"),
            timeout
        )

    /// <summary>
    /// Helper to verify vessel is in expected operational state
    /// </summary>
    member private this.VerifyVesselState
        (vesselActor: Akkling.ActorRefs.IActorRef<Command.VesselActor.VesselProtocol>, expectedStatus: OperationalStatus)
        =
        vesselActor.Tell(Command.VesselActor.VesselProtocol.GetState, this.TestActor)
        let response = this.ExpectMsg<Command.VesselActor.VesselStateResponse>(TimeSpan.FromSeconds(5.0))

        match response with
        | Command.VesselActor.VesselStateResponse.VesselExists state -> Assert.Equal(expectedStatus, state.State)
        | Command.VesselActor.VesselStateResponse.VesselNotFound -> failwith "Vessel should exist"

    /// <summary>
    /// Helper to verify port has vessel docked
    /// </summary>
    member private this.VerifyPortHasVesselDocked
        (portActor: Akkling.ActorRefs.IActorRef<Command.PortActor.PortProtocol>, vesselId: Guid)
        =
        portActor.Tell(Command.PortActor.PortProtocol.GetState, this.TestActor)

        let response =
            this.ExpectMsg<Command.PortActor.PortStateResponse>(TimeSpan.FromSeconds(5.0))

        match response with
        | Command.PortActor.PortStateResponse.PortExists state ->
            Assert.Contains(vesselId, state.DockedVessels)
            Assert.Empty(state.PendingReservations)
        | Command.PortActor.PortStateResponse.PortNotFound -> failwith "Port should exist"

    [<Fact>]
    member this.``Saga actor can be created and is alive``() =
        use store = martenFixture.CreateStore()

        // Act - Create DockingSaga actor directly
        let sagaId, sagaActor = this.CreateDockingSaga store

        // Give actor time to initialize
        System.Threading.Thread.Sleep(100)

        // Query state - should be SagaNotStarted
        sagaActor.Tell(Command.DockingSaga.DockingSagaProtocol.GetSagaState this.TestActor, this.TestActor)

        let response =
            this.ExpectMsg<Command.DockingSaga.DockingSagaStateResponse>(TimeSpan.FromSeconds(1.0))

        match response with
        | Command.DockingSaga.DockingSagaStateResponse.SagaNotStarted -> ()
        | _ -> failwith "Saga should not be started yet"

    [<Fact>]
    member this.``DockingSaga should complete successful docking workflow``() =
        // Arrange
        use store = martenFixture.CreateStore()

        // Create vessel and port
        let vesselId, vesselActor = this.CreateVessel(store, "Test Vessel", 123456789)

        let portPosition = { Latitude = 60.0; Longitude = 11.0 }
        let portId, portActor = this.CreatePort(store, "Test Port", portPosition, 5)

        this.VerifyVesselState(vesselActor, OperationalStatus.AtSea)
        // Set vessel in route to port (prerequisite)
        this.SetVesselInRouteToPort(vesselId, vesselActor, portId, portPosition)

        // Create and start saga
        let sagaId, sagaActor = this.CreateDockingSaga store
        let metadata = Domain.EventMetadata.createInitialMetadata None

        let message =
            Command.DockingSaga.DockingSagaProtocol.StartDocking(vesselId, portId, metadata)

        sagaActor.Tell(message, this.TestActor)

        // Expect saga completion response
        let response =
            this.ExpectMsg<Command.DockingSaga.DockingSagaResponse>(TimeSpan.FromSeconds 10.0)

        match response with
        | Command.DockingSaga.DockingSagaResponse.DockingSagaCompleted completedSagaId ->
            Assert.Equal(sagaId, completedSagaId)
        | Command.DockingSaga.DockingSagaResponse.DockingSagaFailed(_, error) ->
            failwith $"Saga should have completed but failed: {error}"

        // Verify final state - vessel should be docked
        this.VerifyVesselState(vesselActor, OperationalStatus.Docked portId)

        // Verify final state - port should have vessel docked
        this.VerifyPortHasVesselDocked(portActor, vesselId)

    [<Fact>]
    member this.``DockingSaga should fail when port has no available docks``() =
        use store = martenFixture.CreateStore()

        // First vessel
        let vesselId, vesselActor = this.CreateVessel(store, "Test Vessel", 123456789)
        this.VerifyVesselState(vesselActor, OperationalStatus.AtSea)
        // Second vessel
        let vesselId2, vesselActor2 = this.CreateVessel(store, "Test Vessel2", 123456790)
        this.VerifyVesselState(vesselActor, OperationalStatus.AtSea)

        let portPosition = { Latitude = 60.0; Longitude = 11.0 }
        let portId, portActor = this.CreatePort(store, "Test Port", portPosition, 1)

        this.SetVesselInRouteToPort(vesselId, vesselActor, portId, portPosition)
        this.SetVesselInRouteToPort(vesselId2, vesselActor2, portId, portPosition)

        // First saga to dock the port
        let _sagaId, sagaActor = this.CreateDockingSaga store
        let metadata = Domain.EventMetadata.createInitialMetadata None

        let message =
            Command.DockingSaga.DockingSagaProtocol.StartDocking(vesselId, portId, metadata)

        sagaActor.Tell(message, this.TestActor)

        match this.ExpectMsg<Command.DockingSaga.DockingSagaResponse>(TimeSpan.FromSeconds 10.0) with
        | Command.DockingSaga.DockingSagaResponse.DockingSagaFailed(failedSagaId, error) ->
            failwith $"Saga should not fail on first vessel: {error}"
        | Command.DockingSaga.DockingSagaResponse.DockingSagaCompleted _ -> ()

        // Second saga to dock the port and should fail
        let _sagaId2, sagaActor2 = this.CreateDockingSaga store
        let metadata = Domain.EventMetadata.createInitialMetadata None

        let message =
            Command.DockingSaga.DockingSagaProtocol.StartDocking(vesselId2, portId, metadata)

        sagaActor2.Tell(message, this.TestActor)

        match this.ExpectMsg<Command.DockingSaga.DockingSagaResponse>(TimeSpan.FromSeconds 10.0) with
        | Command.DockingSaga.DockingSagaResponse.DockingSagaFailed(failedSagaId, error) -> ()
        | Command.DockingSaga.DockingSagaResponse.DockingSagaCompleted _ ->
            failwith "Should not complete due to no available docks"
