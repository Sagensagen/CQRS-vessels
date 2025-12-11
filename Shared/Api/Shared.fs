module Shared.Api.Shared

open System

type LatLong = { Latitude: float; Longitude: float }

/// Network bounds based on the geojson dataset. North and south pole is not valid
/// The global shipping network extent is [-180.0, -60.0001, 180.0, 76.5]
module NavigationBounds =
    let MinLatitude = -60.0
    let MaxLatitude = 76.5
    let MinLongitude = -180.0
    let MaxLongitude = 180.0

    let isWithinBounds (position: LatLong) : bool =
        position.Latitude >= MinLatitude
        && position.Latitude <= MaxLatitude
        && position.Longitude >= MinLongitude
        && position.Longitude <= MaxLongitude

type EventWrapperType =
    | Success
    | Fail
    | Info

type EventWrapper = {
    Title: string
    Description: string
    EventType: EventWrapperType
    Inserted: DateTimeOffset
}
