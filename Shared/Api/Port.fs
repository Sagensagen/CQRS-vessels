module Shared.Api.Port

open System

type PortStatus =
    | Open
    | Closed

type RegisterPortRequest = {
    Name: string
    Locode: string option
    Country: string
    Latitude: float
    Longitude: float
    MaxDocks: int
    Timezone: string option
} with

    static member DefaultEmpty = {
        Name = ""
        Locode = None
        Country = "Vietnam"
        Latitude = 0.
        Longitude = 0.
        MaxDocks = 10
        Timezone = None
    }

type PortDTO = {
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

type IPortApi = {
    CreatePort: RegisterPortRequest -> Async<Result<Guid, string>>
    // GetPort: Guid -> Async<Result<PortDTO, string>>
    GetAllPorts: unit -> Async<Result<PortDTO array, string>>
}
