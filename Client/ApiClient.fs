module ApiClient

open Fable.Remoting.Client

let Vessel: Shared.Api.Vessel.IVesselApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<Shared.Api.Vessel.IVesselApi>

let Port: Shared.Api.Port.IPortApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<Shared.Api.Port.IPortApi>

let Simulation: Shared.Api.Simulation.ISimulationApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<Shared.Api.Simulation.ISimulationApi>
