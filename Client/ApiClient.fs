module Api

open Fable.Remoting.Client

let Account: Shared.Api.Account.IAccountApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<Shared.Api.Account.IAccountApi>

let VesselApi: Shared.Api.Vessel.IVesselApi =
  Remoting.createApi ()
  |> Remoting.withBaseUrl "/api"
  |> Remoting.buildProxy<Shared.Api.Vessel.IVesselApi>
