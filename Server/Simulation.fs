module Simulation

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Marten
open Microsoft.AspNetCore.Http
open Serilog
open Shared.Api.Vessel
open Shared.Api.Shared
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Shared.Api.Simulation

type private Port = { Name: string; Position: LatLong }

let private ports: Port list =
    [ { Name = "Port of Shanghai, China"
        Position = { Latitude = 31.22; Longitude = 121.48 } }
      { Name = "Yangshan Port, China"
        Position =
          { Latitude = 30.6294
            Longitude = 122.0577 } }
      { Name = "Port of Singapore, Singapore"
        Position =
          { Latitude = 1.264
            Longitude = 103.820 } }
      { Name = "Port of Hong Kong, Hong Kong"
        Position =
          { Latitude = 22.317
            Longitude = 114.170 } }
      { Name = "Port of Busan, South Korea"
        Position = { Latitude = 35.10; Longitude = 129.04 } }
      { Name = "Port of Yokohama, Japan"
        Position = { Latitude = 35.27; Longitude = 139.38 } }
      { Name = "Port of Los Angeles, USA"
        Position =
          { Latitude = 33.7428
            Longitude = -118.2614 } }
      { Name = "Port of Long Beach, USA"
        Position =
          { Latitude = 33.75
            Longitude = -118.20 } }
      { Name = "Port of Rotterdam, Netherlands"
        Position = { Latitude = 51.95; Longitude = 4.14 } }
      { Name = "Port of Antwerp-Bruges, Belgium"
        Position = { Latitude = 51.22; Longitude = 4.40 } }
      { Name = "Port of Barcelona, Spain"
        Position =
          { Latitude = 41.346176
            Longitude = 2.168365 } }
      { Name = "Port of Gothenburg, Sweden"
        Position = { Latitude = 57.7; Longitude = 11.9 } }
      { Name = "Port of Jebel Ali (Dubai), UAE"
        Position =
          { Latitude = 25.0033
            Longitude = 55.0521 } }
      { Name = "Port of Djibouti, Djibouti"
        Position = { Latitude = 11.59; Longitude = 43.15 } }
      { Name = "Port of Callao, Peru"
        Position =
          { Latitude = -12.05
            Longitude = -77.15 } }
      { Name = "Port of Tianjin, China"
        Position = { Latitude = 39.0; Longitude = 117.7 } }
      { Name = "Port of Shenzhen, China"
        Position = { Latitude = 22.5; Longitude = 114.0 } }
      { Name = "Port of Qingdao, China"
        Position = { Latitude = 36.07; Longitude = 120.38 } }
      { Name = "Port of Guangzhou, China"
        Position = { Latitude = 23.10; Longitude = 113.25 } }
      { Name = "Port of Durban, South Africa"
        Position = { Latitude = -29.88; Longitude = 31.02 } }
      { Name = "Port of Santos, Brazil"
        Position =
          { Latitude = -23.96
            Longitude = -46.33 } }
      { Name = "Port of Buenos Aires, Argentina"
        Position = { Latitude = -34.6; Longitude = -58.4 } }
      { Name = "Port of Valparaiso, Chile"
        Position =
          { Latitude = -33.05
            Longitude = -71.63 } }
      { Name = "Port of Vancouver, Canada"
        Position = { Latitude = 49.3; Longitude = -123.1 } }
      { Name = "Port of Seattle, USA"
        Position = { Latitude = 47.6; Longitude = -122.33 } }
      { Name = "Port of Hamburg, Germany"
        Position = { Latitude = 53.55; Longitude = 9.99 } }
      { Name = "Port of Marseille, France"
        Position = { Latitude = 43.3; Longitude = 5.37 } }
      { Name = "Port of Piraeus, Greece"
        Position = { Latitude = 37.94; Longitude = 23.64 } }
      { Name = "Port of Alexandria, Egypt"
        Position = { Latitude = 31.2; Longitude = 29.9 } }
      { Name = "Port of Mombasa, Kenya"
        Position = { Latitude = -4.05; Longitude = 39.66 } }
      { Name = "Port of Lagos (Apapa), Nigeria"
        Position = { Latitude = 6.45; Longitude = 3.40 } }
      { Name = "Port of Colombo, Sri Lanka"
        Position = { Latitude = 6.93; Longitude = 79.85 } }
      { Name = "Port of Karachi, Pakistan"
        Position =
          { Latitude = 24.835596
            Longitude = 66.981445 } }
      { Name = "Port of Mumbai (Nhava Sheva), India"
        Position = { Latitude = 18.94; Longitude = 72.83 } }
      { Name = "Port of Ho Chi Minh City, Vietnam"
        Position = { Latitude = 10.82; Longitude = 106.63 } }
      { Name = "Port of Jakarta (Tanjung Priok), Indonesia"
        Position = { Latitude = -6.12; Longitude = 106.85 } }
      { Name = "Port of Manila, Philippines"
        Position = { Latitude = 14.58; Longitude = 120.97 } }
      { Name = "Port of Sydney, Australia"
        Position =
          { Latitude = -33.86
            Longitude = 151.20 } }
      { Name = "Port of Melbourne, Australia"
        Position =
          { Latitude = -37.82
            Longitude = 144.96 } }
      { Name = "Port of Genoa, Italy"
        Position = { Latitude = 44.40; Longitude = 8.93 } }
      { Name = "Port of Trieste, Italy"
        Position = { Latitude = 45.65; Longitude = 13.76 } }
      { Name = "Port of Istanbul, Turkey"
        Position = { Latitude = 41.0; Longitude = 28.97 } }
      { Name = "Port of Haifa, Israel"
        Position = { Latitude = 32.82; Longitude = 34.99 } }
      { Name = "Port of Beirut, Lebanon"
        Position = { Latitude = 33.90; Longitude = 35.52 } }
      { Name = "Port of Montreal, Canada"
        Position = { Latitude = 45.50; Longitude = -73.55 } }
      { Name = "Port of New York/New Jersey, USA"
        Position = { Latitude = 40.67; Longitude = -74.04 } }
      { Name = "Port of Savannah, USA"
        Position = { Latitude = 32.08; Longitude = -81.10 } }
      { Name = "Port of Houston, USA"
        Position = { Latitude = 29.73; Longitude = -95.27 } }
      { Name = "Port of Auckland, New Zealand"
        Position =
          { Latitude = -36.84
            Longitude = 174.76 } }
      { Name = "Port of Wellington, New Zealand"
        Position =
          { Latitude = -41.29
            Longitude = 174.78 } } ]

type VesselState =
    | AtSea
    | InRoute of destinationPortId: Guid * currentWaypointIndex: int * totalWaypoints: int
    | Docked of portId: Guid
// | // Extend these with Cargo activities or something later?

type SimulatedVessel =
    { VesselId: Guid
      Name: string
      State: VesselState
      CurrentPosition: LatLong }

let private random = Random()

let private randomName prefix = $"{prefix}-{(random.Next(1000, 9999))}"

let private randomPosition () =
    { Latitude = (random.NextDouble() * 180.0) - 90.0
      Longitude = (random.NextDouble() * 360.0) - 180.0 }

let private randomVesselType () =
    let types = [| ContainerShip; BulkCarrier; Passenger; Fishing |]
    types.[random.Next(types.Length)]

/// <summary>
/// Recursive simulation of a single vessels events. A new event is triggered when the first one in finished
/// </summary>
let rec private simulateVessel
    (vessel: SimulatedVessel)
    (portMap: (Guid * Port) array)
    (gateway: CommandGateway.CommandGateway)
    (vesselCount: int)
    (cts: CancellationToken)
    =
    task {
        try
            if cts.IsCancellationRequested then
                Log.Information("Vessel {VesselName} simulation stopped", vessel.Name)
                return ()
            else
                match vessel.State with
                | AtSea ->
                    let destinationPortId, destinationPortData = portMap.[random.Next(portMap.Length)]

                    Log.Information(
                        "{VesselName} creating route from ({StartLat}, {StartLon}) to port {PortName} ({PortId})",
                        vessel.Name,
                        vessel.CurrentPosition.Latitude,
                        vessel.CurrentPosition.Longitude,
                        destinationPortData.Name,
                        destinationPortId
                    )

                    let! waypoints =
                        (Command.Route.AStar.aStar vessel.CurrentPosition destinationPortData.Position)
                        |> AsyncResult.defaultWith (fun _ ->

                            Log.Error "FAILED TO GET SHORTEST PATH A*"
                            [||])



                    let routeInfo =
                        { RouteId = Guid.NewGuid()
                          DestinationPortId = destinationPortId
                          DestinationCoordinates = destinationPortData.Position
                          StartCoordinates = vessel.CurrentPosition
                          Waypoints = waypoints
                          CurrentWaypointIndex = 0
                          StartedAt = DateTimeOffset.UtcNow }

                    let! result =
                        gateway.UpdateOperationalStatus(
                            vessel.VesselId,
                            (OperationalStatus.InRoute routeInfo),
                            Some "Simulation"
                        )

                    match result with
                    | Ok _ ->
                        Log.Information("{VesselName} successfully created route", vessel.Name)

                        let inRouteVessel =
                            { vessel with
                                State = InRoute(destinationPortId, 0, 0) } // Will update waypoint count on first advance

                        // Start advancing immediately
                        do! Task.Delay(1000, cts)
                        return! simulateVessel inRouteVessel portMap gateway vesselCount cts
                    | Error err ->
                        Log.Warning("{VesselName} failed to create route: {Error}", vessel.Name, err)
                        // Wait and try again
                        do! Task.Delay(5000, cts)
                        return! simulateVessel vessel portMap gateway vesselCount cts

                | InRoute(destinationPortId, currentWaypoint, totalWaypoints) ->
                    // In route: advance to next waypoint
                    Log.Information(
                        "{VesselName} advancing waypoint {Current}/{Total}",
                        vessel.Name,
                        currentWaypoint + 1,
                        totalWaypoints
                    )

                    let! result = gateway.AdvanceRouteWaypoint(vessel.VesselId, Some "Simulation")

                    match result with
                    | Ok _ ->
                        Log.Information("{VesselName} successfully advanced waypoint", vessel.Name)

                        let updatedVessel =
                            { vessel with
                                State = InRoute(destinationPortId, currentWaypoint + 1, totalWaypoints) }

                        // Wait 4 seconds between advances
                        do! Task.Delay(2000, cts)
                        return! simulateVessel updatedVessel portMap gateway vesselCount cts
                    | Error err ->
                        // Check if we've reached the end of the route
                        if err.ToString().Contains("no more waypoints") then
                            Log.Information(
                                "{VesselName} reached end of route, attempting to dock at destination port",
                                vessel.Name
                            )

                            let! dockResult = gateway.StartDockingSaga(vessel.VesselId, Some "Simulation")

                            match dockResult with
                            | Ok sagaId ->
                                Log.Information(
                                    "{VesselName} successfully docked (Saga: {SagaId})",
                                    vessel.Name,
                                    sagaId
                                )

                                // Find the port data to get coordinates
                                let portData =
                                    portMap |> Array.find (fun (portId, _) -> portId = destinationPortId) |> snd

                                let dockedVessel =
                                    { vessel with
                                        State = Docked destinationPortId
                                        CurrentPosition = portData.Position }

                                // Wait while docked
                                do! Task.Delay(random.Next(5000, 15000), cts)
                                return! simulateVessel dockedVessel portMap gateway vesselCount cts
                            | Error dockErr ->
                                Log.Warning("{VesselName} failed to dock: {Error}", vessel.Name, dockErr)
                                // Return to sea and try again
                                let atSeaVessel = { vessel with State = AtSea }
                                do! Task.Delay(5000, cts)
                                return! simulateVessel atSeaVessel portMap gateway vesselCount cts
                        else
                            Log.Warning("{VesselName} failed to advance waypoint: {Error}", vessel.Name, err)
                            // Wait and try again
                            do! Task.Delay(4000, cts)
                            return! simulateVessel vessel portMap gateway vesselCount cts

                | Docked portId ->
                    // Docked: request departure
                    Log.Information("{VesselName} requesting departure from port {PortId}", vessel.Name, portId)

                    let! result = gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                    match result with
                    | Ok _ ->
                        Log.Information("{VesselName} successfully departed", vessel.Name)
                        let atSeaVessel = { vessel with State = AtSea }

                        // Wait before next operation
                        do! Task.Delay(random.Next(5000, 15000), cts)

                        return! simulateVessel atSeaVessel portMap gateway vesselCount cts
                    | Error err ->
                        Log.Warning("{VesselName} failed to depart: {Error}", vessel.Name, err)

                        // Wait and try again
                        do! Task.Delay(5000, cts)
                        return! simulateVessel vessel portMap gateway vesselCount cts
        with
        | :? OperationCanceledException ->
            Log.Information("Vessel {VesselName} simulation cancelled", vessel.Name)
            return ()
        | ex ->
            Log.Error(ex, "Error in vessel {VesselName} simulation", vessel.Name)
            return ()
    }

let private runSimulation (gateway: CommandGateway.CommandGateway) (vesselCount: int) (cts: CancellationToken) =
    task {
        try
            // Create ports
            Log.Information("Creating {PortCount} ports...", ports.Length)
            let rnd = System.Random()

            let! portMap =
                task {
                    let portTasks =
                        ports
                        |> List.sortBy (fun _ -> rnd.Next())
                        |> List.take 8
                        |> List.map (fun p ->
                            task {
                                let portId = Guid.NewGuid()

                                let! result =
                                    gateway.RegisterPort(
                                        portId,
                                        p.Name,
                                        None,
                                        "Simulation",
                                        p.Position,
                                        None,
                                        random.Next(2, 4),
                                        Some "Simulation"
                                    )

                                match result with
                                | Ok _ ->
                                    // Open the port
                                    let! openResult = gateway.OpenPort(portId, Some "Simulation")
                                    return Some(portId, p)
                                | Error err ->
                                    Log.Warning("Failed to create port: {Error}", err)
                                    return None
                            })

                    let! results = Task.WhenAll(portTasks)
                    return results |> Array.choose id
                }

            Log.Information("Created {Count} ports successfully", portMap.Length)

            if portMap.Length = 0 then
                Log.Error("No ports created, cannot start simulation")
                return ()
            else
                // Create and register vessels
                Log.Information("Creating {VesselCount} vessels...", vesselCount)

                let! vessels =
                    task {
                        let vesselTasks =

                            [ 1..vesselCount ]
                            |> List.map (fun v ->
                                task {
                                    let vesselId = Guid.NewGuid()
                                    let vesselName = randomName "Vessel"
                                    let initialPosition = randomPosition ()

                                    let! result =
                                        gateway.RegisterVessel(
                                            vesselId,
                                            vesselName,
                                            random.Next(100000000, 999999999),
                                            Some(random.Next(1000000, 9999999)),
                                            "SIM",
                                            initialPosition,
                                            Some(random.NextDouble() * 200.0 + 50.0),
                                            Some(random.NextDouble() * 30.0 + 10.0),
                                            Some(random.NextDouble() * 10.0 + 5.0),
                                            randomVesselType (),
                                            random.Next(10, 50),
                                            Some "Simulation"
                                        )

                                    match result with
                                    | Ok _ ->
                                        return
                                            Some
                                                { VesselId = vesselId
                                                  Name = vesselName
                                                  State = AtSea
                                                  CurrentPosition = initialPosition }
                                    | Error err ->
                                        Log.Warning("Failed to create vessel: {Error}", err)
                                        return None
                                })

                        let! results = Task.WhenAll(vesselTasks)
                        return results |> Array.choose id
                    }

                Log.Information("Created {Count} vessels successfully", vessels.Length)

                // Start vessel simulators
                Log.Information("Starting vessel simulators...")

                let simulationTasks =
                    vessels |> Array.map (fun v -> simulateVessel v portMap gateway vesselCount cts)

                let! _ = Task.WhenAll(simulationTasks) // :fire:

                Log.Information("Load simulation completed")
        with
        | :? OperationCanceledException -> Log.Information("Load simulation cancelled")
        | ex -> Log.Error(ex, "Error in load simulation")
    }

let mutable Current: (Task * CancellationTokenSource) option = None

let private startSimulation (gateway: CommandGateway.CommandGateway) (vesselCount: int) =
    let cts = new CancellationTokenSource()
    let simulationTask = runSimulation gateway vesselCount cts.Token
    (simulationTask, cts)


let simulationApi (ctx: HttpContext) : Shared.Api.Simulation.ISimulationApi =
    let querySession = ctx.GetService<IQuerySession>()

    { ExecuteSimulation =
        fun (vesselCount: int) ->
            asyncResult {
                let commandGateway = ctx.GetService<CommandGateway.CommandGateway>()
                // Make this an actor on its own?
                let task, cts = startSimulation commandGateway vesselCount

                Current <- Some(task, cts)

                return ()
            }
      StopSimulation =
        fun () ->
            asyncResult {
                match Current with
                | Some(_, cts) ->
                    cts.Cancel()
                    Current <- None
                    return ()
                | None -> return ()
            }
      GetPortStatistics = fun () -> Query.QueryHandlers.getPortStatistics querySession
      GetVesselStatistics = fun () -> Query.QueryHandlers.getVesselStatistics querySession }

let simulationHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext simulationApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
