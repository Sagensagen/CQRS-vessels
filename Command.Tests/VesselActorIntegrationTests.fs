module Command.Tests.VesselActorIntegrationTests

open System
open Xunit
open Akka.TestKit.Xunit2
open Command.VesselActor
open Command.Tests.MartenFixture
open Domain.VesselAggregate
open Domain.EventMetadata
open Shared.Api.Vessel
open Shared.Api.Shared
open Query.ReadModels

type VesselActorIntegrationTests(fixture: MartenFixture) =
    inherit TestKit()
    interface IClassFixture<MartenFixture>

    [<Fact>]
    member this.``RegisterVessel should persist events to Marten``() =
        use store = fixture.CreateStore()
        let vesselId = Guid.NewGuid()

        let vesselActor = this.Sys.ActorOf(props vesselId store, $"vessel-{vesselId}")

        let cmd =
            { Id = vesselId
              Name = "HMS Testing"
              Mmsi = 123456789
              Imo = Some 1234567
              Flag = "NO"
              Position =
                { Latitude = 59.9139
                  Longitude = 10.7522 }
              Length = Some 150.0
              Beam = Some 25.0
              Draught = Some 8.0
              VesselType = VesselType.Fishing
              CrewSize = 20
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(RegisterVessel cmd), this.TestActor)

        let result = this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0))

        match result with
        | VesselCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(vesselId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(1, stream.Count)

        let firstEvent = stream.[0].Data

        match firstEvent with
        | :? VesselRegisteredEvt as evt ->
            Assert.Equal(vesselId, evt.Id)
            Assert.Equal("HMS Testing", evt.Name)
            Assert.Equal(123456789, evt.Mmsi)
        | _ -> failwith "Expected VesselRegisteredEvt"

    [<Fact>]
    member this.``GetState should recover state from Marten events``() =
        use store = fixture.CreateStore()
        let vesselId = Guid.NewGuid()

        let vesselActor1 =
            this.Sys.ActorOf(props vesselId store, $"vessel-register-{vesselId}")

        let registerCmd =
            { Id = vesselId
              Name = "Test Vessel"
              Mmsi = 987654321
              Imo = None
              Flag = "NO"
              Position = { Latitude = 60.0; Longitude = 10.0 }
              Length = None
              Beam = None
              Draught = None
              VesselType = VesselType.Fishing
              CrewSize = 15
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor1.Tell(ExecuteCommand(RegisterVessel registerCmd), this.TestActor)
        let res = this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0))

        match res with
        | VesselCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        this.Watch(vesselActor1) |> ignore
        this.Sys.Stop(vesselActor1)
        this.ExpectTerminated(vesselActor1, TimeSpan.FromSeconds(5.0)) |> ignore

        let vesselActor2 =
            this.Sys.ActorOf(props vesselId store, $"vessel-recover-{vesselId}")

        vesselActor2.Tell(GetState, this.TestActor)

        match this.ExpectMsg<VesselStateResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselExists state ->
            Assert.Equal("Test Vessel", state.Name)
            Assert.Equal(987654321, state.Mmsi)
            Assert.Equal(OperationalStatus.AtSea, state.State)
        | Command.VesselActor.VesselNotFound -> failwith "Vessel should exist after recovery"

    [<Fact>]
    member this.``UpdatePosition should persist new event``() =
        use store = fixture.CreateStore()
        let vesselId = Guid.NewGuid()

        let vesselActor =
            this.Sys.ActorOf(props vesselId store, $"vessel-position-{vesselId}")

        let registerCmd =
            { Id = vesselId
              Name = "Moving Vessel"
              Mmsi = 111222333
              Imo = None
              Flag = "NO"
              Position = { Latitude = 59.0; Longitude = 10.0 }
              Length = None
              Beam = None
              Draught = None
              VesselType = VesselType.Fishing
              CrewSize = 10
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(RegisterVessel registerCmd), this.TestActor)
        this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) |> ignore

        // Act - Update position
        let updateCmd =
            { AggregateId = vesselId
              Position = { Latitude = 60.5; Longitude = 11.5 }
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(UpdatePosition updateCmd), this.TestActor)

        // Assert - Check response
        match this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Assert - Check events in Marten (should have 2 events now)
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(vesselId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(2, stream.Count)

        // Assert - Check second event is position update
        let secondEvent = stream.[1].Data

        match secondEvent with
        | :? VesselPositionUpdatedEvt as evt ->
            Assert.Equal(60.5, evt.Position.Latitude)
            Assert.Equal(11.5, evt.Position.Longitude)
        | _ -> failwith "Expected VesselPositionUpdatedEvt"

    [<Fact>]
    member this.``Full vessel lifecycle should work end-to-end``() =
        // Arrange
        use store = fixture.CreateStore()
        let vesselId = Guid.NewGuid()
        let portId = Guid.NewGuid()

        let vesselActor =
            this.Sys.ActorOf(props vesselId store, $"vessel-lifecycle-{vesselId}")

        // Step 1: Register vessel
        let registerCmd =
            { Id = vesselId
              Name = "Lifecycle Vessel"
              Mmsi = 444555666
              Imo = Some 7654321
              Flag = "NO"
              Position = { Latitude = 59.0; Longitude = 10.0 }
              Length = Some 200.0
              Beam = Some 30.0
              Draught = Some 10.0
              VesselType = VesselType.Fishing
              CrewSize = 25
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(RegisterVessel registerCmd), this.TestActor)

        match this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 2: Update position
        let updatePosCmd =
            { AggregateId = vesselId
              Position = { Latitude = 60.0; Longitude = 11.0 }
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(UpdatePosition updatePosCmd), this.TestActor)

        match this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 3: Arrive at port
        let arriveCmd =
            { AggregateId = vesselId
              PortId = portId
              ReservationId = Guid.NewGuid()
              Metadata = createInitialMetadata (Some "TestUser") }

        vesselActor.Tell(ExecuteCommand(ArriveAtPort arriveCmd), this.TestActor)

        match this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselCommandSuccess eventCount -> Assert.Equal(2, eventCount)
        | VesselCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 4: Query final state
        vesselActor.Tell(GetState, this.TestActor)
        let stateResponse = this.ExpectMsg<VesselStateResponse>(TimeSpan.FromSeconds(5.0))

        // Assert - Check final state
        match stateResponse with
        | VesselExists state ->
            Assert.Equal("Lifecycle Vessel", state.Name)
            Assert.Equal(60.0, state.Position.Latitude)
            Assert.Equal(Some portId, state.CurrentPortId)
            Assert.Equal(OperationalStatus.Docked portId, state.State)
        | Command.VesselActor.VesselNotFound -> failwith "Vessel should exist"

        // Assert - Check all events persisted (4 events: registered + position + arrived + status)
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(vesselId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(4, stream.Count)

        // Assert - Check read model updated (projection)
        let readModel =
            session.LoadAsync<VesselReadModel>(vesselId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.NotNull(readModel)
        Assert.Equal("Lifecycle Vessel", readModel.Name)
        Assert.Equal(60.0, readModel.Position.Latitude)
        Assert.Equal(Some portId, readModel.CurrentPortId)

    [<Fact>]
    member this.``Invalid MMSI should return failure``() =
        // Arrange
        use store = fixture.CreateStore()
        let vesselId = Guid.NewGuid()

        let vesselActor =
            this.Sys.ActorOf(props vesselId store, $"vessel-invalid-{vesselId}")

        let cmd =
            { Id = vesselId
              Name = "Invalid Vessel"
              Mmsi = 12345 // Invalid - only 5 digits
              Imo = None
              Flag = "NO"
              Position = { Latitude = 59.0; Longitude = 10.0 }
              Length = None
              Beam = None
              Draught = None
              VesselType = VesselType.Fishing
              CrewSize = 10
              Metadata = createInitialMetadata (Some "TestUser") }

        // Act
        vesselActor.Tell(ExecuteCommand(RegisterVessel cmd), this.TestActor)

        // Assert - Should fail with validation error
        match this.ExpectMsg<VesselCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | VesselCommandFailure(ValidationError msg) -> Assert.Contains("MMSI", msg)
        | _ -> failwith "Expected validation error"

        // Assert - No events should be persisted
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(vesselId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(0, stream.Count)
