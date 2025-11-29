module Domain.EventMetadata

open System

[<CLIMutable>]
type EventMetadata = {
    EventId: Guid
    CorrelationId: Guid
    CausationId: Guid
    Timestamp: DateTimeOffset
    Version: int
    Actor: string option
}

/// Creates default metadata for a new event
let createMetadata correlationId causationId actor = {
    EventId = Guid.NewGuid()
    CorrelationId = correlationId
    CausationId = causationId
    Timestamp = DateTimeOffset.UtcNow
    Version = 1
    Actor = actor
}

let createInitialMetadata actor =
    let correlationId = Guid.NewGuid()
    createMetadata correlationId correlationId actor
