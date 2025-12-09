module Query.QueryHandlers

open System
open System.Linq
open FsToolkit.ErrorHandling
open Marten
open Query.ReadModels
open Domain.VesselAggregate
open Domain.PortAggregate
open Shared.Api.Vessel
open Shared.Api.Port
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
            |> Seq.map (fun event ->
                match event.Data with
                | :? VesselRegisteredEvt ->
                    { Title = "Vessel registered"
                      Description = "Vessel was registered into the system"
                      EventType = Shared.Api.Shared.EventWrapperType.Success
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

                | :? VesselPositionUpdatedEvt as ev ->
                    { Title = "Position updated"
                      Description = $"{ev.Position.Latitude},{ev.Position.Longitude}"
                      EventType = EventWrapperType.Info
                      Inserted = event.Timestamp }

                | :? VesselOperationalStatusUpdatedEvt as ev ->
                    { Title = "Status updated"
                      Description = $"{ev.Status} - {ev.Activity}"
                      EventType = EventWrapperType.Info
                      Inserted = event.Timestamp }

                | :? VesselDecommissionedEvt as ev ->
                    { Title = "Vessel decommissioned"
                      Description = $"{ev.DecommissionedAt}"
                      EventType = EventWrapperType.Fail
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
            session
                .Query<PortReadModel>()
                .Where(fun p -> p.AvailableDocks > 0 && p.Status = PortStatus.Open)
                .OrderBy(fun p -> p.Name)
                .ToListAsync()
            |> Async.AwaitTask

        return ports |> Seq.toArray
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
