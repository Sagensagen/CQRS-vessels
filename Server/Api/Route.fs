module Api.Route

open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Shared.Api.Route

let private routeApi (ctx: HttpContext) : IRouteApi =
    { CalculateRoute =
        fun (startPos: LatLong) (endPos: LatLong) ->
            asyncResult {
                let! res =
                    Command.Route.AStar.aStar startPos.Latitude startPos.Longitude endPos.Latitude endPos.Longitude

                return res |> Array.map (fun (lat, lon) -> { Latitude = lat; Longitude = lon })
            } }

let routeHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext routeApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
