module Query.QueryHandlers

open System
open System.Linq
open FsToolkit.ErrorHandling
open Marten
open Query.ReadModels
open Domain.VesselAggregate
open Domain.PortAggregate
open Domain.CargoAggregate
open Shared.Api.Vessel
open Shared.Api.Port
open Shared.Api.Cargo
open Shared.Api.Shared


let getVessel (vesselId: Guid) (session: IQuerySession) =
    asyncResult {
        let! vessel =
            session.LoadAsync<VesselReadModel> vesselId
            |> Async.AwaitTask
            |> Async.map (fun v -> if obj.ReferenceEquals(v, null) then None else Some v)
            |> AsyncResult.requireSome VesselQueryErrors.VesselNotFound

        return vessel
    }

let getAllVessels (session: IQuerySession) =
    asyncResult {
        let! vessels =
            session.Query<VesselReadModel>().OrderBy(fun v -> v.Name).ToListAsync()
            |> Async.AwaitTask

        return vessels |> Seq.toArray
    }

let getVesselsByPort (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! vessels =
            session.Query<VesselReadModel>().Where(fun v -> v.CurrentPortId = Some portId).ToListAsync()
            |> Async.AwaitTask

        return vessels |> Seq.toArray
    }

let getVesselEvents (vesselId: Guid) (session: IQuerySession) =
    asyncResult {
        let! events = session.Events.FetchStreamAsync(vesselId) |> Async.AwaitTask

        let mapped =
            events
            |> Seq.filter (fun event ->
                match event.Data with
                | :? VesselPositionUpdatedEvt -> false
                | :? VesselOperationalStatusUpdatedEvt as evt -> not evt.Status.IsInRoute
                | _ -> true)
            |> Seq.map (fun event ->
                match event.Data with
                | :? VesselRegisteredEvt ->
                    { Title = "Vessel registered"
                      Description = "Vessel was registered into the system"
                      EventType = EventWrapperType.Success
                      Inserted = event.Timestamp }

                | :? VesselDepartedEvt as ev ->
                    { Title = "Departed from port"
                      Description = ev.FromPortId.ToString()
                      EventType = EventWrapperType.Info
                      Inserted = event.Timestamp }

                | :? VesselArrivedEvt as ev ->
                    { Title = "Arrived at port"
                      Description = ev.PortId.ToString()
                      EventType = EventWrapperType.Success
                      Inserted = event.Timestamp }

                // SKIP THIS AS IT POLLUTES EVENT HISTORY QUITE ALOT
                // | :? VesselPositionUpdatedEvt as ev ->
                //     { Title = "Position updated"
                //       Description = $"{ev.Position.Latitude},{ev.Position.Longitude}"
                //       EventType = EventWrapperType.Info
                //       Inserted = event.Timestamp }

                | :? VesselOperationalStatusUpdatedEvt as ev ->
                    { Title = "Status updated"
                      Description =
                        match ev.Status with
                        | AtSea -> $"{ev.Status}"
                        | InRoute route ->
                            // +1 for index relative to the length
                            $"InRoute: towards port {route.DestinationPortId}, {route.CurrentWaypointIndex + 1}/{route.Waypoints.Length} steps"
                        | Docked _port -> $"{ev.Status}"
                        | Anchored _pos -> $"{ev.Status}"
                        | UnderMaintenance -> $"{ev.Status}"
                        | Decommissioned -> $"{ev.Status}"

                      EventType = EventWrapperType.Info
                      Inserted = event.Timestamp }

                | :? VesselDecommissionedEvt as ev ->
                    { Title = "Vessel decommissioned"
                      Description = $"{ev.DecommissionedAt}"
                      EventType = EventWrapperType.Fail
                      Inserted = event.Timestamp }

                | :? CargoLoadedEvt as ev ->
                    { Title = "Cargo loaded"
                      Description = $"Cargo {ev.CargoId} loaded"
                      EventType = EventWrapperType.Success
                      Inserted = event.Timestamp }

                | :? CargoUnloadedEvt as ev ->
                    { Title = "Cargo unloaded"
                      Description = $"Cargo {ev.CargoId} unloaded"
                      EventType = EventWrapperType.Success
                      Inserted = event.Timestamp }

                | _ ->
                    { Title = "Unknown event"
                      Description = event.Data.GetType().Name
                      EventType = EventWrapperType.Fail
                      Inserted = event.Timestamp })

        return mapped
    }


let getPort (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! port =
            session.LoadAsync<PortReadModel> portId
            |> Async.AwaitTask
            |> Async.map (fun p -> if obj.ReferenceEquals(p, null) then None else Some p)
            |> AsyncResult.requireSome "Port not found"

        return port
    }

let getAllPorts (session: IQuerySession) =
    asyncResult {
        let! ports =
            session.Query<PortReadModel>().OrderBy(fun p -> p.Name).ToListAsync()
            |> Async.AwaitTask

        return ports |> Seq.toArray
    }

let getPortsWithAvailability (session: IQuerySession) =
    asyncResult {
        let! ports =
            session.Query<PortReadModel>().Where(fun p -> p.AvailableDocks > 0).OrderBy(_.Name).ToListAsync()
            |> Async.AwaitTask

        let openPorts =
            ports
            |> Seq.filter (fun c ->
                match c.Status with
                | PortStatus.Open -> true
                | _ -> false)
            |> Seq.toArray

        return openPorts |> Seq.toArray
    }

let getPortEvents (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! events = session.Events.FetchStreamAsync(portId) |> Async.AwaitTask

        let mapped =
            events
            |> Seq.map (fun event ->
                let (title, description) =
                    match event.Data with
                    | :? PortRegisteredEvt as ev -> ("Port registered", $"{ev.Name} in {ev.Country}")

                    | :? VesselDockingReservedEvt as ev -> ("Docking reserved", $"Vessel {ev.VesselId}")

                    | :? DockingConfirmedEvt as ev -> ("Docking confirmed", $"Vessel {ev.VesselId}")

                    | :? DockingReservationExpiredEvt as ev -> ("Reservation expired", $"Vessel {ev.VesselId}")

                    | :? VesselUndockedEvt as ev -> ("Vessel undocked", $"Vessel {ev.VesselId}")

                    | :? PortOpenedEvt -> ("Port opened", "Port is now accepting vessels")

                    | :? PortClosedEvt -> ("Port closed", "Port is no longer accepting vessels")

                    | _ -> ("Unknown event", event.Data.GetType().Name)

                { Title = title
                  Description = description
                  EventType = EventWrapperType.Info
                  Inserted = event.Timestamp })

        return mapped
    }

let getVesselStatistics (session: IQuerySession) =
    asyncResult {
        let! vessels = session.Query<VesselReadModel>().ToListAsync() |> Async.AwaitTask

        let total = vessels.Count

        let atSea =
            vessels |> Seq.filter (fun v -> v.State = OperationalStatus.AtSea) |> Seq.length

        let docked =
            vessels
            |> Seq.filter (fun v ->
                match v.State with
                | OperationalStatus.Docked _ -> true
                | _ -> false)
            |> Seq.length

        let decommissioned =
            vessels
            |> Seq.filter (fun v -> v.State = OperationalStatus.Decommissioned)
            |> Seq.length

        let stats =
            { Total = total
              AtSea = atSea
              Docked = docked
              Decommissioned = decommissioned
              Active = total - decommissioned }
            : Shared.Api.Simulation.VesselStatistics

        return stats
    }

let getPortStatistics (session: IQuerySession) : Async<Result<Shared.Api.Simulation.PortStatistics, string>> =
    asyncResult {
        let! ports = session.Query<PortReadModel>().ToListAsync() |> Async.AwaitTask

        let total = ports.Count
        let open' = ports |> Seq.filter (fun p -> p.Status = PortStatus.Open) |> Seq.length

        let closed =
            ports |> Seq.filter (fun p -> p.Status = PortStatus.Closed) |> Seq.length

        let totalDocks = ports |> Seq.sumBy _.MaxDocks
        let occupiedDocks = ports |> Seq.sumBy _.CurrentDocked

        let stats: Shared.Api.Simulation.PortStatistics =
            { Total = total
              Open = open'
              Closed = closed
              AvailableDocks = totalDocks - occupiedDocks
              TotalDocks = totalDocks
              OccupiedDocks = occupiedDocks
              OccupancyRate =
                if totalDocks > 0 then
                    (float occupiedDocks / float totalDocks) * 100.0
                else
                    0.0 }

        return stats
    }

let getCargoStatistics (session: IQuerySession) : Async<Result<Shared.Api.Simulation.CargoStatistics, string>> =
    asyncResult {
        let! cargo = session.Query<CargoReadModel>().ToListAsync() |> Async.AwaitTask

        let total = cargo.Count

        let awaitingPickup =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.AwaitingPickup -> true
                | _ -> false)
            |> Seq.length

        let reserved =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.ReservedForVessel _ -> true
                | _ -> false)
            |> Seq.length

        let loadedOnVessel =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.LoadedOnVessel _ -> true
                | _ -> false)
            |> Seq.length

        let inTransit =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.InTransit _ -> true
                | _ -> false)
            |> Seq.length

        let delivered =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.Delivered -> true
                | _ -> false)
            |> Seq.length

        let cancelled =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.Cancelled -> true
                | _ -> false)
            |> Seq.length

        let stats: Shared.Api.Simulation.CargoStatistics =
            { Total = total
              AwaitingPickup = awaitingPickup
              Reserved = reserved
              LoadedOnVessel = loadedOnVessel
              InTransit = inTransit
              Delivered = delivered
              Cancelled = cancelled }

        return stats
    }

let getCargo (cargoId: Guid) (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session.LoadAsync<CargoReadModel> cargoId
            |> Async.AwaitTask
            |> Async.map (fun c -> if obj.ReferenceEquals(c, null) then None else Some c)
            |> AsyncResult.requireSome CargoQueryErrors.CargoNotFound

        return cargo
    }

let getAllCargo (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session.Query<CargoReadModel>().OrderBy(fun c -> c.CreatedAt).ToListAsync()
            |> Async.AwaitTask

        return cargo |> Seq.toArray
    }

let getCargoByPort (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session
                .Query<CargoReadModel>()
                .Where(fun c -> c.OriginPortId = portId || c.DestinationPortId = portId)
                .ToListAsync()
            |> Async.AwaitTask

        return cargo |> Seq.toArray
    }

let getCargoByVessel (vesselId: Guid) (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session.Query<CargoReadModel>().Where(fun c -> c.CurrentVesselId = Some vesselId).ToListAsync()
            |> Async.AwaitTask

        return cargo |> Seq.toArray
    }

let getCargoEvents (cargoId: Guid) (session: IQuerySession) =
    asyncResult {
        let! events = session.Events.FetchStreamAsync(cargoId) |> Async.AwaitTask

        let mapped =
            events
            |> Seq.map (fun event ->
                let title, description =
                    match event.Data with
                    | :? CargoCreatedEvt as ev ->
                        ("Cargo created", $"From {ev.OriginPortId} to {ev.DestinationPortId}")
                    | :? CargoLoadedOnVesselEvt as ev -> ("Loaded on vessel", $"Vessel {ev.VesselId}")
                    | :? CargoMarkedInTransitEvt -> ("In transit", "Cargo is now in transit")
                    | :? CargoUnloadedFromVesselEvt as ev -> ("Unloaded at port", $"Port {ev.PortId}")
                    | :? CargoDeliveredEvt -> ("Delivered", "Cargo delivered to destination")
                    | :? CargoCancelledEvt -> ("Cancelled", "Cargo was cancelled")
                    | _ -> ("Unknown event", event.Data.GetType().Name)

                { Title = title
                  Description = description
                  EventType = EventWrapperType.Info
                  Inserted = event.Timestamp })

        return mapped
    }

let getAvailableCargoAtPort (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session.Query<CargoReadModel>().Where(fun c -> c.OriginPortId = portId).ToListAsync()
            |> Async.AwaitTask

        // Filter in memory after loading
        let availableCargo =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.AwaitingPickup -> true
                | _ -> false)
            |> Seq.toArray

        return availableCargo
    }

/// Get all reserved cargo at a specific port with reservation details
let getReservedCargoAtPort (portId: Guid) (session: IQuerySession) =
    asyncResult {
        let! cargo =
            session.Query<CargoReadModel>().Where(fun c -> c.OriginPortId = portId).ToListAsync()
            |> Async.AwaitTask

        // Filter in memory after loading
        let reservedCargo =
            cargo
            |> Seq.filter (fun c ->
                match c.Status with
                | CargoStatus.ReservedForVessel _ -> true
                | _ -> false)
            |> Seq.toArray

        return reservedCargo
    }
