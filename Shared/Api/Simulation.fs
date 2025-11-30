module Shared.Api.Simulation

type SimulationConfig = {
    VesselCount: int
    PortCount: int
    OperationDelayMs: int
    DockDurationMs: int
}

// Error types for simulation operations
type SimulationErrors =
    | InvalidConfiguration of message: string
    | SimulationFailed of message: string
    | InsufficientResources of message: string

type ISimulationApi = {
    ExecuteSimulation: SimulationConfig -> Async<Result<unit, SimulationErrors>>
}
