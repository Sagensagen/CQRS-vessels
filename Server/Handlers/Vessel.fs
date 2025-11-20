module Handlers.Vessel

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open System
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Marten
open Shared.Api.Vessel

let private mapVesselToDTO (vessel: Domain.Vessel.VesselView) : VesselDTO =
    { Id = vessel.Id
      Registered = vessel.Registered
      Name = vessel.Name
      Mmsi = vessel.Mmsi
      Position = vessel.Position
      Imo = vessel.Imo
      Flag = vessel.Flag
      Length = vessel.Length
      Beam = vessel.Beam
      Draught = vessel.Draught
      State = vessel.State
      Activity = vessel.Activity
      VesselType = vessel.VesselType
      Inserted = vessel.Inserted
      CrewSize = vessel.CrewSize }

let private vesselApi (ctx: HttpContext) : Shared.Api.Vessel.IVesselApi =
    { CreateVessel =
        fun (newVessel: Shared.Api.Vessel.RegisterVesselRequest) ->
            asyncResult {
                let session = ctx.GetService<IDocumentSession>()

                let newCommand =
                    Command.VesselHandler.RegisterVessel
                        { Id = Guid.NewGuid()
                          Name = newVessel.Name
                          VesselType = newVessel.VesselType
                          Position = newVessel.Position
                          Mmsi = newVessel.Mmsi
                          Imo = newVessel.Imo
                          Flag = newVessel.Flag
                          Length = newVessel.Length
                          Beam = newVessel.Beam
                          Draught = newVessel.Draught
                          CrewSize = newVessel.CrewSize }

                let! res = Command.VesselHandler.decide newCommand session
                return res
            }

      GetVessel =
        fun id ->
            asyncResult {
                let session = ctx.GetService<IQuerySession>()
                let! vessel = Query.getVessel id session
                return vessel |> mapVesselToDTO
            }
      GetAllVessels =
        fun () ->
            asyncResult {
                let session = ctx.GetService<IQuerySession>()
                let! vessels = Query.getAllVessels session

                return vessels |> Array.map mapVesselToDTO
            }
      UpdateOperationalStatus =
        fun id status ->
            asyncResult {
                let session = ctx.GetService<IDocumentSession>()

                let newCommand =
                    match status with
                    | Docked port ->
                        Command.VesselHandler.ArriveVessel
                            { VesselId = id
                              AtPort = port
                              Inserted = DateTimeOffset.UtcNow }
                    | AtSea ->
                        Command.VesselHandler.DepartVessel
                            { VesselId = id
                              FromPort = "Departed from dock"
                              Inserted = DateTimeOffset.UtcNow }
                    | Anchored position -> Command.VesselHandler.Other
                    | UnderMaintenance -> Command.VesselHandler.Other
                    | Decommissioned -> Command.VesselHandler.Other

                let! res = Command.VesselHandler.decide newCommand session
                return res
            }
      UpdatePosition =
        fun id position ->
            asyncResult {
                let session = ctx.GetService<IDocumentSession>()

                let newCommand =
                    Command.VesselHandler.UpdateVesselPosition
                        { VesselId = id
                          Position = position
                          Inserted = position.Timestamp }

                let! res = Command.VesselHandler.decide newCommand session
                return res

            }
      GetEvents =
        fun id ->
            asyncResult {
                let session = ctx.GetService<IQuerySession>()
                let! events = Query.getVesselEvents id session
                return events |> Seq.toArray
            } }


let vesselHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext vesselApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
