module Shared.Api.Vessel

open System

type VesselType =
    | ContainerShip
    | BulkCarrier
    | Passenger
    | Fishing
    | Unknown of string

type VesselPosition = {
    Latitude: float
    Longitude: float
    Timestamp: DateTimeOffset
}

type VesselActivity =
    | Idle
    | CargoOperation of mode: CargoOperation * cargoId: Guid option
    | Maintenance
    | Refueling
    | CrewChange
and CargoOperation =
    | Loading
    | Unloading

and OperationalStatus =
    | AtSea
    | Docked of port: Guid
    | Anchored of location: string
    | UnderMaintenance
    | Decommissioned

type RegisterVesselRequest = {
    Name: string
    Position: VesselPosition
    Mmsi: int
    Imo: int option
    Flag: string
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
} with

    static member DefaultEmpty = {
        Name = ""
        Position = {
            Latitude = 0000
            Longitude = 0000
            Timestamp = DateTimeOffset.UtcNow
        }
        Mmsi = 0
        Imo = None
        Flag = ""
        Length = None
        Beam = None
        Draught = None
        VesselType = ContainerShip
        CrewSize = 0
    }

type VesselDTO = {
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

type VesselStatusCommand =
    | Arrive of portId: Guid
    | Depart of portId: Guid
    | Anchor of location: string
    | StartMaintenance
    | Decommission

type VesselCommandErrors =
    | VesselIdAlreadyExists
    | VesselNotFound
    | VesselAlreadyDecommissioned
    | VesselAlreadyDeparted
    | VesselIsAlreadyArrived
    | NoDockingAvailableAtPort
    | InvalidVesselState of expected: string * actual: string
    | CargoNotFound
    | PortNotFound

type VesselQueryErrors = | VesselNotFound

// API interface
type IVesselApi = {
    CreateVessel: RegisterVesselRequest -> Async<Result<Guid, VesselCommandErrors>>
    GetVessel: Guid -> Async<Result<VesselDTO, VesselQueryErrors>>
    GetAllVessels: unit -> Async<Result<VesselDTO array, VesselQueryErrors>>
    UpdateOperationalStatus: Guid -> VesselStatusCommand -> Async<Result<Guid, VesselCommandErrors>>
    UpdatePosition: Guid -> VesselPosition -> Async<Result<Guid, VesselCommandErrors>>
    GetEvents: Guid -> Async<Result<Shared.EventWrapper array, VesselQueryErrors>>
}
