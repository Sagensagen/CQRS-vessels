module Shared.Api.Simulation

type SimulationErrors =
    | InvalidConfiguration of message: string
    | SimulationFailed of message: string
    | InsufficientResources of message: string

type ISimulationApi = {
    ExecuteSimulation: int -> Async<Result<unit, SimulationErrors>>
    StopSimulation: unit -> Async<Result<unit, SimulationErrors>>
}
