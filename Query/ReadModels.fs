module Query.ReadModels

open System
open Shared.Api.Shared
open Shared.Api.Vessel
open Shared.Api.Port
open Shared.Api.Cargo

[<CLIMutable>]
type VesselReadModel =
    { Id: Guid
      Name: string
      Mmsi: int
      Imo: int option
      Flag: string
      Position: LatLong
      Length: float option
      Beam: float option
      Draught: float option
      State: OperationalStatus
      VesselType: VesselType
      CrewSize: int
      CurrentPortId: Guid option
      CurrentPortName: string option
      CurrentCargo: CurrentVesselCargo option
      RegisteredAt: DateTimeOffset
      LastUpdated: DateTimeOffset
      Version: int64 }

[<CLIMutable>]
type DockedVesselInfo =
    { VesselId: Guid
      VesselName: string
      DockedAt: DateTimeOffset }

[<CLIMutable>]
type PortReadModel =
    { Id: Guid
      Name: string
      Locode: string option
      Country: string
      Position: LatLong
      Timezone: string option
      MaxDocks: int
      Status: PortStatus
      CurrentDocked: int
      AvailableDocks: int
      DockedVessels: DockedVesselInfo list
      RegisteredAt: DateTimeOffset
      LastUpdated: DateTimeOffset
      Version: int64 }

[<CLIMutable>]
type CargoReadModel =
    { Id: Guid
      Spec: CargoSpec
      Status: Domain.CargoAggregate.CargoStatus
      OriginPortId: Guid
      OriginPortName: string option
      DestinationPortId: Guid
      DestinationPortName: string option
      CurrentVesselId: Guid option
      CurrentVesselName: string option
      CreatedAt: DateTimeOffset
      LoadedAt: DateTimeOffset option
      DeliveredAt: DateTimeOffset option
      LastUpdated: DateTimeOffset
      Version: int64 }
