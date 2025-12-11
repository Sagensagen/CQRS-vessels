module Query.ReadModels

open System
open Shared.Api.Shared
open Shared.Api.Vessel
open Shared.Api.Port

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
type EventHistoryEntry =
    { Id: Guid
      AggregateId: Guid
      AggregateType: string
      EventType: string
      EventData: string
      Timestamp: DateTimeOffset
      Version: int64 }
