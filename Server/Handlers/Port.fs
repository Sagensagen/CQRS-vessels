module Handlers.Port

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open System
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Marten
open Shared.Api.Port

let private mapPortToDTO (vessel: Domain.Port.PortView) : PortDTO =
    { Id = vessel.Id
      Name = vessel.Name
      Status = vessel.Status
      Locode = vessel.Locode
      Country = vessel.Country
      Latitude = vessel.Latitude
      Longitude = vessel.Longitude
      Timezone = vessel.Timezone
      MaxDocks = vessel.MaxDocks
      CurrentDocked = vessel.CurrentDocked
      Inserted = vessel.Inserted }

let private portApi (ctx: HttpContext) : Shared.Api.Port.IPortApi =
    { CreatePort =
        fun (newPort: RegisterPortRequest) ->
            asyncResult {
                let session = ctx.GetService<IDocumentSession>()

                let newCommand =
                    Command.PortHandler.RegisterPort
                        { Id = Guid.NewGuid()
                          Name = newPort.Name
                          Locode = newPort.Locode
                          Country = newPort.Country
                          Latitude = newPort.Latitude
                          Longitude = newPort.Longitude
                          Timezone = newPort.Timezone
                          MaxDocks = newPort.MaxDocks }

                let! res = Command.PortHandler.decide newCommand session
                return res
            }
      GetAllPorts =
        fun unit ->
            asyncResult {
                let session = ctx.GetService<IQuerySession>()
                let! vessels = Query.getAllPorts session

                return vessels |> Array.map mapPortToDTO

            } }

let vesselHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext portApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
