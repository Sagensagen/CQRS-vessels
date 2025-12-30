module Domain.CargoAggregate

open System
open Shared.Api.Cargo
open Domain.EventMetadata
open FsToolkit.ErrorHandling

type CargoState = {
    Id: Guid
    Version: int64
    Spec: CargoSpec
    Status: CargoStatus
    OriginPortId: Guid
    DestinationPortId: Guid
    CurrentVesselId: Guid option
    CurrentLocation: CargoLocation
    CreatedAt: DateTimeOffset
    LoadedAt: DateTimeOffset option
    DeliveredAt: DateTimeOffset option
}

and CargoStatus =
    | AwaitingPickup
    | ReservedForVessel of vesselId: Guid * reservedAt: DateTimeOffset
    | LoadedOnVessel of vesselId: Guid
    | InTransit of vesselId: Guid
    | UnloadedAtPort of portId: Guid
    | Delivered
    | Cancelled

and CargoLocation =
    | AtPort of portId: Guid
    | OnVessel of vesselId: Guid

type CargoCommand =
    | CreateCargo of CreateCargoCmd
    | ReserveForVessel of ReserveForVesselCmd
    | CancelReservation of CancelReservationCmd
    | LoadOntoVessel of LoadOntoVesselCmd
    | MarkInTransit of MarkInTransitCmd
    | UnloadFromVessel of UnloadFromVesselCmd
    | MarkDelivered of MarkDeliveredCmd
    | CancelCargo of CancelCargoCmd

and CreateCargoCmd = {
    Id: Guid
    Spec: CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
    Metadata: EventMetadata
}

and ReserveForVesselCmd = {
    AggregateId: Guid
    VesselId: Guid
    PortId: Guid
    ExpiresAt: DateTimeOffset
    Metadata: EventMetadata
}

and CancelReservationCmd = {
    AggregateId: Guid
    Reason: string
    Metadata: EventMetadata
}

and LoadOntoVesselCmd = {
    AggregateId: Guid
    VesselId: Guid
    Metadata: EventMetadata
}

and MarkInTransitCmd = { AggregateId: Guid; Metadata: EventMetadata }

and UnloadFromVesselCmd = {
    AggregateId: Guid
    PortId: Guid
    Metadata: EventMetadata
}

and MarkDeliveredCmd = { AggregateId: Guid; Metadata: EventMetadata }

and CancelCargoCmd = {
    AggregateId: Guid
    Reason: string
    Metadata: EventMetadata
}

type CargoEvent =
    | CargoCreated of CargoCreatedEvt
    | CargoReservedForVessel of CargoReservedForVesselEvt
    | CargoReservationCancelled of CargoReservationCancelledEvt
    | CargoLoadedOnVessel of CargoLoadedOnVesselEvt
    | CargoMarkedInTransit of CargoMarkedInTransitEvt
    | CargoUnloadedFromVessel of CargoUnloadedFromVesselEvt
    | CargoDelivered of CargoDeliveredEvt
    | CargoCancelled of CargoCancelledEvt

and CargoCreatedEvt = {
    Id: Guid
    Spec: CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
    CreatedAt: DateTimeOffset
}

and CargoReservedForVesselEvt = {
    VesselId: Guid
    PortId: Guid
    ReservedAt: DateTimeOffset
    ExpiresAt: DateTimeOffset
}

and CargoReservationCancelledEvt = { CancelledAt: DateTimeOffset; Reason: string }

and CargoLoadedOnVesselEvt = { VesselId: Guid; LoadedAt: DateTimeOffset }

and CargoMarkedInTransitEvt = { MarkedAt: DateTimeOffset }

and CargoUnloadedFromVesselEvt = { PortId: Guid; UnloadedAt: DateTimeOffset }

and CargoDeliveredEvt = { DeliveredAt: DateTimeOffset }

and CargoCancelledEvt = { Reason: string; CancelledAt: DateTimeOffset }

module Validation =
    let validateWeight (weight: Weight) =
        if weight > 0.0<kg> then
            Ok()
        else
            Error(InvalidCargoSpec "Weight must be positive")

    let validateVolume (volume: Volume) =
        if volume > 0.0<m^3> then
            Ok()
        else
            Error(InvalidCargoSpec "Volume must be positive")

    let validateContainerCount (count: int) =
        if count > 0 then
            Ok()
        else
            Error(InvalidCargoSpec "Container count must be positive")

    let validateCargoSpec (spec: CargoSpec) =
        result {
            do! validateWeight spec.TotalWeight
            do! validateVolume spec.TotalVolume
            do! validateContainerCount spec.ContainerLoad.Count
            return ()
        }

let decide
    (state: CargoState option)
    (command: CargoCommand)
    : Result<CargoEvent list, CargoCommandErrors> =
    match command, state with
    | CreateCargo cmd, None ->
        result {
            do! Validation.validateCargoSpec cmd.Spec

            return [
                CargoCreated {
                    Id = cmd.Id
                    Spec = cmd.Spec
                    OriginPortId = cmd.OriginPortId
                    DestinationPortId = cmd.DestinationPortId
                    CreatedAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | CreateCargo _, Some _ -> Error CargoAlreadyExists

    | ReserveForVessel cmd, Some cargo ->
        result {
            // Validate cargo is awaiting pickup
            match cargo.Status with
            | AwaitingPickup -> ()
            | ReservedForVessel(vesselId, _) -> return! Error(CargoAlreadyReserved vesselId)
            | LoadedOnVessel i when i = cmd.VesselId -> return! Error CargoAlreadyLoaded
            | LoadedOnVessel i when i <> cmd.VesselId ->
                return! Error CargoAlreadyLoadedOnAnotherVessel
            | Delivered -> return! Error(InvalidCargoSpec "Cargo already delivered")
            | Cancelled -> return! Error Shared.Api.Cargo.CargoCancelled
            | _ -> return! Error(InvalidCargoState("AwaitingPickup", $"{cargo.Status}"))

            // Validate cargo is at the port where vessel wants to pick up
            match cargo.CurrentLocation with
            | AtPort portId when portId = cmd.PortId -> ()
            | AtPort portId ->
                return!
                    Error(
                        ValidationError
                            $"Cargo is at port {portId}, not at pickup port {cmd.PortId}"
                    )
            | OnVessel vesselId ->
                return! Error(ValidationError $"Cargo already on vessel {vesselId}")

            return [
                CargoReservedForVessel {
                    VesselId = cmd.VesselId
                    PortId = cmd.PortId
                    ReservedAt = cmd.Metadata.Timestamp
                    ExpiresAt = cmd.ExpiresAt
                }
            ]
        }

    | ReserveForVessel _, None -> Error Shared.Api.Cargo.CargoNotFound

    | CancelReservation cmd, Some cargo ->
        result {
            // Validate cargo is actually reserved
            match cargo.Status with
            | ReservedForVessel _ -> ()
            | _ -> return! Error(InvalidCargoState("ReservedForVessel", $"{cargo.Status}"))

            return [
                CargoReservationCancelled {
                    CancelledAt = cmd.Metadata.Timestamp
                    Reason = cmd.Reason
                }
            ]
        }

    | CancelReservation _, None -> Error Shared.Api.Cargo.CargoNotFound

    | LoadOntoVessel cmd, Some cargo ->
        result {
            match cargo.Status with
            | ReservedForVessel(vesselId, _) when vesselId = cmd.VesselId -> ()
            | ReservedForVessel(otherVesselId, _) ->
                return! Error(CargoReservedForDifferentVessel otherVesselId)
            | AwaitingPickup ->
                return! Error(ValidationError "Cargo must be reserved before loading")
            | LoadedOnVessel _ -> return! Error CargoAlreadyLoaded
            | Delivered -> return! Error(InvalidCargoSpec "Cargo already delivered")
            | Cancelled -> return! Error Shared.Api.Cargo.CargoCancelled
            | _ -> return! Error(InvalidCargoState("ReservedForVessel", $"{cargo.Status}"))

            return [
                CargoLoadedOnVessel { VesselId = cmd.VesselId; LoadedAt = cmd.Metadata.Timestamp }
            ]
        }

    | LoadOntoVessel _, None -> Error Shared.Api.Cargo.CargoNotFound

    | MarkInTransit cmd, Some cargo ->
        result {
            match cargo.Status with
            | LoadedOnVessel _ -> ()
            | _ -> return! Error CargoNotLoaded

            return [ CargoMarkedInTransit { MarkedAt = cmd.Metadata.Timestamp } ]
        }

    | MarkInTransit _, None -> Error Shared.Api.Cargo.CargoNotFound

    | UnloadFromVessel cmd, Some cargo ->
        result {
            // Validate cargo is on a vessel
            match cargo.Status with
            | LoadedOnVessel _
            | InTransit _ -> ()
            | _ -> return! Error CargoNotOnVessel

            // Validate cargo has a current vessel
            if cargo.CurrentVesselId.IsNone then
                return! Error CargoNotOnVessel

            return [
                CargoUnloadedFromVessel { PortId = cmd.PortId; UnloadedAt = cmd.Metadata.Timestamp }
            ]
        }

    | UnloadFromVessel _, None -> Error Shared.Api.Cargo.CargoNotFound

    | MarkDelivered cmd, Some cargo ->
        result {
            // Validate cargo is at destination port
            match cargo.CurrentLocation with
            | AtPort portId when portId = cargo.DestinationPortId -> ()
            | _ -> return! Error CargoNotAtDestination

            match cargo.Status with
            | UnloadedAtPort _ -> ()
            | _ -> return! Error(InvalidCargoSpec "Cargo must be unloaded before delivery")

            return [ CargoDelivered { DeliveredAt = cmd.Metadata.Timestamp } ]
        }

    | MarkDelivered _, None -> Error Shared.Api.Cargo.CargoNotFound

    | CancelCargo cmd, Some cargo ->
        result {
            match cargo.Status with
            | Delivered -> return! Error(InvalidCargoSpec "Cannot cancel delivered cargo")
            | Cancelled -> return! Error Shared.Api.Cargo.CargoCancelled
            | _ -> ()

            return [
                CargoEvent.CargoCancelled {
                    Reason = cmd.Reason
                    CancelledAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | CancelCargo _, None -> Error Shared.Api.Cargo.CargoNotFound

let evolve (state: CargoState option) (event: CargoEvent) : CargoState option =
    match event, state with
    | CargoCreated evt, None ->
        Some {
            Id = evt.Id
            Version = 0L
            Spec = evt.Spec
            Status = AwaitingPickup
            OriginPortId = evt.OriginPortId
            DestinationPortId = evt.DestinationPortId
            CurrentVesselId = None
            CurrentLocation = AtPort evt.OriginPortId
            CreatedAt = evt.CreatedAt
            LoadedAt = None
            DeliveredAt = None
        }

    | CargoReservedForVessel evt, Some cargo ->
        Some { cargo with Status = ReservedForVessel(evt.VesselId, evt.ReservedAt) }

    | CargoReservationCancelled _evt, Some cargo -> Some { cargo with Status = AwaitingPickup }

    | CargoLoadedOnVessel evt, Some cargo ->
        Some {
            cargo with
                Status = LoadedOnVessel evt.VesselId
                CurrentVesselId = Some evt.VesselId
                CurrentLocation = OnVessel evt.VesselId
                LoadedAt = Some evt.LoadedAt
        }

    | CargoMarkedInTransit evt, Some cargo ->
        match cargo.CurrentVesselId with
        | Some vesselId -> Some { cargo with Status = InTransit vesselId }
        | None -> state

    | CargoUnloadedFromVessel evt, Some cargo ->
        Some {
            cargo with
                Status = UnloadedAtPort evt.PortId
                CurrentVesselId = None
                CurrentLocation = AtPort evt.PortId
        }

    | CargoDelivered evt, Some cargo ->
        Some { cargo with Status = Delivered; DeliveredAt = Some evt.DeliveredAt }

    | CargoEvent.CargoCancelled _evt, Some cargo -> Some { cargo with Status = Cancelled }

    | _, state -> state
