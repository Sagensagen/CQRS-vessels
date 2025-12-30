module Query.VesselProjection

open System
open JasperFx.Events.Projections
open Marten.Events.Aggregation
open Domain.VesselAggregate
open Query.ReadModels
open Shared.Api.Vessel


type VesselProjection() =
    inherit SingleStreamProjection<VesselReadModel, Guid>()

    // Create initial read model from VesselRegistered event
    member _.Create(evt: VesselRegisteredEvt) =
        { Id = evt.Id
          Name = evt.Name
          Mmsi = evt.Mmsi
          Imo = evt.Imo
          Flag = evt.Flag
          Position = evt.Position
          Length = evt.Length
          Beam = evt.Beam
          Draught = evt.Draught
          State = OperationalStatus.AtSea
          VesselType = evt.VesselType
          CrewSize = evt.CrewSize
          CurrentPortId = None
          CurrentPortName = None
          CurrentCargo = None
          RegisteredAt = evt.RegisteredAt
          LastUpdated = evt.RegisteredAt
          Version = 0L }

    member _.Apply(evt: VesselPositionUpdatedEvt, current: VesselReadModel) =
        { current with
            Position = evt.Position
            LastUpdated = evt.UpdatedAt }

    member _.Apply(evt: VesselArrivedEvt, current: VesselReadModel) =
        { current with
            CurrentPortId = Some evt.PortId
            // Port name will be populated by a separate query or denormalization
            CurrentPortName = None
            LastUpdated = evt.ArrivedAt }

    member _.Apply(evt: VesselDepartedEvt, current: VesselReadModel) =
        { current with
            CurrentPortId = None
            CurrentPortName = None
            LastUpdated = evt.DepartedAt }

    member _.Apply(evt: VesselOperationalStatusUpdatedEvt, current: VesselReadModel) =
        { current with
            State = evt.Status
            LastUpdated = evt.UpdatedAt }

    member _.Apply(evt: VesselDecommissionedEvt, current: VesselReadModel) =
        { current with
            LastUpdated = evt.DecommissionedAt }

    member _.Apply(evt: CargoLoadedEvt, current: VesselReadModel) =
        { current with
            CurrentCargo =
                Some
                    { CurrentCargoId = evt.CargoId
                      CurrentCargoDestinationPortId = evt.DestinationPortId }
            LastUpdated = evt.LoadedAt }

    member _.Apply(evt: CargoUnloadedEvt, current: VesselReadModel) =
        { current with
            CurrentCargo = None
            LastUpdated = evt.UnloadedAt }

let registerProjection (options: Marten.StoreOptions) =
    options.Projections.Add<VesselProjection>(ProjectionLifecycle.Inline)
