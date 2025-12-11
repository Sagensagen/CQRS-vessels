module Shared.Api.Shared

open System

type LatLong = { Latitude: float; Longitude: float }

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
