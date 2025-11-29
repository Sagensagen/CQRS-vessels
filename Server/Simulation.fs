module Simulation

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Serilog
open Shared.Api.Vessel
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Giraffe
open Shared.Api.Simulation

type VesselState =
    | AtSea
    | Docked of portId: Guid

type SimulatedVessel =
    { VesselId: Guid
      Name: string
      State: VesselState }

type private Port =
    { Name: string
      Latitude: float
      Longitude: float }

let private random = Random()

let private randomName prefix =
    sprintf "%s-%d" prefix (random.Next(1000, 9999))

let private randomPosition () =
    { Latitude = (random.NextDouble() * 180.0) - 90.0
      Longitude = (random.NextDouble() * 360.0) - 180.0
      Timestamp = DateTimeOffset.UtcNow }

let private randomVesselType () =
    let types = [| ContainerShip; BulkCarrier; Passenger; Fishing |]
    types.[random.Next(types.Length)]


let rec private simulateVessel
    (vessel: SimulatedVessel)
    (ports: Guid array)
    (gateway: CommandGateway.CommandGateway)
    (config: SimulationConfig)
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
                    // At sea: randomly dock at a port
                    let portId = ports.[random.Next(ports.Length)]
                    Log.Information("{VesselName} requesting to dock at port {PortId}", vessel.Name, portId)

                    let! result = gateway.StartDockingSaga(vessel.VesselId, portId, Some "Simulation")

                    match result with
                    | Ok sagaId ->
                        Log.Information("{VesselName} successfully docked (Saga: {SagaId})", vessel.Name, sagaId)
                        let dockedVessel = { vessel with State = Docked portId }

                        // Wait while docked
                        do! Task.Delay(config.DockDurationMs, cts)

                        return! simulateVessel dockedVessel ports gateway config cts
                    | Error err ->
                        Log.Warning("{VesselName} failed to dock: {Error}", vessel.Name, err)

                        // Wait and try again
                        do! Task.Delay(config.OperationDelayMs, cts)
                        return! simulateVessel vessel ports gateway config cts

                | Docked portId ->
                    // Docked: request departure
                    Log.Information("{VesselName} requesting departure from port {PortId}", vessel.Name, portId)

                    let! result = gateway.StartUndockingSaga(vessel.VesselId, portId, Some "Simulation")

                    match result with
                    | Ok _ ->
                        Log.Information("{VesselName} successfully departed", vessel.Name)
                        let atSeaVessel = { vessel with State = AtSea }

                        // Wait before next operation
                        do! Task.Delay(config.OperationDelayMs, cts)

                        return! simulateVessel atSeaVessel ports gateway config cts
                    | Error err ->
                        Log.Warning("{VesselName} failed to depart: {Error}", vessel.Name, err)

                        // Wait and try again
                        do! Task.Delay(config.OperationDelayMs, cts)
                        return! simulateVessel vessel ports gateway config cts
        with
        | :? OperationCanceledException ->
            Log.Information("Vessel {VesselName} simulation cancelled", vessel.Name)
            return ()
        | ex ->
            Log.Error(ex, "Error in vessel {VesselName} simulation", vessel.Name)
            return ()
    }

let private runSimulation (gateway: CommandGateway.CommandGateway) (config: SimulationConfig) (cts: CancellationToken) =
    task {
        try
            Log.Information(
                "Starting load simulation with {VesselCount} vessels and {PortCount} ports",
                config.VesselCount,
                config.PortCount
            )

            // Create ports
            Log.Information("Creating {PortCount} ports...", config.PortCount)

            let! portIds =
                task {
                    let portTasks =
                        [| for i in 1 .. config.PortCount do
                               task {
                                   let portId = Guid.NewGuid()

                                   let! result =
                                       gateway.RegisterPort(
                                           portId,
                                           randomName "Port",
                                           None,
                                           "Simulation",
                                           (random.NextDouble() * 180.0) - 90.0,
                                           (random.NextDouble() * 360.0) - 180.0,
                                           None,
                                           random.Next(5, 20), // 5-20 docks per port
                                           Some "Simulation"
                                       )

                                   match result with
                                   | Ok _ ->
                                       // Open the port
                                       let! openResult = gateway.OpenPort(portId, Some "Simulation")
                                       return Some portId
                                   | Error err ->
                                       Log.Warning("Failed to create port: {Error}", err)
                                       return None
                               } |]

                    let! results = Task.WhenAll(portTasks)
                    return results |> Array.choose id
                }

            Log.Information("Created {Count} ports successfully", portIds.Length)

            if portIds.Length = 0 then
                Log.Error("No ports created, cannot start simulation")
                return ()
            else
                // Create and register vessels
                Log.Information("Creating {VesselCount} vessels...", config.VesselCount)

                let! vessels =
                    task {
                        let vesselTasks =
                            [| for i in 1 .. config.VesselCount do
                                   task {
                                       let vesselId = Guid.NewGuid()
                                       let vesselName = randomName "Vessel"

                                       let! result =
                                           gateway.RegisterVessel(
                                               vesselId,
                                               vesselName,
                                               random.Next(100000000, 999999999),
                                               Some(random.Next(1000000, 9999999)),
                                               "SIM",
                                               randomPosition (),
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
                                                     State = AtSea }
                                       | Error err ->
                                           Log.Warning("Failed to create vessel: {Error}", err)
                                           return None
                                   } |]

                        let! results = Task.WhenAll(vesselTasks)
                        return results |> Array.choose id
                    }

                Log.Information("Created {Count} vessels successfully", vessels.Length)

                // Start vessel simulators
                Log.Information("Starting vessel simulators...")

                let simulationTasks =
                    [| for vessel in vessels -> simulateVessel vessel portIds gateway config cts |]

                // Wait for all simulations to complete (or be cancelled)
                let! _ = Task.WhenAll(simulationTasks)

                Log.Information("Load simulation completed")
        with
        | :? OperationCanceledException -> Log.Information("Load simulation cancelled")
        | ex -> Log.Error(ex, "Error in load simulation")
    }

let private createDefaultConfig () =
    { VesselCount = 10
      PortCount = 3
      OperationDelayMs = 2000
      DockDurationMs = 5000 }

let private startSimulation (gateway: CommandGateway.CommandGateway) (config: SimulationConfig option) =
    let cfg = config |> Option.defaultValue (createDefaultConfig ())
    let cts = new CancellationTokenSource()

    let simulationTask = runSimulation gateway cfg cts.Token

    // Return cancellation token source so caller can stop simulation
    (simulationTask, cts)


let simulationApi (ctx: HttpContext) : Shared.Api.Simulation.ISimulationApi =
    { ExecuteSimulation =
        fun (config: SimulationConfig) ->
            asyncResult {
                let commandGateway = ctx.GetService<CommandGateway.CommandGateway>()
                do startSimulation commandGateway (Some config) |> ignore

                return ()
            } }

let simuationHandler: HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder RemotingHelpers.routeBuilder
    |> Remoting.fromContext simulationApi
    |> Remoting.withErrorHandler RemotingHelpers.errorHandler
    |> Remoting.buildHttpHandler
