module VesselAggregateTests

open System
open Expecto
open Domain.VesselAggregate
open Domain.EventMetadata
open Shared.Api.Vessel
open Shared.Api.Shared

// Test helpers
let private createMetadata () = createInitialMetadata (Some "TestUser")

let private createValidRegisterVesselCmd () =
    { Id = Guid.NewGuid()
      Name = "Test Vessel"
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
      Metadata = createMetadata () }

[<Tests>]
let decideTests =
    testList
        "Vessel decide function"
        [

          testCase "RegisterVessel with valid data should succeed"
          <| fun () ->
              let cmd = createValidRegisterVesselCmd ()
              let result = decide None (RegisterVessel cmd)

              Expect.isOk result "Should succeed with valid data"

              match result with
              | Ok events ->
                  Expect.equal events.Length 1 "Should produce one VesselRegistered event"

                  match events.[0] with
                  | VesselRegistered evt ->
                      Expect.equal evt.Id cmd.Id "Event ID should match command ID"
                      Expect.equal evt.Name cmd.Name "Event name should match command name"
                      Expect.equal evt.Mmsi cmd.Mmsi "Event MMSI should match command MMSI"
                  | _ -> failtest "Expected VesselRegistered event"
              | Error _ -> failtest "Expected success"

          testCase "RegisterVessel with invalid MMSI should fail"
          <| fun () ->
              let cmd =
                  { createValidRegisterVesselCmd () with
                      Mmsi = 12345 }

              let result = decide None (RegisterVessel cmd)

              Expect.isError result "Should fail with invalid MMSI"

              match result with
              | Error(ValidationError msg) -> Expect.stringContains msg "MMSI" "Error should mention MMSI"
              | _ -> failtest "Expected validation error"

          testCase "RegisterVessel with invalid IMO should fail"
          <| fun () ->
              let cmd =
                  { createValidRegisterVesselCmd () with
                      Imo = Some 123 }

              let result = decide None (RegisterVessel cmd)

              Expect.isError result "Should fail with invalid IMO"

              match result with
              | Error(ValidationError msg) -> Expect.stringContains msg "IMO" "Error should mention IMO"
              | _ -> failtest "Expected validation error"

          testCase "RegisterVessel with empty name should fail"
          <| fun () ->
              let cmd =
                  { createValidRegisterVesselCmd () with
                      Name = "" }

              let result = decide None (RegisterVessel cmd)

              Expect.isError result "Should fail with empty name"

          testCase "RegisterVessel when vessel already exists should fail"
          <| fun () ->
              let cmd = createValidRegisterVesselCmd ()

              let existingState =
                  Some
                      { Id = cmd.Id
                        Version = 1L
                        Name = "Existing Vessel"
                        Mmsi = 987654321
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 60.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.AtSea
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = None
                        RegisteredAt = DateTimeOffset.UtcNow }

              let result = decide existingState (RegisterVessel cmd)

              Expect.equal result (Error VesselIdAlreadyExists) "Should fail when vessel exists"

          testCase "UpdatePosition with valid position should succeed"
          <| fun () ->
              let vesselId = Guid.NewGuid()

              let state =
                  Some
                      { Id = vesselId
                        Version = 1L
                        Name = "Test Vessel"
                        Mmsi = 123456789
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 59.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.AtSea
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = None
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd =
                  { AggregateId = vesselId
                    Position = { Latitude = 60.0; Longitude = 11.0 }
                    Metadata = createMetadata () }

              let result = decide state (UpdatePosition cmd)

              Expect.isOk result "Should succeed"

              match result with
              | Ok events ->
                  Expect.equal events.Length 1 "Should produce one event"

                  match events.[0] with
                  | VesselPositionUpdated evt -> Expect.equal evt.Position cmd.Position "Position should match"
                  | _ -> failtest "Expected VesselPositionUpdated event"
              | Error _ -> failtest "Expected success"

          testCase "ArriveAtPort when AtSea should succeed"
          <| fun () ->
              let vesselId = Guid.NewGuid()
              let portId = Guid.NewGuid()

              let state =
                  Some
                      { Id = vesselId
                        Version = 1L
                        Name = "Test Vessel"
                        Mmsi = 123456789
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 59.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.AtSea
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = None
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd =
                  { AggregateId = vesselId
                    PortId = portId
                    ReservationId = Guid.NewGuid()
                    Metadata = createMetadata () }

              let result = decide state (ArriveAtPort cmd)

              Expect.isOk result "Should succeed"

              match result with
              | Ok events -> Expect.equal events.Length 2 "Should produce two events"
              | Error _ -> failtest "Expected success"

          testCase "ArriveAtPort when already Docked should fail"
          <| fun () ->
              let vesselId = Guid.NewGuid()
              let currentPortId = Guid.NewGuid()

              let state =
                  Some
                      { Id = vesselId
                        Version = 1L
                        Name = "Test Vessel"
                        Mmsi = 123456789
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 59.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.Docked currentPortId
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = Some currentPortId
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd =
                  { AggregateId = vesselId
                    PortId = Guid.NewGuid()
                    ReservationId = Guid.NewGuid()
                    Metadata = createMetadata () }

              let result = decide state (ArriveAtPort cmd)

              Expect.isError result "Should fail when already docked" ]

[<Tests>]
let evolveTests =
    testList
        "Vessel evolve function"
        [

          testCase "Evolve with VesselRegistered should create initial state"
          <| fun () ->
              let evt =
                  { Id = Guid.NewGuid()
                    Name = "Test Vessel"
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
                    RegisteredAt = DateTimeOffset.UtcNow }

              let newState = evolve None (VesselRegistered evt)

              Expect.isSome newState "State should be created"

              match newState with
              | Some state ->
                  Expect.equal state.Id evt.Id "ID should match"
                  Expect.equal state.Name evt.Name "Name should match"
                  Expect.equal state.State OperationalStatus.AtSea "Should start AtSea"
                  Expect.isNone state.CurrentPortId "Should have no port"
              | None -> failtest "Expected state"

          testCase "Evolve with VesselPositionUpdated should update position"
          <| fun () ->
              let initialState =
                  Some
                      { Id = Guid.NewGuid()
                        Version = 1L
                        Name = "Test Vessel"
                        Mmsi = 123456789
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 59.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.AtSea
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = None
                        RegisteredAt = DateTimeOffset.UtcNow }

              let newPosition = { Latitude = 60.0; Longitude = 11.0 }

              let evt =
                  { Position = newPosition
                    UpdatedAt = DateTimeOffset.UtcNow }

              let newState = evolve initialState (VesselPositionUpdated evt)

              match newState with
              | Some state -> Expect.equal state.Position newPosition "Position should be updated"
              | None -> failtest "State should exist"

          testCase "Evolve with VesselArrived should set CurrentPortId"
          <| fun () ->
              let portId = Guid.NewGuid()

              let initialState =
                  Some
                      { Id = Guid.NewGuid()
                        Version = 1L
                        Name = "Test Vessel"
                        Mmsi = 123456789
                        Imo = None
                        Flag = "NO"
                        Position = { Latitude = 59.0; Longitude = 10.0 }
                        Length = None
                        Beam = None
                        Draught = None
                        State = OperationalStatus.AtSea
                        VesselType = VesselType.Fishing
                        CrewSize = 10
                        CurrentPortId = None
                        RegisteredAt = DateTimeOffset.UtcNow }

              let evt =
                  { PortId = portId
                    ReservationId = Guid.NewGuid()
                    ArrivedAt = DateTimeOffset.UtcNow }

              let newState = evolve initialState (VesselArrived evt)

              match newState with
              | Some state -> Expect.equal state.CurrentPortId (Some portId) "CurrentPortId should be set"
              | None -> failtest "State should exist"

          testCase "Event sourcing: multiple events should build correct state"
          <| fun () ->
              let vesselId = Guid.NewGuid()
              let portId = Guid.NewGuid()

              let events =
                  [ VesselRegistered
                        { Id = vesselId
                          Name = "Test Vessel"
                          Mmsi = 123456789
                          Imo = None
                          Flag = "NO"
                          Position = { Latitude = 59.0; Longitude = 10.0 }
                          Length = None
                          Beam = None
                          Draught = None
                          VesselType = VesselType.Fishing
                          CrewSize = 10
                          RegisteredAt = DateTimeOffset.UtcNow }
                    VesselPositionUpdated
                        { Position = { Latitude = 60.0; Longitude = 11.0 }
                          UpdatedAt = DateTimeOffset.UtcNow }
                    VesselArrived
                        { PortId = portId
                          ReservationId = Guid.NewGuid()
                          ArrivedAt = DateTimeOffset.UtcNow } ]

              let finalState = events |> List.fold evolve None

              match finalState with
              | Some state ->
                  Expect.equal state.Id vesselId "ID should match"
                  Expect.equal state.Position.Latitude 60.0 "Latitude should be updated"
                  Expect.equal state.CurrentPortId (Some portId) "Should be at port"
              | None -> failtest "State should exist" ]

[<Tests>]
let tests = testList "Vessel Aggregate" [ decideTests; evolveTests ]
