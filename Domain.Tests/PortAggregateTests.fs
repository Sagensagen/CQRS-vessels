module PortAggregateTests

open System
open Expecto
open Domain.PortAggregate
open Domain.EventMetadata
open Shared.Api.Port
open Shared.Api.Shared

// Test helpers
let private createMetadata () = createInitialMetadata (Some "TestUser")

let private createValidRegisterPortCmd () =
    { Id = Guid.NewGuid()
      Name = "Test Port"
      Locode = Some "USNYC"
      Country = "United States"
      Position =
        { Latitude = 40.7128
          Longitude = -74.0060 }
      Timezone = Some "America/New_York"
      MaxDocks = 10
      Metadata = createMetadata () }

[<Tests>]
let decideTests =
    testList
        "Port decide function"
        [

          testCase "RegisterPort with valid data should succeed"
          <| fun () ->
              let cmd = createValidRegisterPortCmd ()
              let result = decide None (RegisterPort cmd)

              Expect.isOk result "Should succeed with valid data"

              match result with
              | Ok events ->
                  Expect.equal events.Length 1 "Should produce one PortRegistered event"

                  match events.[0] with
                  | PortRegistered evt ->
                      Expect.equal evt.Id cmd.Id "Event ID should match command ID"
                      Expect.equal evt.Name cmd.Name "Event name should match command name"
                      Expect.equal evt.MaxDocks cmd.MaxDocks "MaxDocks should match"
                  | _ -> failtest "Expected PortRegistered event"
              | Error _ -> failtest "Expected success"

          testCase "RegisterPort with empty name should fail"
          <| fun () ->
              let cmd =
                  { createValidRegisterPortCmd () with
                      Name = "" }

              let result = decide None (RegisterPort cmd)

              Expect.isError result "Should fail with empty name"

              match result with
              | Error(ValidationError msg) -> Expect.stringContains msg "name" "Error should mention name"
              | _ -> failtest "Expected validation error"

          testCase "RegisterPort with maxDocks of 0 should fail"
          <| fun () ->
              let cmd =
                  { createValidRegisterPortCmd () with
                      MaxDocks = 0 }

              let result = decide None (RegisterPort cmd)

              Expect.isError result "Should fail with maxDocks = 0"

              match result with
              | Error(ValidationError msg) -> Expect.stringContains msg "MaxDocks" "Error should mention MaxDocks"
              | _ -> failtest "Expected validation error"

          testCase "ReserveDocking when port has available space should succeed"
          <| fun () ->
              let portId = Guid.NewGuid()

              let state =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 10
                        Status = PortStatus.Open
                        DockedVessels = Set.empty
                        PendingReservations = Map.empty
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd: ReserveDockingCmd =
                  { AggregateId = portId
                    VesselId = Guid.NewGuid()
                    ReservationId = Guid.NewGuid()
                    Metadata = createMetadata () }

              let result = decide state (ReserveDocking cmd)

              Expect.isOk result "Should succeed"

              match result with
              | Ok events ->
                  Expect.equal events.Length 1 "Should produce one event"

                  match events.[0] with
                  | VesselDockingReserved evt ->
                      Expect.equal evt.VesselId cmd.VesselId "VesselId should match"
                      Expect.equal evt.ReservationId cmd.ReservationId "ReservationId should match"
                  | _ -> failtest "Expected VesselDockingReserved event"
              | Error _ -> failtest "Expected success"

          testCase "ReserveDocking when port is full should fail"
          <| fun () ->
              let portId = Guid.NewGuid()

              let state =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 2
                        Status = PortStatus.Open
                        DockedVessels = Set.ofList [ Guid.NewGuid(); Guid.NewGuid() ]
                        PendingReservations = Map.empty
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd =
                  ReserveDocking
                      { AggregateId = portId
                        VesselId = Guid.NewGuid()
                        ReservationId = Guid.NewGuid()
                        Metadata = createMetadata () }

              let result = decide state cmd

              Expect.equal result (Error NoDockingSpaceAvailable) "Should fail when port is full"

          testCase "ConfirmDocking with valid reservation should succeed"
          <| fun () ->
              let portId = Guid.NewGuid()
              let vesselId = Guid.NewGuid()
              let reservationId = Guid.NewGuid()

              let reservation: ReservationState =
                  { ReservationId = reservationId
                    VesselId = vesselId
                    ReservedAt = DateTimeOffset.UtcNow
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30.0) }

              let state =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 10
                        Status = PortStatus.Open
                        DockedVessels = Set.empty
                        PendingReservations = Map.ofList [ (reservationId, reservation) ]
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd: ConfirmDockingCmd =
                  { AggregateId = portId
                    VesselId = vesselId
                    ReservationId = reservationId
                    Metadata = createMetadata () }

              let result = decide state (ConfirmDocking cmd)

              Expect.isOk result "Should succeed"

          testCase "UndockVessel when vessel is docked should succeed"
          <| fun () ->
              let portId = Guid.NewGuid()
              let vesselId = Guid.NewGuid()

              let state =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 10
                        Status = PortStatus.Open
                        DockedVessels = Set.ofList [ vesselId ]
                        PendingReservations = Map.empty
                        RegisteredAt = DateTimeOffset.UtcNow }

              let cmd =
                  { AggregateId = portId
                    VesselId = vesselId
                    Metadata = createMetadata () }

              let result = decide state (UndockVessel cmd)

              Expect.isOk result "Should succeed" ]

[<Tests>]
let evolveTests =
    testList
        "Port evolve function"
        [

          testCase "Evolve with PortRegistered should create initial state"
          <| fun () ->
              let evt =
                  { Id = Guid.NewGuid()
                    Name = "Test Port"
                    Locode = Some "USNYC"
                    Country = "United States"
                    Position =
                      { Latitude = 40.7128
                        Longitude = -74.0060 }
                    Timezone = Some "America/New_York"
                    MaxDocks = 10
                    RegisteredAt = DateTimeOffset.UtcNow }

              let newState = evolve None (PortRegistered evt)

              Expect.isSome newState "State should be created"

              match newState with
              | Some state ->
                  Expect.equal state.Id evt.Id "ID should match"
                  Expect.equal state.Name evt.Name "Name should match"
                  Expect.equal state.MaxDocks evt.MaxDocks "MaxDocks should match"
                  Expect.equal state.Status PortStatus.Open "Should start Open"
                  Expect.isEmpty state.DockedVessels "Should have no docked vessels"
              | None -> failtest "Expected state"

          testCase "Evolve with VesselDockingReserved should add reservation"
          <| fun () ->
              let portId = Guid.NewGuid()
              let vesselId = Guid.NewGuid()
              let reservationId = Guid.NewGuid()

              let initialState =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 10
                        Status = PortStatus.Open
                        DockedVessels = Set.empty
                        PendingReservations = Map.empty
                        RegisteredAt = DateTimeOffset.UtcNow }

              let evt =
                  { VesselId = vesselId
                    ReservationId = reservationId
                    ReservedAt = DateTimeOffset.UtcNow
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30.0) }

              let newState = evolve initialState (VesselDockingReserved evt)

              match newState with
              | Some state ->
                  Expect.equal state.PendingReservations.Count 1 "Should have one reservation"
                  Expect.isTrue (state.PendingReservations.ContainsKey reservationId) "Reservation should exist"
              | None -> failtest "State should exist"

          testCase "Evolve with DockingConfirmed should move vessel to docked"
          <| fun () ->
              let portId = Guid.NewGuid()
              let vesselId = Guid.NewGuid()
              let reservationId = Guid.NewGuid()

              let reservation: ReservationState =
                  { ReservationId = reservationId
                    VesselId = vesselId
                    ReservedAt = DateTimeOffset.UtcNow
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30.0) }

              let initialState =
                  Some
                      { Id = portId
                        Version = 1L
                        Name = "Test Port"
                        Locode = None
                        Country = "Norway"
                        Position =
                          { Latitude = 59.9139
                            Longitude = 10.7522 }
                        Timezone = None
                        MaxDocks = 10
                        Status = PortStatus.Open
                        DockedVessels = Set.empty
                        PendingReservations = Map.ofList [ (reservationId, reservation) ]
                        RegisteredAt = DateTimeOffset.UtcNow }

              let evt =
                  { VesselId = vesselId
                    ReservationId = reservationId
                    ConfirmedAt = DateTimeOffset.UtcNow }

              let newState = evolve initialState (DockingConfirmed evt)

              match newState with
              | Some state ->
                  Expect.contains state.DockedVessels vesselId "Vessel should be docked"
                  Expect.equal state.PendingReservations.Count 0 "Reservation should be removed"
              | None -> failtest "State should exist"

          testCase "AvailableDocks calculation should be correct"
          <| fun () ->
              let portId = Guid.NewGuid()
              let vessel1 = Guid.NewGuid()
              let vessel2 = Guid.NewGuid()
              let reservationId = Guid.NewGuid()

              let reservation: ReservationState =
                  { ReservationId = reservationId
                    VesselId = Guid.NewGuid()
                    ReservedAt = DateTimeOffset.UtcNow
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30.0) }

              let state =
                  { Id = portId
                    Version = 1L
                    Name = "Test Port"
                    Locode = None
                    Country = "Norway"
                    Position =
                      { Latitude = 59.9139
                        Longitude = 10.7522 }
                    Timezone = None
                    MaxDocks = 10
                    Status = PortStatus.Open
                    DockedVessels = Set.ofList [ vessel1; vessel2 ]
                    PendingReservations = Map.ofList [ (reservationId, reservation) ]
                    RegisteredAt = DateTimeOffset.UtcNow }

              let availableDocks = state.AvailableDocks

              Expect.equal availableDocks 7 "Should be 10 - 2 (docked) - 1 (reserved) = 7" ]

[<Tests>]
let tests = testList "Port Aggregate" [ decideTests; evolveTests ]
