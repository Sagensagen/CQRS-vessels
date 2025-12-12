module Domain.PortAggregate

open System
open Shared.Api.Port
open Domain.EventMetadata
open FsToolkit.ErrorHandling
open Shared.Api.Shared

type ReservationState = {
    ReservationId: Guid
    VesselId: Guid
    ReservedAt: DateTimeOffset
    ExpiresAt: DateTimeOffset
}

type PortState = {
    Id: Guid
    Version: int64
    Name: string
    Locode: string option
    Country: string
    Position: LatLong
    Timezone: string option
    MaxDocks: int
    Status: PortStatus
    DockedVessels: Set<Guid>
    PendingReservations: Map<Guid, ReservationState>
    RegisteredAt: DateTimeOffset
} with

    member this.AvailableDocks =
        this.MaxDocks - this.DockedVessels.Count - this.PendingReservations.Count

    member this.CanAcceptReservation =
        this.Status = PortStatus.Open && this.AvailableDocks > 0

type PortCommand =
    | RegisterPort of RegisterPortCmd
    | ReserveDocking of ReserveDockingCmd
    | ConfirmDocking of ConfirmDockingCmd
    | ExpireReservation of ExpireReservationCmd
    | UndockVessel of UndockVesselCmd
    | OpenPort of OpenPortCmd
    | ClosePort of ClosePortCmd

and RegisterPortCmd = {
    Id: Guid
    Name: string
    Locode: string option
    Country: string
    Position: LatLong
    Timezone: string option
    MaxDocks: int
    Metadata: EventMetadata
}

and ReserveDockingCmd = {
    AggregateId: Guid
    VesselId: Guid
    ReservationId: Guid
    Metadata: EventMetadata
}

and ConfirmDockingCmd = {
    AggregateId: Guid
    VesselId: Guid
    ReservationId: Guid
    Metadata: EventMetadata
}

and ExpireReservationCmd = {
    AggregateId: Guid
    VesselId: Guid
    ReservationId: Guid
    Metadata: EventMetadata
}

and UndockVesselCmd = {
    AggregateId: Guid
    VesselId: Guid
    Metadata: EventMetadata
}

and OpenPortCmd = { AggregateId: Guid; Metadata: EventMetadata }

and ClosePortCmd = { AggregateId: Guid; Metadata: EventMetadata }

type PortEvent =
    | PortRegistered of PortRegisteredEvt
    | VesselDockingReserved of VesselDockingReservedEvt
    | DockingConfirmed of DockingConfirmedEvt
    | DockingReservationExpired of DockingReservationExpiredEvt
    | VesselUndocked of VesselUndockedEvt
    | PortOpened of PortOpenedEvt
    | PortClosed of PortClosedEvt

and PortRegisteredEvt = {
    Id: Guid
    Name: string
    Locode: string option
    Country: string
    Position: LatLong
    Timezone: string option
    MaxDocks: int
    RegisteredAt: DateTimeOffset
}

and VesselDockingReservedEvt = {
    VesselId: Guid
    ReservationId: Guid
    ReservedAt: DateTimeOffset
    ExpiresAt: DateTimeOffset
}

and DockingConfirmedEvt = {
    VesselId: Guid
    ReservationId: Guid
    ConfirmedAt: DateTimeOffset
}

and DockingReservationExpiredEvt = {
    VesselId: Guid
    ReservationId: Guid
    ExpiredAt: DateTimeOffset
}

and VesselUndockedEvt = { VesselId: Guid; UndockedAt: DateTimeOffset }

and PortOpenedEvt = { OpenedAt: DateTimeOffset }

and PortClosedEvt = { ClosedAt: DateTimeOffset }

module private Validation =
    let validateMaxDocks (maxDocks: int) : Result<int, PortCommandErrors> =
        if maxDocks > 0 && maxDocks <= 1000 then
            Ok maxDocks
        else
            Error(ValidationError "MaxDocks must be between 1 and 1000")

    let validateName (name: string) : Result<string, PortCommandErrors> =
        if String.IsNullOrWhiteSpace(name) then
            Error(ValidationError "Port name cannot be empty")
        elif name.Length > 200 then
            Error(ValidationError "Port name cannot exceed 200 characters")
        else
            Ok name

    let validateLatLong (pos: LatLong) : Result<LatLong, PortCommandErrors> =
        if
            (pos.Latitude >= -90.0 && pos.Latitude <= 90.0)
            && (pos.Longitude >= -180.0 && pos.Longitude <= 180.0)
        then
            Ok pos
        else
            Error(ValidationError "Latitude must be between -90 and 90")

    let canReserveDocking
        (port: PortState)
        (reservationId: Guid)
        : Result<unit, PortCommandErrors> =
        if not port.CanAcceptReservation then
            Error NoDockingSpaceAvailable
        elif port.PendingReservations.ContainsKey(reservationId) then
            Error ReservationAlreadyExists
        else
            Ok()

    let hasReservation (port: PortState) (reservationId: Guid) : Result<unit, PortCommandErrors> =
        if port.PendingReservations.ContainsKey(reservationId) then
            Ok()
        else
            Error ReservationNotFound

    let vesselIsNotDocked (port: PortState) (vesselId: Guid) : Result<unit, PortCommandErrors> =
        if port.DockedVessels.Contains(vesselId) then
            Error VesselAlreadyDocked
        else
            Ok()

    let vesselIsDocked (port: PortState) (vesselId: Guid) : Result<unit, PortCommandErrors> =
        if port.DockedVessels.Contains(vesselId) then
            Ok()
        else
            Error VesselNotDockedAtPort

let decide
    (state: PortState option)
    (command: PortCommand)
    : Result<PortEvent list, PortCommandErrors> =
    match command, state with

    // Register new port
    | RegisterPort cmd, None ->
        result {
            let! _ = Validation.validateName cmd.Name
            let! _ = Validation.validateMaxDocks cmd.MaxDocks
            let! _ = Validation.validateLatLong cmd.Position

            return [
                PortRegistered {
                    Id = cmd.Id
                    Name = cmd.Name
                    Locode = cmd.Locode
                    Country = cmd.Country
                    Position = cmd.Position
                    Timezone = cmd.Timezone
                    MaxDocks = cmd.MaxDocks
                    RegisteredAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | RegisterPort _, Some _ -> Error PortAlreadyRegistered

    // Reserve docking
    | ReserveDocking cmd, Some port ->
        result {
            let! _ = Validation.canReserveDocking port cmd.ReservationId
            let! _ = Validation.vesselIsNotDocked port cmd.VesselId

            // Reservation expires after 30 minutes
            let expiresAt = cmd.Metadata.Timestamp.AddMinutes(30.0)

            return [
                VesselDockingReserved {
                    VesselId = cmd.VesselId
                    ReservationId = cmd.ReservationId
                    ReservedAt = cmd.Metadata.Timestamp
                    ExpiresAt = expiresAt
                }
            ]
        }

    | ReserveDocking _, None -> Error Shared.Api.Port.PortNotFound

    | ConfirmDocking cmd, Some port ->
        result {
            let! _ = Validation.hasReservation port cmd.ReservationId

            return [
                DockingConfirmed {
                    VesselId = cmd.VesselId
                    ReservationId = cmd.ReservationId
                    ConfirmedAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | ConfirmDocking _, None -> Error Shared.Api.Port.PortNotFound

    | ExpireReservation cmd, Some port ->
        result {
            let! _ = Validation.hasReservation port cmd.ReservationId

            return [
                DockingReservationExpired {
                    VesselId = cmd.VesselId
                    ReservationId = cmd.ReservationId
                    ExpiredAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | ExpireReservation _, None -> Error Shared.Api.Port.PortNotFound

    | UndockVessel cmd, Some port ->
        result {
            let! _ = Validation.vesselIsDocked port cmd.VesselId

            return [
                VesselUndocked { VesselId = cmd.VesselId; UndockedAt = cmd.Metadata.Timestamp }
            ]
        }

    | UndockVessel _, None -> Error Shared.Api.Port.PortNotFound

    | OpenPort cmd, Some port ->
        result {
            if port.Status = PortStatus.Open then
                return! Error PortAlreadyOpen
            else
                return [ PortOpened { OpenedAt = cmd.Metadata.Timestamp } ]
        }

    | OpenPort _, None -> Error Shared.Api.Port.PortNotFound

    | ClosePort cmd, Some port ->
        result {

            if port.Status = PortStatus.Closed then
                return! Error PortAlreadyClosed
            else
                return [ PortClosed { ClosedAt = cmd.Metadata.Timestamp } ]
        }

    | ClosePort _, None -> Error Shared.Api.Port.PortNotFound

let evolve (state: PortState option) (event: PortEvent) : PortState option =
    match event, state with

    | PortRegistered evt, None ->
        Some {
            Id = evt.Id
            Version = 0L
            Name = evt.Name
            Locode = evt.Locode
            Country = evt.Country
            Position = evt.Position
            Timezone = evt.Timezone
            MaxDocks = evt.MaxDocks
            Status = PortStatus.Open
            DockedVessels = Set.empty
            PendingReservations = Map.empty
            RegisteredAt = evt.RegisteredAt
        }

    | VesselDockingReserved evt, Some port ->
        let reservation: ReservationState = {
            ReservationId = evt.ReservationId
            VesselId = evt.VesselId
            ReservedAt = evt.ReservedAt
            ExpiresAt = evt.ExpiresAt
        }
        Some {
            port with
                PendingReservations = port.PendingReservations.Add(evt.ReservationId, reservation)
        }

    | DockingConfirmed evt, Some port ->
        Some {
            port with
                DockedVessels = port.DockedVessels.Add(evt.VesselId)
                PendingReservations = port.PendingReservations.Remove(evt.ReservationId)
        }

    | DockingReservationExpired evt, Some port ->
        Some { port with PendingReservations = port.PendingReservations.Remove(evt.ReservationId) }

    | VesselUndocked evt, Some port ->
        Some { port with DockedVessels = port.DockedVessels.Remove(evt.VesselId) }

    | PortOpened _, Some port -> Some { port with Status = PortStatus.Open }

    | PortClosed _, Some port -> Some { port with Status = PortStatus.Closed }

    | _, _ -> state
