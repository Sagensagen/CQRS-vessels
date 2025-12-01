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
type ISimulationApi = {
    ExecuteSimulation: int -> Async<Result<unit, SimulationErrors>>
    StopSimulation: unit -> Async<Result<unit, SimulationErrors>>
    GetPortStatistics: unit -> Async<Result<PortStatistics, string>>
    GetVesselStatistics: unit -> Async<Result<VesselStatistics, string>>
}
