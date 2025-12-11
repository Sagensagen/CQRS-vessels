module Api.Vessel

open System
open FsToolkit.ErrorHandling
open Giraffe.ComputationExpressions
open Microsoft.AspNetCore.Http
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Marten
open Shared.Api.Shared
open Shared.Api.Vessel
open CommandGateway
open Query.QueryHandlers

let private mapVesselToDTO (vessel: Query.ReadModels.VesselReadModel) : VesselDTO =
    { Id = vessel.Id
      Registered = vessel.RegisteredAt
      Name = vessel.Name
      Mmsi = vessel.Mmsi
      Position = vessel.Position
      Imo = vessel.Imo
      Flag = vessel.Flag
      Length = vessel.Length
      Beam = vessel.Beam
      Draught = vessel.Draught
      State = vessel.State
      VesselType = vessel.VesselType
      Inserted = vessel.RegisteredAt
      CrewSize = vessel.CrewSize }

let private vesselApi (ctx: HttpContext) : IVesselApi =
    let commandGateway = ctx.GetService<CommandGateway>()
    let querySession = ctx.GetService<IQuerySession>()

    { CreateVessel =
        fun (newVessel: RegisterVesselRequest) ->
            asyncResult {
                let newVesselId = Guid.NewGuid()

                let! res =
                    commandGateway.RegisterVessel(
                        newVesselId,
                        newVessel.Name,
                        newVessel.Mmsi,
                        newVessel.Imo,
                        newVessel.Flag,
                        newVessel.Position,
                        newVessel.Length,
                        newVessel.Beam,
                        newVessel.Draught,
                        newVessel.VesselType,
                        newVessel.CrewSize,
                        Some "API.CreateVessel"
                    )

                return res
            }

      GetVessel =
        fun id ->
            asyncResult {
                let! vessel = getVessel id querySession
                return vessel |> mapVesselToDTO
            }

      GetAllVessels =
        fun () ->
            asyncResult {
                let! vessels = getAllVessels querySession
                return vessels |> Array.map mapVesselToDTO
            }

      UpdateOperationalStatus =
        fun vesselId cmd ->
            asyncResult {
                match cmd with
                | Arrive portId -> return! commandGateway.StartDockingSaga(vesselId, Some "API.Arrive")

                | Depart portId ->
                    // Start the undocking process
                    return! commandGateway.StartUndockingSaga(vesselId, portId, Some "API.Depart")
                | Anchor position ->
                    return!
                        commandGateway.UpdateOperationalStatus(
                            vesselId,
                            OperationalStatus.Anchored "",
                            Some "API.Anchor"
                        )

                | StartMaintenance ->
                    return!
                        commandGateway.UpdateOperationalStatus(
                            vesselId,
                            OperationalStatus.UnderMaintenance,
                            Some "API.StartMaintenance"
                        )
                | Decommission -> return! commandGateway.DecommissionVessel(vesselId, Some "API.Decommission")

                | StartRoute route ->
                    // Try calculate shortest path
                    let! waypoints = Command.Route.AStar.aStar route.StartCoordinates route.DestinationCoordinates

                    // Remove consecutive duplicate waypoints
                    let deduplicatedWaypoints =
                        waypoints
                        |> Array.fold
                            (fun acc waypoint ->
                                match acc with
                                | [] -> [ waypoint ]
                                | head :: _ when
                                    head.Latitude = waypoint.Latitude && head.Longitude = waypoint.Longitude
                                    ->
                                    acc // Skip duplicate
                                | _ -> waypoint :: acc)
                            []
                        |> List.rev
                        |> List.toArray

                    return!
                        commandGateway.UpdateOperationalStatus(
                            vesselId,
                            OperationalStatus.InRoute
                                { route with
                                    Waypoints = deduplicatedWaypoints },
                            Some "API.Anchor"
                        )

                | Advance -> return! commandGateway.AdvanceRouteWaypoint(vesselId, Some "API.MoveSingleWaypoint")
            }

      UpdatePosition =
        fun id position ->
            asyncResult { return! commandGateway.UpdateVesselPosition(id, position, Some "API.UpdatePosition") }

      GetEvents =
        fun id ->
            asyncResult {
                let! events = getVesselEvents id querySession
                return events |> Seq.toArray
            } }

let vesselHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext vesselApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
