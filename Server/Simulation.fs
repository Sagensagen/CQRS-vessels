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
open Shared.Api.Cargo
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Shared.Api.Simulation

type private Port =
    { Id: Guid
      Name: string
      Position: LatLong }

let private ports: Port list =
    [ { Id = Guid.NewGuid()
        Name = "Port of Shanghai, China"
        Position = { Latitude = 31.22; Longitude = 121.48 } }
      { Id = Guid.NewGuid()
        Name = "Yangshan Port, China"
        Position =
          { Latitude = 30.6294
            Longitude = 122.0577 } }
      { Id = Guid.NewGuid()
        Name = "Port of Singapore, Singapore"
        Position =
          { Latitude = 1.264
            Longitude = 103.820 } }
      { Id = Guid.NewGuid()
        Name = "Port of Hong Kong, Hong Kong"
        Position =
          { Latitude = 22.317
            Longitude = 114.170 } }
      { Id = Guid.NewGuid()
        Name = "Port of Busan, South Korea"
        Position = { Latitude = 35.10; Longitude = 129.04 } }
      { Id = Guid.NewGuid()
        Name = "Port of Yokohama, Japan"
        Position = { Latitude = 35.27; Longitude = 139.38 } }
      { Id = Guid.NewGuid()
        Name = "Port of Los Angeles, USA"
        Position =
          { Latitude = 33.7428
            Longitude = -118.2614 } }
      { Id = Guid.NewGuid()
        Name = "Port of Long Beach, USA"
        Position =
          { Latitude = 33.75
            Longitude = -118.20 } }
      { Id = Guid.NewGuid()
        Name = "Port of Rotterdam, Netherlands"
        Position = { Latitude = 51.95; Longitude = 4.14 } }
      { Id = Guid.NewGuid()
        Name = "Port of Antwerp-Bruges, Belgium"
        Position = { Latitude = 51.22; Longitude = 4.40 } }
      { Id = Guid.NewGuid()
        Name = "Port of Barcelona, Spain"
        Position =
          { Latitude = 41.346176
            Longitude = 2.168365 } }
      { Id = Guid.NewGuid()
        Name = "Port of Gothenburg, Sweden"
        Position = { Latitude = 57.7; Longitude = 11.9 } }
      { Id = Guid.NewGuid()
        Name = "Port of Jebel Ali (Dubai), UAE"
        Position =
          { Latitude = 25.0033
            Longitude = 55.0521 } }
      { Id = Guid.NewGuid()
        Name = "Port of Djibouti, Djibouti"
        Position = { Latitude = 11.59; Longitude = 43.15 } }
      { Id = Guid.NewGuid()
        Name = "Port of Callao, Peru"
        Position =
          { Latitude = -12.05
            Longitude = -77.15 } }
      { Id = Guid.NewGuid()
        Name = "Port of Tianjin, China"
        Position = { Latitude = 39.0; Longitude = 117.7 } }
      { Id = Guid.NewGuid()
        Name = "Port of Shenzhen, China"
        Position = { Latitude = 22.5; Longitude = 114.0 } }
      { Id = Guid.NewGuid()
        Name = "Port of Qingdao, China"
        Position = { Latitude = 36.07; Longitude = 120.38 } }
      { Id = Guid.NewGuid()
        Name = "Port of Guangzhou, China"
        Position = { Latitude = 23.10; Longitude = 113.25 } }
      { Id = Guid.NewGuid()
        Name = "Port of Durban, South Africa"
        Position = { Latitude = -29.88; Longitude = 31.02 } }
      { Id = Guid.NewGuid()
        Name = "Port of Santos, Brazil"
        Position =
          { Latitude = -23.96
            Longitude = -46.33 } }
      { Id = Guid.NewGuid()
        Name = "Port of Buenos Aires, Argentina"
        Position = { Latitude = -34.6; Longitude = -58.4 } }
      { Id = Guid.NewGuid()
        Name = "Port of Valparaiso, Chile"
        Position =
          { Latitude = -33.05
            Longitude = -71.63 } }
      { Id = Guid.NewGuid()
        Name = "Port of Vancouver, Canada"
        Position = { Latitude = 49.3; Longitude = -123.1 } }
      { Id = Guid.NewGuid()
        Name = "Port of Seattle, USA"
        Position = { Latitude = 47.6; Longitude = -122.33 } }
      { Id = Guid.NewGuid()
        Name = "Port of Hamburg, Germany"
        Position = { Latitude = 53.55; Longitude = 9.99 } }
      { Id = Guid.NewGuid()
        Name = "Port of Marseille, France"
        Position = { Latitude = 43.3; Longitude = 5.37 } }
      { Id = Guid.NewGuid()
        Name = "Port of Piraeus, Greece"
        Position = { Latitude = 37.94; Longitude = 23.64 } }
      { Id = Guid.NewGuid()
        Name = "Port of Alexandria, Egypt"
        Position = { Latitude = 31.2; Longitude = 29.9 } }
      { Id = Guid.NewGuid()
        Name = "Port of Mombasa, Kenya"
        Position = { Latitude = -4.05; Longitude = 39.66 } }
      { Id = Guid.NewGuid()
        Name = "Port of Lagos (Apapa), Nigeria"
        Position = { Latitude = 6.45; Longitude = 3.40 } }
      { Id = Guid.NewGuid()
        Name = "Port of Colombo, Sri Lanka"
        Position = { Latitude = 6.93; Longitude = 79.85 } }
      { Id = Guid.NewGuid()
        Name = "Port of Karachi, Pakistan"
        Position =
          { Latitude = 24.835596
            Longitude = 66.981445 } }
      { Id = Guid.NewGuid()
        Name = "Port of Mumbai (Nhava Sheva), India"
        Position = { Latitude = 18.94; Longitude = 72.83 } }
      { Id = Guid.NewGuid()
        Name = "Port of Ho Chi Minh City, Vietnam"
        Position = { Latitude = 10.82; Longitude = 106.63 } }
      { Id = Guid.NewGuid()
        Name = "Port of Jakarta (Tanjung Priok), Indonesia"
        Position = { Latitude = -6.12; Longitude = 106.85 } }
      { Id = Guid.NewGuid()
        Name = "Port of Manila, Philippines"
        Position = { Latitude = 14.58; Longitude = 120.97 } }
      { Id = Guid.NewGuid()
        Name = "Port of Sydney, Australia"
        Position =
          { Latitude = -33.86
            Longitude = 151.20 } }
      { Id = Guid.NewGuid()
        Name = "Port of Melbourne, Australia"
        Position =
          { Latitude = -37.82
            Longitude = 144.96 } }
      { Id = Guid.NewGuid()
        Name = "Port of Genoa, Italy"
        Position = { Latitude = 44.40; Longitude = 8.93 } }
      { Id = Guid.NewGuid()
        Name = "Port of Trieste, Italy"
        Position = { Latitude = 45.65; Longitude = 13.76 } }
      { Id = Guid.NewGuid()
        Name = "Port of Istanbul, Turkey"
        Position = { Latitude = 41.0; Longitude = 28.97 } }
      { Id = Guid.NewGuid()
        Name = "Port of Haifa, Israel"
        Position = { Latitude = 32.82; Longitude = 34.99 } }
      { Id = Guid.NewGuid()
        Name = "Port of Beirut, Lebanon"
        Position = { Latitude = 33.90; Longitude = 35.52 } }
      { Id = Guid.NewGuid()
        Name = "Port of Montreal, Canada"
        Position = { Latitude = 45.50; Longitude = -73.55 } }
      { Id = Guid.NewGuid()
        Name = "Port of New York/New Jersey, USA"
        Position = { Latitude = 40.67; Longitude = -74.04 } }
      { Id = Guid.NewGuid()
        Name = "Port of Savannah, USA"
        Position = { Latitude = 32.08; Longitude = -81.10 } }
      { Id = Guid.NewGuid()
        Name = "Port of Houston, USA"
        Position = { Latitude = 29.73; Longitude = -95.27 } }
      { Id = Guid.NewGuid()
        Name = "Port of Auckland, New Zealand"
        Position =
          { Latitude = -36.84
            Longitude = 174.76 } }
      { Id = Guid.NewGuid()
        Name = "Port of Wellington, New Zealand"
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
    // Generate position within navigable network bounds (-60° to 76.5° latitude)
    let latRange = NavigationBounds.MaxLatitude - NavigationBounds.MinLatitude
    let lonRange = NavigationBounds.MaxLongitude - NavigationBounds.MinLongitude

    { Latitude = NavigationBounds.MinLatitude + (random.NextDouble() * latRange)
      Longitude = NavigationBounds.MinLongitude + (random.NextDouble() * lonRange) }

let private randomVesselType () =
    let types = [| ContainerShip; BulkCarrier; Passenger; Fishing |]
    types[random.Next(types.Length)]

let private randomCargoSpec () =
    let containerTypes = [| TEU20; FEU40 |]
    let containerType = containerTypes[random.Next(containerTypes.Length)]
    let containerCount = random.Next(5, 21) // 5-20 containers

    // Weight ranges from 5,000 to 50,000 kg
    let weight = (random.NextDouble() * 45000.0 + 5000.0) * 1.0<kg>

    // Volume ranges from 20 to 200 m³
    let volume = (random.NextDouble() * 180.0 + 20.0) * 1.0<m^3>

    { CargoType = Containerized
      TotalWeight = weight
      TotalVolume = volume
      ContainerLoad =
        { ContainerType = containerType
          Count = containerCount } }

/// <summary>
/// Recursive simulation of a single vessels' event. A new event is triggered when the first one is finished
/// </summary>
let rec private simulateVessel
    (vessel: SimulatedVessel)
    (portMap: (Guid * Port) array)
    (gateway: CommandGateway.CommandGateway)
    (querySession: IQuerySession)
    (simConfig: ExecuteSimConfig)
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
                    let destinationPortId, destinationPortData = portMap[random.Next(portMap.Length)]

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

                    if waypoints.Length = 0 then
                        Log.Warning(
                            "A* RETURNED EMPTY ROUTE for {VesselName} at position ({Lat}, {Lon}). Relocating vessel to valid position.",
                            vessel.Name,
                            vessel.CurrentPosition.Latitude,
                            vessel.CurrentPosition.Longitude
                        )

                        // Generate a new valid position and update the vessel
                        let newPosition = randomPosition ()

                        let! updateResult =
                            gateway.UpdateVesselPosition(vessel.VesselId, newPosition, Some "Simulation")

                        match updateResult with
                        | Ok _ ->
                            Log.Information(
                                "{VesselName} relocated to ({Lat}, {Lon})",
                                vessel.Name,
                                newPosition.Latitude,
                                newPosition.Longitude
                            )

                            let relocatedVessel =
                                { vessel with
                                    CurrentPosition = newPosition }

                            do! Task.Delay(2000, cts)
                            return! simulateVessel relocatedVessel portMap gateway querySession simConfig cts
                        | Error err ->
                            Log.Error("{VesselName} failed to relocate: {Error}", vessel.Name, err)
                            do! Task.Delay(5000, cts)
                            return! simulateVessel vessel portMap gateway querySession simConfig cts
                    else
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
                                    State = InRoute(destinationPortId, 0, routeInfo.Waypoints.Length) } // Will update waypoint count on the first advance

                            // Start advancing immediately
                            do! Task.Delay(1000, cts)
                            return! simulateVessel inRouteVessel portMap gateway querySession simConfig cts
                        | Error err ->
                            Log.Warning("{VesselName} failed to create route: {Error}", vessel.Name, err)
                            // Wait and try again
                            do! Task.Delay(5000, cts)
                            return! simulateVessel vessel portMap gateway querySession simConfig cts

                | InRoute(destinationPortId, currentWaypoint, totalWaypoints) ->
                    // In route: advance to the next waypoint


                    let! result = gateway.AdvanceRouteWaypoint(vessel.VesselId, Some "Simulation")

                    match result with
                    | Ok _ ->
                        Log.Information(
                            "{VesselName} advanced waypoint {Current}/{Total}",
                            vessel.Name,
                            currentWaypoint + 1,
                            totalWaypoints
                        )

                        let updatedVessel =
                            { vessel with
                                State = InRoute(destinationPortId, currentWaypoint + 1, totalWaypoints) }

                        // Wait 4 seconds between advances
                        do! Task.Delay(2000, cts)
                        return! simulateVessel updatedVessel portMap gateway querySession simConfig cts
                    | Error VesselCommandErrors.NoMoreWaypoints ->
                        let! dockResult = gateway.StartDockingSaga(vessel.VesselId, Some "Simulation")

                        match dockResult with
                        | Ok sagaId ->
                            Log.Information("{VesselName} successfully docked (Saga: {SagaId})", vessel.Name, sagaId)

                            // Find the port data to get coordinates
                            let portData =
                                portMap |> Array.find (fun (portId, _) -> portId = destinationPortId) |> snd

                            let dockedVessel =
                                { vessel with
                                    State = Docked destinationPortId
                                    CurrentPosition = portData.Position }

                            // Wait while docked
                            do! Task.Delay(random.Next(5000, 10000), cts)
                            return! simulateVessel dockedVessel portMap gateway querySession simConfig cts
                        | Error dockErr ->
                            Log.Warning("{VesselName} failed to dock: {Error}", vessel.Name, dockErr)
                            // Return to sea and try again
                            let atSeaVessel = { vessel with State = AtSea }
                            do! Task.Delay(5000, cts)
                            return! simulateVessel atSeaVessel portMap gateway querySession simConfig cts
                    | _ ->
                        Log.Warning("{VesselName} failed to advance waypoint: {Error}", vessel.Name, result)
                        // Wait and try again
                        do! Task.Delay(4000, cts)
                        return! simulateVessel vessel portMap gateway querySession simConfig cts

                | Docked portId ->
                    Log.Information("{VesselName} docked at port {PortId}, checking for cargo...", vessel.Name, portId)

                    // Check if a vessel has cargo to unload
                    let! vesselState = gateway.GetVesselState(vessel.VesselId)

                    match vesselState with
                    | Ok(Some state) when state.CurrentCargo.IsSome ->
                        // Vessel has cargo, check if this is the destination
                        let cargo = state.CurrentCargo.Value

                        if cargo.DestinationPortId = portId then
                            Log.Information(
                                "{VesselName} unloading cargo {CargoId} at destination port {PortId}",
                                vessel.Name,
                                cargo.CargoId,
                                portId
                            )

                            let! unloadResult =
                                gateway.UnloadCargoFromVessel(
                                    cargo.CargoId,
                                    vessel.VesselId,
                                    portId,
                                    true,
                                    Some "Simulation"
                                )

                            match unloadResult with
                            | Ok _ ->
                                Log.Information("{VesselName} successfully unloaded cargo", vessel.Name)
                                do! Task.Delay(3000, cts)
                                // After unloading, depart
                                let! departResult =
                                    gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                                match departResult with
                                | Ok _ ->
                                    Log.Information("{VesselName} departed after unloading", vessel.Name)
                                    let atSeaVessel = { vessel with State = AtSea }
                                    do! Task.Delay(random.Next(5000, 15000), cts)
                                    return! simulateVessel atSeaVessel portMap gateway querySession simConfig cts
                                | Error err ->
                                    Log.Warning("{VesselName} failed to depart after unload: {Error}", vessel.Name, err)
                                    do! Task.Delay(5000, cts)
                                    return! simulateVessel vessel portMap gateway querySession simConfig cts
                            | Error err ->
                                Log.Warning("{VesselName} failed to unload cargo: {Error}", vessel.Name, err)
                                do! Task.Delay(5000, cts)
                                return! simulateVessel vessel portMap gateway querySession simConfig cts
                        else
                            // Wrong port shouldn't happen with validation but handle it
                            Log.Warning(
                                "{VesselName} at wrong port {CurrentPort}, cargo destination is {DestPort}",
                                vessel.Name,
                                portId,
                                cargo.DestinationPortId
                            )

                            do! Task.Delay(5000, cts)
                            return! simulateVessel vessel portMap gateway querySession simConfig cts
                    | _ ->
                        // No cargo on a vessel, try to find and load cargo
                        let! cargoResult = Query.QueryHandlers.getAvailableCargoAtPort portId querySession

                        match cargoResult with
                        | Ok cargos when cargos.Length > 0 ->
                            // Found cargo, load the first one
                            let cargo = cargos[0]

                            Log.Information(
                                "{VesselName} found cargo {CargoId} at port, loading...",
                                vessel.Name,
                                cargo.Id
                            )

                            let! loadResult =
                                gateway.LoadCargoOntoVessel(
                                    cargo.Id,
                                    vessel.VesselId,
                                    portId,
                                    cargo.Spec,
                                    Some "Simulation"
                                )

                            match loadResult with
                            | Ok _ ->
                                Log.Information(
                                    "{VesselName} successfully loaded cargo {CargoId}, creating route to destination {DestPort}",
                                    vessel.Name,
                                    cargo.Id,
                                    cargo.DestinationPortId
                                )

                                // Wait a bit after loading
                                do! Task.Delay(2000, cts)

                                // Find destination port
                                let destPortOpt =
                                    portMap |> Array.tryFind (fun (pid, _) -> pid = cargo.DestinationPortId)

                                match destPortOpt with
                                | Some(destPortId, destPort) ->
                                    // Get the current port position
                                    let currentPortData = portMap |> Array.find (fun (pid, _) -> pid = portId) |> snd

                                    // Create route to cargo destination
                                    let! waypoints =
                                        (Command.Route.AStar.aStar currentPortData.Position destPort.Position)
                                        |> AsyncResult.defaultWith (fun _ ->
                                            Log.Error "Failed to create route to cargo destination"
                                            [||])

                                    if waypoints.Length > 0 then
                                        let routeInfo =
                                            { RouteId = Guid.NewGuid()
                                              DestinationPortId = destPortId
                                              DestinationCoordinates = destPort.Position
                                              StartCoordinates = currentPortData.Position
                                              Waypoints = waypoints
                                              CurrentWaypointIndex = 0
                                              StartedAt = DateTimeOffset.UtcNow }

                                        // Depart and start route
                                        let! departResult =
                                            gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                                        match departResult with
                                        | Ok _ ->
                                            let! routeResult =
                                                gateway.UpdateOperationalStatus(
                                                    vessel.VesselId,
                                                    (OperationalStatus.InRoute routeInfo),
                                                    Some "Simulation"
                                                )

                                            match routeResult with
                                            | Ok _ ->
                                                Log.Information(
                                                    "{VesselName} departed with cargo, en route to {DestPort}",
                                                    vessel.Name,
                                                    destPort.Name
                                                )

                                                let inRouteVessel =
                                                    { vessel with
                                                        State = InRoute(destPortId, 0, routeInfo.Waypoints.Length)
                                                        CurrentPosition = currentPortData.Position }

                                                do! Task.Delay(1000, cts)

                                                return!
                                                    simulateVessel
                                                        inRouteVessel
                                                        portMap
                                                        gateway
                                                        querySession
                                                        simConfig
                                                        cts
                                            | Error err ->
                                                Log.Warning(
                                                    "{VesselName} failed to start route: {Error}",
                                                    vessel.Name,
                                                    err
                                                )

                                                do! Task.Delay(5000, cts)

                                                return! simulateVessel vessel portMap gateway querySession simConfig cts
                                        | Error err ->
                                            Log.Warning(
                                                "{VesselName} failed to depart with cargo: {Error}",
                                                vessel.Name,
                                                err
                                            )

                                            do! Task.Delay(5000, cts)
                                            return! simulateVessel vessel portMap gateway querySession simConfig cts
                                    else
                                        Log.Warning(
                                            "{VesselName} could not create route to cargo destination",
                                            vessel.Name
                                        )

                                        do! Task.Delay(5000, cts)
                                        return! simulateVessel vessel portMap gateway querySession simConfig cts
                                | None ->
                                    Log.Warning(
                                        "{VesselName} cargo destination port not found in simulation",
                                        vessel.Name
                                    )

                                    do! Task.Delay(5000, cts)
                                    return! simulateVessel vessel portMap gateway querySession simConfig cts
                            | Error err ->
                                Log.Warning("{VesselName} failed to load cargo: {Error}", vessel.Name, err)
                                do! Task.Delay(5000, cts)
                                // No cargo or failed to load, just depart
                                let! departResult =
                                    gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                                match departResult with
                                | Ok _ ->
                                    let atSeaVessel = { vessel with State = AtSea }
                                    do! Task.Delay(random.Next(5000, 15000), cts)
                                    return! simulateVessel atSeaVessel portMap gateway querySession simConfig cts
                                | Error err ->
                                    Log.Warning("{VesselName} failed to depart: {Error}", vessel.Name, err)
                                    do! Task.Delay(5000, cts)
                                    return! simulateVessel vessel portMap gateway querySession simConfig cts
                        | _ ->
                            // No cargo available, just depart
                            Log.Information("{VesselName} no cargo available at port, departing", vessel.Name)
                            let! departResult = gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                            match departResult with
                            | Ok _ ->
                                Log.Information("{VesselName} successfully departed", vessel.Name)
                                let atSeaVessel = { vessel with State = AtSea }
                                do! Task.Delay(random.Next(5000, 15000), cts)
                                return! simulateVessel atSeaVessel portMap gateway querySession simConfig cts
                            | Error err ->
                                Log.Warning("{VesselName} failed to depart: {Error}", vessel.Name, err)
                                do! Task.Delay(5000, cts)
                                return! simulateVessel vessel portMap gateway querySession simConfig cts
        with
        | :? OperationCanceledException ->
            Log.Information("Vessel {VesselName} simulation cancelled", vessel.Name)
            return ()
        | ex ->
            Log.Error(ex, "Error in vessel {VesselName} simulation", vessel.Name)
            return ()
    }

let private runSimulation
    querySession
    (gateway: CommandGateway.CommandGateway)
    (simConfig: ExecuteSimConfig)
    (cts: CancellationToken)
    =
    task {
        try
            // Create ports
            let rnd = Random()

            let selectedPorts =
                ports |> List.sortBy (fun _ -> rnd.Next()) |> List.take simConfig.PortCount

            Log.Information("Creating {PortCount} ports...", selectedPorts.Length)

            let! portMap =
                task {
                    let portTasks =
                        selectedPorts
                        |> List.map (fun port ->
                            task {
                                let! result =
                                    gateway.RegisterPort(
                                        port.Id,
                                        port.Name,
                                        None,
                                        "Simulation",
                                        port.Position,
                                        None,
                                        random.Next(2, 4),
                                        Some "Simulation"
                                    )

                                match result with
                                | Ok _ ->
                                    // Open the port
                                    let! _ = gateway.OpenPort(port.Id, Some "Simulation")

                                    // Create 5-10 random cargo items for this port
                                    let cargoCount = random.Next(5, 11)

                                    Log.Information(
                                        "Creating {CargoCount} cargo items for port {PortName}",
                                        cargoCount,
                                        port.Name
                                    )

                                    // Move to higher order function?
                                    for i in 1..cargoCount do
                                        let cargoSpec = randomCargoSpec ()

                                        let randomDestination =
                                            selectedPorts
                                            |> List.filter (fun p -> p.Id <> port.Id)
                                            |> List.randomChoice

                                        let! cargoRes =
                                            gateway.CreateCargo(
                                                Guid.NewGuid(),
                                                cargoSpec,
                                                port.Id,
                                                randomDestination.Id,
                                                None
                                            )

                                        match cargoRes with
                                        | Ok _ ->
                                            Log.Debug(
                                                "Created cargo {Index}/{Total} at port {PortName}: {Weight}kg, {Volume}m³",
                                                i,
                                                cargoCount,
                                                port.Name,
                                                cargoSpec.TotalWeight,
                                                cargoSpec.TotalVolume
                                            )
                                        | Error err ->
                                            Log.Warning(
                                                "Failed to create cargo {Index} for port {PortName}: {Error}",
                                                i,
                                                port.Name,
                                                err
                                            )

                                    return Some(port.Id, port)

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
                Log.Information("Creating {VesselCount} vessels...", simConfig.VesselCount)

                let! vessels =
                    task {
                        let vesselTasks =

                            [ 1 .. simConfig.VesselCount ]
                            |> List.map (fun _ ->
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
                    vessels
                    |> Array.map (fun v -> simulateVessel v portMap gateway querySession simConfig cts)

                let! _ = Task.WhenAll(simulationTasks) // :fire:

                Log.Information("Load simulation completed")
        with
        | :? OperationCanceledException -> Log.Information("Load simulation cancelled")
        | ex -> Log.Error(ex, "Error in load simulation")
    }

let mutable Current: (Task * CancellationTokenSource) option = None

let private startSimulation querySession (gateway: CommandGateway.CommandGateway) (simConfig: ExecuteSimConfig) =
    let cts = new CancellationTokenSource()
    let simulationTask = runSimulation querySession gateway simConfig cts.Token
    (simulationTask, cts)


let simulationApi (ctx: HttpContext) : ISimulationApi =
    let querySession = ctx.GetService<IQuerySession>()

    { ExecuteSimulation =
        fun (simConfig: ExecuteSimConfig) ->
            asyncResult {
                let commandGateway = ctx.GetService<CommandGateway.CommandGateway>()
                // Make this an actor on its own?
                let task, cts = startSimulation querySession commandGateway simConfig

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
      GetVesselStatistics = fun () -> Query.QueryHandlers.getVesselStatistics querySession
      GetCargoStatistics = fun () -> Query.QueryHandlers.getCargoStatistics querySession }

let simulationHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext simulationApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
