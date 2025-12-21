module Command.Tests.PortActorIntegrationTests

open System
open Xunit
open Akka.TestKit.Xunit2
open Command.PortActor
open Command.Tests.MartenFixture
open Domain.PortAggregate
open Domain.EventMetadata
open Shared.Api.Port
open Shared.Api.Shared
open Query.ReadModels

type PortActorIntegrationTests(fixture: MartenFixture) =
    inherit TestKit()
    interface IClassFixture<MartenFixture>

    [<Fact>]
    member this.``RegisterPort should persist events to Marten``() =
        // Arrange
        use store = fixture.CreateStore()
        let portId = Guid.NewGuid()

        let portActor = Command.PortActor.spawn this.Sys $"port-{portId}" portId store

        let cmd =
            { Id = portId
              Name = "Port of Testing"
              Locode = Some "NOOST"
              Country = "Norway"
              Position =
                { Latitude = 59.9139
                  Longitude = 10.7522 }
              Timezone = Some "Europe/Oslo"
              MaxDocks = 15
              Metadata = createInitialMetadata (Some "TestUser") }

        // Act - Send command to actor
        portActor.Tell(ExecuteCommand(RegisterPort cmd), this.TestActor)

        let result = this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0))

        match result with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Assert - Check events persisted to Marten
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(portId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(1, stream.Count)

        // Assert - Check event content
        let firstEvent = stream.[0].Data

        match firstEvent with
        | :? PortRegisteredEvt as evt ->
            Assert.Equal(portId, evt.Id)
            Assert.Equal("Port of Testing", evt.Name)
            Assert.Equal(15, evt.MaxDocks)
        | _ -> failwith "Expected PortRegisteredEvt"

    [<Fact>]
    member this.``ReserveDocking should create reservation``() =
        use store = fixture.CreateStore()
        let portId = Guid.NewGuid()
        let vesselId = Guid.NewGuid()
        let reservationId = Guid.NewGuid()

        let portActor =
            Command.PortActor.spawn this.Sys $"port-reserve-{portId}" portId store

        let registerCmd =
            { Id = portId
              Name = "Test Port"
              Locode = None
              Country = "Norway"
              Position = { Latitude = 60.0; Longitude = 10.0 }
              Timezone = None
              MaxDocks = 10
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(RegisterPort registerCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        let reserveCmd: ReserveDockingCmd =
            { AggregateId = portId
              VesselId = vesselId
              ReservationId = reservationId
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(ReserveDocking reserveCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(portId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(2, stream.Count)

        let secondEvent = stream.[1].Data

        match secondEvent with
        | :? VesselDockingReservedEvt as evt ->
            Assert.Equal(vesselId, evt.VesselId)
            Assert.Equal(reservationId, evt.ReservationId)
        | _ -> failwith "Expected VesselDockingReservedEvt"

    [<Fact>]
    member this.``Full docking lifecycle should work``() =
        use store = fixture.CreateStore()
        let portId = Guid.NewGuid()
        let vesselId = Guid.NewGuid()
        let reservationId = Guid.NewGuid()

        let portActor =
            Command.PortActor.spawn this.Sys $"port-lifecycle-{portId}" portId store

        let registerCmd =
            { Id = portId
              Name = "Lifecycle Port"
              Locode = Some "NOBGO"
              Country = "Norway"
              Position =
                { Latitude = 60.3913
                  Longitude = 5.3221 }
              Timezone = Some "Europe/Oslo"
              MaxDocks = 5
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(RegisterPort registerCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 2: Reserve docking
        let reserveCmd: ReserveDockingCmd =
            { AggregateId = portId
              VesselId = vesselId
              ReservationId = reservationId
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(ReserveDocking reserveCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 3: Confirm docking
        let confirmCmd: ConfirmDockingCmd =
            { AggregateId = portId
              VesselId = vesselId
              ReservationId = reservationId
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(ConfirmDocking confirmCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess eventCount -> Assert.Equal(1, eventCount)
        | PortCommandFailure err -> failwith $"Expected success but got error: {err}"

        // Step 4: Undock vessel
        let undockCmd =
            { AggregateId = portId
              VesselId = vesselId
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(UndockVessel undockCmd), this.TestActor)
        this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) |> ignore

        // Step 5: Query final state
        portActor.Tell(GetState, this.TestActor)

        match this.ExpectMsg<PortStateResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortExists state ->
            Assert.Equal("Lifecycle Port", state.Name)
            Assert.Empty(state.DockedVessels) // Vessel undocked
            Assert.Empty(state.PendingReservations) // Reservation cleared
        | PortStateResponse.PortNotFound -> failwith "Port should exist"

        // Assert - Check all events persisted
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(portId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(4, stream.Count)

        // Assert - Check read model updated (projection)
        let readModel =
            session.LoadAsync<PortReadModel>(portId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.NotNull(readModel)
        Assert.Equal("Lifecycle Port", readModel.Name)
        Assert.Equal(5, readModel.MaxDocks)
        Assert.Equal(0, readModel.CurrentDocked)
        Assert.Equal(5, readModel.AvailableDocks)

    [<Fact>]
    member this.``Port at capacity should reject reservation``() =
        // Arrange
        use store = fixture.CreateStore()
        let portId = Guid.NewGuid()

        let portActor =
            Command.PortActor.spawn this.Sys $"port-capacity-{portId}" portId store

        // Register port with only 1 dock
        let registerCmd =
            { Id = portId
              Name = "Small Port"
              Locode = None
              Country = "Norway"
              Position = { Latitude = 60.0; Longitude = 10.0 }
              Timezone = None
              MaxDocks = 1
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(RegisterPort registerCmd), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess _ -> ()
        | err -> failwith $"Expected success but got error: {err}"

        // Reserve the only dock
        let reserveCmd1: ReserveDockingCmd =
            { AggregateId = portId
              VesselId = Guid.NewGuid()
              ReservationId = Guid.NewGuid()
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(ReserveDocking reserveCmd1), this.TestActor)

        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandSuccess 1 -> ()
        | err -> failwith $"Expected success but got error: {err}"

        // Act - Try to reserve again (should fail - port full)
        let reserveCmd2: ReserveDockingCmd =
            { AggregateId = portId
              VesselId = Guid.NewGuid()
              ReservationId = Guid.NewGuid()
              Metadata = createInitialMetadata (Some "TestUser") }

        portActor.Tell(ExecuteCommand(ReserveDocking reserveCmd2), this.TestActor)

        // Assert - Should fail with specific error
        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandFailure NoDockingSpaceAvailable -> ()
        | _ -> failwith "Expected NoDockingSpaceAvailable error"

    [<Fact>]
    member this.``Invalid maxDocks should return failure``() =
        // Arrange
        use store = fixture.CreateStore()
        let portId = Guid.NewGuid()

        let portActor =
            Command.PortActor.spawn this.Sys $"port-invalid-{portId}" portId store

        let cmd =
            { Id = portId
              Name = "Invalid Port"
              Locode = None
              Country = "Norway"
              Position = { Latitude = 60.0; Longitude = 10.0 }
              Timezone = None
              MaxDocks = 0 // Invalid
              Metadata = createInitialMetadata (Some "TestUser") }

        // Act
        portActor.Tell(ExecuteCommand(RegisterPort cmd), this.TestActor)

        // Assert - Should fail with validation error
        match this.ExpectMsg<PortCommandResponse>(TimeSpan.FromSeconds(5.0)) with
        | PortCommandFailure(ValidationError msg) -> Assert.Contains("MaxDocks", msg)
        | _ -> failwith "Expected validation error"

        // Assert - No events should be persisted
        use session = store.QuerySession()

        let stream =
            session.Events.FetchStreamAsync(portId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(0, stream.Count)
