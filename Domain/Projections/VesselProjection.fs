module Domain.Projections.VesselProjection

open System
open Domain.Vessel
open Shared.Api.Vessel

// Once en event is written to the event store, catch the event here and update projection
type VesselViewProjection() =
    inherit Marten.Events.Aggregation.SingleStreamProjection<VesselView, Guid>()

    member this.Create (event: Event.VesselRegistered) : VesselView = {
        Id = event.Id
        Registered = DateTimeOffset.UtcNow
        Name = event.Name
        Position = event.Position
        Mmsi = event.Mmsi
        Imo = event.Imo
        Flag = event.Flag
        Length = event.Length
        Beam = event.Beam
        Draught = event.Draught
        State = Docked "Oslo"
        Activity = Idle
        VesselType = event.VesselType
        CrewSize = event.CrewSize
        Inserted = DateTimeOffset.UtcNow
    }

    member this.Apply (_event: Event.VesselDeparted, current: VesselView) : VesselView = {
        current with
            State = AtSea
    }

    member this.Apply (event: Event.VesselArrived, current: VesselView) : VesselView = {
        current with
            State = Docked event.AtPort
    }

    member this.Apply (event: Event.VesselLoadingStarted, current: VesselView) : VesselView = {
        current with
            Activity = CargoOperation(Loading, Some event.CargoId)
    }

    member this.Apply (_event: Event.VesselLoadingCompleted, current: VesselView) : VesselView = {
        current with
            Activity = Idle
    }

    member this.Apply (event: Event.VesselUnloadingStarted, current: VesselView) : VesselView = {
        current with
            Activity = CargoOperation(Unloading, Some event.CargoId)
    }

    member this.Apply (_event: Event.VesselUnloadingCompleted, current: VesselView) : VesselView = {
        current with
            Activity = Idle
    }

    member this.Apply (event: Event.VesselPositionUpdated, current: VesselView) : VesselView = {
        current with
            Position = event
    }

    member this.Apply (_event: Event.VesselDecommissioned, current: VesselView) : VesselView = {
        current with
            State = Decommissioned
    }
