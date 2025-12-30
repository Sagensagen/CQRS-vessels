module Api.Cargo

open System
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Marten
open Query.ReadModels
open Shared.Api.Cargo
open CommandGateway
open Query.QueryHandlers

let private mapCargoToDTO (cargo: Query.ReadModels.CargoReadModel) : CargoDTO =
    { Id = cargo.Id
      Spec = cargo.Spec
      Status = cargo.Status.ToString()
      OriginPortId = cargo.OriginPortId
      OriginPortName = cargo.OriginPortName
      DestinationPortId = cargo.DestinationPortId
      DestinationPortName = cargo.DestinationPortName
      CurrentVesselId = cargo.CurrentVesselId
      CurrentVesselName = cargo.CurrentVesselName
      CreatedAt = cargo.CreatedAt
      LoadedAt = cargo.LoadedAt
      DeliveredAt = cargo.DeliveredAt }

let private cargoApi (ctx: HttpContext) : ICargoApi =
    let commandGateway = ctx.GetService<CommandGateway>()
    let querySession = ctx.GetService<IQuerySession>()

    { CreateCargo =
        fun (req: CreateCargoRequest) ->
            asyncResult {
                let newCargoId = Guid.NewGuid()

                let! res =
                    commandGateway.CreateCargo(
                        newCargoId,
                        req.Spec,
                        req.OriginPortId,
                        req.DestinationPortId,
                        Some "API.CreateCargo"
                    )

                return res
            }

      GetCargo =
        fun id ->
            asyncResult {
                let! cargo = getCargo id querySession
                return cargo |> mapCargoToDTO
            }

      GetAllCargo =
        fun () ->
            asyncResult {
                let! cargo = getAllCargo querySession
                return cargo |> Array.map mapCargoToDTO
            }

      GetCargoByPort =
        fun portId ->
            asyncResult {
                let! cargo = getCargoByPort portId querySession
                return cargo |> Array.map mapCargoToDTO
            }

      GetCargoByVessel =
        fun vesselId ->
            asyncResult {
                let! cargo = getCargoByVessel vesselId querySession
                return cargo |> Array.map mapCargoToDTO
            }

      GetAvailableCargoAtPort =
        fun portId ->
            asyncResult {
                let! cargo = getAvailableCargoAtPort portId querySession
                return cargo |> Array.map mapCargoToDTO
            }

      GetReservedCargoAtPort =
        fun portId ->
            asyncResult {
                let! cargo = getReservedCargoAtPort portId querySession
                return cargo |> Array.map mapCargoToDTO
            }

      LoadCargoOntoVessel =
        fun (req: LoadCargoRequest) ->
            asyncResult {
                // Fetch cargo spec from read model
                let! cargo =
                    getCargo req.CargoId querySession
                    |> AsyncResult.mapError (fun _ -> CargoCommandErrors.CargoNotFound)

                let! res =
                    commandGateway.LoadCargoOntoVessel(
                        req.CargoId,
                        req.VesselId,
                        req.PortId,
                        cargo.Spec,
                        Some "API.LoadCargo"
                    )

                return res
            }

      UnloadCargoFromVessel =
        fun (req: UnloadCargoRequest) ->
            asyncResult {
                let! res =
                    commandGateway.UnloadCargoFromVessel(
                        req.CargoId,
                        req.VesselId,
                        req.PortId,
                        req.IsDestinationPort,
                        Some "API.UnloadCargo"
                    )

                return res
            }

      CancelCargo =
        fun cargoId ->
            asyncResult {
                let! res = commandGateway.CancelCargo(cargoId, Some "API.CancelCargo")
                return res
            } }

let cargoHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext cargoApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
