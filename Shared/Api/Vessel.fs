module Shared.Domain.Vessel

open System

// ---------------------PROJECTION----------------------
type VesselType =
    | ContainerShip
    | BulkCarrier
    | Passenger
    | Fishing
    | Unknown of string
    
type VesselState =
    | Registered of DateTimeOffset
    | Active of VesselActivity
    | Decommissioned of DateTimeOffset

and VesselActivity =
    | Docked of port: string
    | AtSea of location: string option
    | Loading of cargoId: Guid
    | Unloading of cargoId: Guid
    | UnderMaintenance

[<CLIMutable>]
type Vessel = {
    Id: Guid
    Name: string
    Inserted: DateTimeOffset
    VesselType: VesselType
    State: VesselState
}

// ---------------------API---------------------
type RegisterVesselRequest = {
    Name: string
    VesselType:  VesselType  // String instead of discriminated union
    InitialActivity: VesselActivity
}

type DepartVesselRequest = {
    FromPort: string
}

type ArriveVesselRequest = {
    AtPort: string
}

type StartLoadingRequest = {
    CargoId: Guid
}

type UpdateVesselPositionRequest = {
    Position: string
}
        
type IVesselApi = {
    CreateVessel: RegisterVesselRequest -> Async<Result<Guid, string>>
}