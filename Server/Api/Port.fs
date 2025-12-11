module Api.Port

open System
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Marten
open Shared.Api.Port
open CommandGateway
open Query.QueryHandlers

// Map the Marten Read projection into Client scoped type
let private mapPortToDTO (port: Query.ReadModels.PortReadModel) : PortDTO =
    { Id = port.Id
      Name = port.Name
      Status = port.Status
      Locode = port.Locode
      Country = port.Country
      Position = port.Position
      Timezone = port.Timezone
      MaxDocks = port.MaxDocks
      CurrentDocked = port.CurrentDocked
      Inserted = port.RegisteredAt }

let private portApi (ctx: HttpContext) : IPortApi =
    let commandGateway = ctx.GetService<CommandGateway>()
    let querySession = ctx.GetService<IQuerySession>()

    { CreatePort =
        fun (newPort: RegisterPortRequest) ->
            asyncResult {
                let newPortId = Guid.NewGuid()

                let! result =
                    commandGateway.RegisterPort(
                        newPortId,
                        newPort.Name,
                        newPort.Locode,
                        newPort.Country,
                        newPort.Position,
                        newPort.Timezone,
                        newPort.MaxDocks,
                        Some "API.CreatePort"
                    )

                return newPortId
            }

      GetAllPorts =
        fun () ->
            asyncResult {
                let! ports = getAllPorts querySession
                return ports |> Array.map mapPortToDTO
            } }

let portHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext portApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
