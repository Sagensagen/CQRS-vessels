module Shared.Api.Cargo

open System

[<Measure>]
type kg
[<Measure>]
type m

type Weight = float<kg>
type Length = float<m>
type Volume = float<m^3>

type CargoId = CargoId of Guid
type VesselId = VesselId of Guid
type PortId = PortId of Guid

type CargoType =
    | DryBulk
    | LiquidBulk
    | Containerized
    | Refrigerated
    | Hazardous of HazardClass

and HazardClass =
    | Explosives
    | Gases
    | FlammableLiquids
    | FlammableSolids
    | OxidisingSubstancesAndOrganicPeroxides
    | ToxicAndInfectiousSubstances
    | Radioactive
    | Toxic
    | Miscellaneous

type ContainerType =
    | TEU20
    | FEU40
    | Reefer20
    | Tank20

type ContainerLoad = { ContainerType: ContainerType; Count: int }

type CargoSpec = {
    CargoType: CargoType
    TotalWeight: Weight
    TotalVolume: Volume
    ContainerLoad: ContainerLoad
}

type CargoState =
    | Created
    | Reserved
    | Loaded of VesselId
    | InTransit of VesselId
    | Unloaded of PortId
    | Delivered
    | Cancelled

type Cargo = {
    Id: CargoId
    Spec: CargoSpec
    State: CargoState
}

// API Types

type CargoDTO = {
    Id: Guid
    Spec: CargoSpec
    Status: string
    OriginPortId: Guid
    OriginPortName: string option
    DestinationPortId: Guid
    DestinationPortName: string option
    CurrentVesselId: Guid option
    CurrentVesselName: string option
    CreatedAt: DateTimeOffset
    LoadedAt: DateTimeOffset option
    DeliveredAt: DateTimeOffset option
}

type CreateCargoRequest = {
    Spec: CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
}

type LoadCargoRequest = {
    CargoId: Guid
    VesselId: Guid
    PortId: Guid
}

type UnloadCargoRequest = {
    CargoId: Guid
    VesselId: Guid
    PortId: Guid
    IsDestinationPort: bool
}

type CargoCommandErrors =
    | CargoAlreadyExists
    | CargoNotFound
    | InvalidCargoSpec of string
    | CargoNotAtOriginPort
    | CargoAlreadyReserved of vesselId: Guid
    | CargoNotReserved
    | CargoReservedForDifferentVessel of reservedForVesselId: Guid
    | CargoAlreadyLoaded
    | CargoAlreadyLoadedOnAnotherVessel
    | CargoNotLoaded
    | CargoNotOnVessel
    | InvalidPortForUnloading
    | CargoNotAtDestination
    | CargoCancelled
    | ValidationError of string
    | InvalidCargoState of expected: string * actual: string
    | PersistenceError of string

type CargoQueryErrors = | CargoNotFound

// API Interface
type ICargoApi = {
    CreateCargo: CreateCargoRequest -> Async<Result<Guid, CargoCommandErrors>>
    GetCargo: Guid -> Async<Result<CargoDTO, CargoQueryErrors>>
    GetAllCargo: unit -> Async<Result<CargoDTO array, CargoQueryErrors>>
    GetCargoByPort: Guid -> Async<Result<CargoDTO array, CargoQueryErrors>>
    GetCargoByVessel: Guid -> Async<Result<CargoDTO array, CargoQueryErrors>>
    GetAvailableCargoAtPort: Guid -> Async<Result<CargoDTO array, CargoQueryErrors>>
    GetReservedCargoAtPort: Guid -> Async<Result<CargoDTO array, CargoQueryErrors>>
    LoadCargoOntoVessel: LoadCargoRequest -> Async<Result<Guid, CargoCommandErrors>>
    UnloadCargoFromVessel: UnloadCargoRequest -> Async<Result<Guid, CargoCommandErrors>>
    CancelCargo: Guid -> Async<Result<Guid, CargoCommandErrors>>
}
