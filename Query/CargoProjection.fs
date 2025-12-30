module Query.CargoProjection

open System
open JasperFx.Events.Projections
open Marten.Events.Aggregation
open Marten.Events.Projections
open Domain.CargoAggregate
open Query.ReadModels

type CargoProjection() =
    inherit SingleStreamProjection<CargoReadModel, Guid>()

    member _.Create(evt: CargoCreatedEvt) =
        { Id = evt.Id
          Spec = evt.Spec
          Status = AwaitingPickup
          OriginPortId = evt.OriginPortId
          OriginPortName = None
          DestinationPortId = evt.DestinationPortId
          DestinationPortName = None
          CurrentVesselId = None
          CurrentVesselName = None
          CreatedAt = evt.CreatedAt
          LoadedAt = None
          DeliveredAt = None
          LastUpdated = evt.CreatedAt
          Version = 0L }

    member _.Apply(evt: CargoLoadedOnVesselEvt, current: CargoReadModel) =
        { current with
            Status = LoadedOnVessel evt.VesselId
            CurrentVesselId = Some evt.VesselId
            LoadedAt = Some evt.LoadedAt
            LastUpdated = evt.LoadedAt }

    member _.Apply(evt: CargoMarkedInTransitEvt, current: CargoReadModel) =
        match current.CurrentVesselId with
        | Some vesselId ->
            { current with
                Status = InTransit vesselId
                LastUpdated = evt.MarkedAt }
        | None -> current

    member _.Apply(evt: CargoUnloadedFromVesselEvt, current: CargoReadModel) =
        { current with
            Status = UnloadedAtPort evt.PortId
            CurrentVesselId = None
            LastUpdated = evt.UnloadedAt }

    member _.Apply(evt: CargoDeliveredEvt, current: CargoReadModel) =
        { current with
            Status = Delivered
            DeliveredAt = Some evt.DeliveredAt
            LastUpdated = evt.DeliveredAt }

    member _.Apply(evt: CargoCancelledEvt, current: CargoReadModel) =
        { current with
            Status = Cancelled
            LastUpdated = evt.CancelledAt }

let registerProjection (options: Marten.StoreOptions) =
    options.Projections.Add<CargoProjection>(ProjectionLifecycle.Inline)
