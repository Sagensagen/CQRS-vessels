module Shared.Api.Simulation

type SimulationErrors =
    | InvalidConfiguration of message: string
    | SimulationFailed of message: string
    | InsufficientResources of message: string

type PortStatistics = {
    AvailableDocks: int
    Closed: int
    OccupancyRate: float
    OccupiedDocks: int
    Open: int
    Total: int
    TotalDocks: int
}

type VesselStatistics = {
    Total: int
    AtSea: int
    Docked: int
    Decommissioned: int
    Active: int
}

type CargoStatistics = {
    Total: int
    AwaitingPickup: int
    Reserved: int
    LoadedOnVessel: int
    InTransit: int
    Delivered: int
    Cancelled: int
}

type ExecuteSimConfig = {
    VesselCount: int
    PortCount: int
    PositionAdvanceDelayMilliseconds: int
}

type ISimulationApi = {
    ExecuteSimulation: ExecuteSimConfig -> Async<Result<unit, SimulationErrors>>
    StopSimulation: unit -> Async<Result<unit, SimulationErrors>>
    GetPortStatistics: unit -> Async<Result<PortStatistics, string>>
    GetVesselStatistics: unit -> Async<Result<VesselStatistics, string>>
    GetCargoStatistics: unit -> Async<Result<CargoStatistics, string>>
}
