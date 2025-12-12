module Shared.Api.Vessel

open System
open Shared.Api.Shared

type VesselType =
    | ContainerShip
    | BulkCarrier
    | Passenger
    | Fishing
    | Unknown of string

type RouteInfo = {
    RouteId: Guid
    DestinationPortId: Guid
    DestinationCoordinates: LatLong
    StartCoordinates: LatLong
    Waypoints: LatLong array
    CurrentWaypointIndex: int
    StartedAt: DateTimeOffset
}

type OperationalStatus =
    | AtSea
    | InRoute of routeInfo: RouteInfo
    | Docked of port: Guid
    | Anchored of location: string
    | UnderMaintenance
    | Decommissioned

type RegisterVesselRequest = {
    Name: string
    Position: LatLong
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
        Position = { Latitude = 0000; Longitude = 0000 }
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
    Position: LatLong
    Imo: int option
    Flag: string
    Length: float option
    Beam: float option
    Draught: float option
    State: OperationalStatus
    VesselType: VesselType
    CrewSize: int
    Inserted: DateTimeOffset
}

type VesselStatusCommand =
    | Arrive of portId: Guid
    | StartRoute of RouteInfo
    | Advance
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
    | RouteNotFinished
    | InvalidVesselState of expected: string * actual: string
    | CargoNotFound
    | PortNotFound
    | NoActiveRoute
    | NotInRoute
    | NoMoreWaypoints
    | RouteCalculationFailed of message: string
    | ValidationError of message: string
    | PersistenceError of message: string
    | InvalidStateTransition of string * string

type VesselQueryErrors = | VesselNotFound

// API interface
type IVesselApi = {
    CreateVessel: RegisterVesselRequest -> Async<Result<Guid, VesselCommandErrors>>
    GetVessel: Guid -> Async<Result<VesselDTO, VesselQueryErrors>>
    GetAllVessels: unit -> Async<Result<VesselDTO array, VesselQueryErrors>>
    UpdateOperationalStatus: Guid -> VesselStatusCommand -> Async<Result<Guid, VesselCommandErrors>>
    UpdatePosition: Guid -> LatLong -> Async<Result<Guid, VesselCommandErrors>>
    GetEvents: Guid -> Async<Result<Shared.EventWrapper array, VesselQueryErrors>>
}
