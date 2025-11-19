module Query

open System
open Domain.Vessel
open FsToolkit.ErrorHandling
open Marten
open Shared.Api.Vessel

let getVessel (vesselId: Guid) (session: IQuerySession) =
    asyncResult {
        let! vessel =
            session.LoadAsync<VesselView> vesselId
            |> Async.AwaitTask
            |> Async.map (fun v -> if obj.ReferenceEquals(v, null) then None else Some v)
            |> AsyncResult.requireSome VesselQueryErrors.VesselNotFound

        return vessel
    }

let getAllVessels (session: IQuerySession) =
    asyncResult {
        let! vessels = session.Query<VesselView>().ToListAsync()
        return vessels |> Seq.toArray
    }

let getVesselEvents (id: Guid) (session: IQuerySession) =
    asyncResult {
        let! events = session.Events.FetchStreamAsync(id)

        let mapped =
            events
            |> Seq.map (fun event ->
                match event.Data with
                | :? Event.VesselRegistered as _ ->
                    { Title = "Vessel registed"
                      Description = "Vessel was registerd into the system"
                      EventType = VesselEventType.Success
                      Inserted = event.Timestamp }
                | :? Event.VesselDeparted as ev ->
                    { Title = "Departed from port"
                      Description = ev.FromPort
                      EventType = VesselEventType.Info
                      Inserted = event.Timestamp }
                | :? Event.VesselArrived as ev ->
                    { Title = "Arrived at port"
                      Description = ev.AtPort
                      EventType = VesselEventType.Success
                      Inserted = event.Timestamp }
                | :? Event.VesselLoadingStarted as ev ->
                    { Title = "Started loading cargo"
                      Description = $"CargoId {ev.CargoId}"
                      EventType = VesselEventType.Info
                      Inserted = event.Timestamp }
                | :? Event.VesselUnloadingStarted as ev ->
                    { Title = "Started unloading cargo"
                      Description = $"CargoId {ev.CargoId}"
                      EventType = VesselEventType.Info
                      Inserted = event.Timestamp }
                | :? Event.VesselLoadingCompleted as ev ->
                    { Title = "Finished loading cargo"
                      Description = $"CargoId {ev.CargoId}"
                      EventType = VesselEventType.Success
                      Inserted = event.Timestamp }
                | :? Event.VesselUnloadingCompleted as ev ->
                    { Title = "Finished unloading cargo"
                      Description = $"CargoId {ev.CargoId}"
                      EventType = VesselEventType.Success
                      Inserted = event.Timestamp }
                | :? Event.VesselPositionUpdated as ev ->
                    { Title = "Finished loading cargo"
                      Description = $"{ev.Latitude},{ev.Longitude}"
                      EventType = VesselEventType.Info
                      Inserted = event.Timestamp }
                | :? Event.VesselDecommissioned as ev ->
                    { Title = "Vessel decommissioned"
                      Description = $"{ev.At}"
                      EventType = VesselEventType.Fail
                      Inserted = event.Timestamp }
                | _ ->
                    { Title = "Unknown event"
                      Description = ""
                      EventType = VesselEventType.Fail
                      Inserted = event.Timestamp })

        return mapped
    }

let getAllPorts (session: IQuerySession) =
    asyncResult {
        let! ports = session.Query<Domain.Port.PortView>().ToListAsync()
        return ports |> Seq.toArray
    }
