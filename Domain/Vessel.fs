module Domain.Vessel

open System
open Microsoft.CodeAnalysis.Options
open Shared.Api.Vessel

[<CLIMutable>]
type VesselView = {
    Id: Guid
    Registered: DateTimeOffset
    Name: string
    Mmsi: int
    Position: VesselPosition
    Imo: int option
    Flag: string
    Length: float option
    Beam: float option
    Draught: float option
    State: OperationalStatus
    Activity: VesselActivity
    VesselType: VesselType
    CrewSize: int
    Inserted: DateTimeOffset
}

module Event =
    type VesselRegistered = {
        Id: Guid
        Name: string
        State: OperationalStatus
        Position: VesselPosition
        Mmsi: int
        Imo: int option
        Flag: string
        Length: float option
        Beam: float option
        Draught: float option
        VesselType: VesselType
        CrewSize: int
    }

    type VesselDeparted = { FromPort: string }

    type VesselArrived = { AtPort: string }

    type VesselLoadingStarted = { CargoId: Guid }

    type VesselUnloadingStarted = { CargoId: Guid }

    type VesselLoadingCompleted = { CargoId: Guid }

    type VesselUnloadingCompleted = { CargoId: Guid }

    type VesselPositionUpdated = VesselPosition

    type VesselDecommissioned = { At: DateTimeOffset }
