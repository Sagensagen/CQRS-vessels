module Shared.Api.Port

open System
open Shared.Api.Shared

type PortStatus =
    | Open
    | Closed

type RegisterPortRequest = {
    Name: string
    Locode: string option
    Country: string
    Position: LatLong
    MaxDocks: int
    Timezone: string option
} with

    static member DefaultEmpty = {
        Name = ""
        Locode = None
        Country = "Vietnam"
        Position = { Latitude = 0.; Longitude = 0. }
        MaxDocks = 10
        Timezone = None
    }

type PortDTO = {
    Id: Guid
    Name: string
    Status: PortStatus
    Locode: string option
    Country: string
    Position: LatLong
    Timezone: string option
    MaxDocks: int
    CurrentDocked: int
    Inserted: DateTimeOffset
}

type PortCommandErrors =
    | PortNotFound
    | PortAlreadyRegistered
    | PortAlreadyOpen
    | PortAlreadyClosed
    | NoDockingSpaceAvailable
    | VesselNotDockedAtPort
    | ReservationNotFound
    | InvalidPortState of expected: string * actual: string
    | CommandFailed of message: string // Generic message
    | VesselAlreadyDocked
    | ReservationAlreadyExists
    | ValidationError of message: string
    | PersistenceError of message: string

type PortQueryErrors =
    | PortNotFound
    | QueryFailed of message: string

type IPortApi = {
    CreatePort: RegisterPortRequest -> Async<Result<Guid, PortCommandErrors>>
    GetAllPorts: unit -> Async<Result<PortDTO array, PortQueryErrors>>
}
