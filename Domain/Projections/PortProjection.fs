module Domain.Projections.PortProjection

open System
open Domain.Port
open Shared.Api.Port

type PortViewProjection() =
    inherit Marten.Events.Aggregation.SingleStreamProjection<PortView, Guid>()

    member this.Create (event: Event.PortRegistered) : PortView = {
        Id = event.Id
        Name = event.Name
        Status = Open
        Locode = event.Locode
        Country = event.Country
        Latitude = event.Latitude
        Longitude = event.Longitude
        Timezone = event.Timezone
        MaxDocks = event.MaxDocks
        CurrentDocked = 0
        Inserted = DateTimeOffset.UtcNow
    }
