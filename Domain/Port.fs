module Domain.Port

open System
open Shared.Api.Port

[<CLIMutable>]
type PortView = {
    Id: Guid
    Name: string
    Status: PortStatus
    Locode: string option
    Country: string
    Latitude: float
    Longitude: float
    Timezone: string option
    MaxDocks: int
    CurrentDocked: int
    Inserted: DateTimeOffset
}

module Event =
    type PortRegistered = {
        Id: Guid
        Name: string
        Locode: string option
        Country: string
        Latitude: float
        Longitude: float
        Timezone: string option
        MaxDocks: int
    }

    type VesselDocked = { VesselId: Guid }
    type VesselUndocked = { VesselId: Guid }
    type PortOpened = { PortId: Guid }
    type PortClosed = { PortId: Guid }
