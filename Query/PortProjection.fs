module Query.PortProjection

open System
open JasperFx.Events.Projections
open Marten.Events.Aggregation
open Domain.PortAggregate
open Query.ReadModels
open Shared.Api.Port

/// Projection that builds PortReadModel from PortEvents
type PortProjection() =
    inherit SingleStreamProjection<PortReadModel, Guid>()

    member _.Create(evt: PortRegisteredEvt) =
        { Id = evt.Id
          Name = evt.Name
          Locode = evt.Locode
          Country = evt.Country
          Latitude = evt.Latitude
          Longitude = evt.Longitude
          Timezone = evt.Timezone
          MaxDocks = evt.MaxDocks
          Status = PortStatus.Open
          CurrentDocked = 0
          AvailableDocks = evt.MaxDocks
          DockedVessels = []
          RegisteredAt = evt.RegisteredAt
          LastUpdated = evt.RegisteredAt
          Version = 0L }

    // Docking reservation made
    member _.Apply(evt: VesselDockingReservedEvt, current: PortReadModel) =
        { current with
            // Reservations reduce available docks but don't count as docked yet
            AvailableDocks = current.MaxDocks - current.CurrentDocked - 1
            LastUpdated = evt.ReservedAt }

    // Docking confirmed - vessel is now docked
    member _.Apply(evt: DockingConfirmedEvt, current: PortReadModel) =
        let dockedVessel =
            { VesselId = evt.VesselId
              VesselName = "" // Will be populated by denormalization or separate query
              DockedAt = evt.ConfirmedAt }

        { current with
            CurrentDocked = current.CurrentDocked + 1
            AvailableDocks = current.MaxDocks - (current.CurrentDocked + 1)
            DockedVessels = dockedVessel :: current.DockedVessels
            LastUpdated = evt.ConfirmedAt }

    // Reservation expired - frees up a slot
    member _.Apply(evt: DockingReservationExpiredEvt, current: PortReadModel) =
        { current with
            AvailableDocks = current.MaxDocks - current.CurrentDocked
            LastUpdated = evt.ExpiredAt }

    // Vessel undocked
    member _.Apply(evt: VesselUndockedEvt, current: PortReadModel) =
        let updatedDockedVessels =
            current.DockedVessels |> List.filter (fun v -> v.VesselId <> evt.VesselId)

        { current with
            CurrentDocked = current.CurrentDocked - 1
            AvailableDocks = current.MaxDocks - (current.CurrentDocked - 1)
            DockedVessels = updatedDockedVessels
            LastUpdated = evt.UndockedAt }

    // Port opened
    member _.Apply(evt: PortOpenedEvt, current: PortReadModel) =
        { current with
            Status = PortStatus.Open
            LastUpdated = evt.OpenedAt }

    // Port closed
    member _.Apply(evt: PortClosedEvt, current: PortReadModel) =
        { current with
            Status = PortStatus.Closed
            LastUpdated = evt.ClosedAt }

let registerProjection (options: Marten.StoreOptions) =
    options.Projections.Add<PortProjection>(ProjectionLifecycle.Inline)
