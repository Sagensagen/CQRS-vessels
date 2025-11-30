module Domain.VesselAggregate

open System
open Shared.Api.Vessel
open Domain.EventMetadata
open FsToolkit.ErrorHandling
open Domain.VesselErrors

type VesselState = {
    Id: Guid
    Version: int64
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: VesselPosition
    Length: float option
    Beam: float option
    Draught: float option
    State: OperationalStatus
    Activity: VesselActivity
    VesselType: VesselType
    CrewSize: int
    CurrentPortId: Guid option
    RegisteredAt: DateTimeOffset
}

type VesselCommand =
    | RegisterVessel of RegisterVesselCmd
    | UpdatePosition of UpdatePositionCmd
    | ArriveAtPort of ArriveAtPortCmd
    | DepartFromPort of DepartFromPortCmd
    | UpdateOperationalStatus of UpdateOperationalStatusCmd
    | DecommissionVessel of DecommissionVesselCmd

and RegisterVesselCmd = {
    Id: Guid
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: VesselPosition
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
    Metadata: EventMetadata
}

and UpdatePositionCmd = {
    AggregateId: Guid
    Position: VesselPosition
    Metadata: EventMetadata
}

and ArriveAtPortCmd = {
    AggregateId: Guid
    PortId: Guid
    ReservationId: Guid
    Metadata: EventMetadata
}

and DepartFromPortCmd = { AggregateId: Guid; Metadata: EventMetadata }

and UpdateOperationalStatusCmd = {
    AggregateId: Guid
    Status: OperationalStatus
    Activity: VesselActivity
    Metadata: EventMetadata
}

and DecommissionVesselCmd = { AggregateId: Guid; Metadata: EventMetadata }

type VesselEvent =
    | VesselRegistered of VesselRegisteredEvt
    | VesselPositionUpdated of VesselPositionUpdatedEvt
    | VesselArrived of VesselArrivedEvt
    | VesselDeparted of VesselDepartedEvt
    | VesselOperationalStatusUpdated of VesselOperationalStatusUpdatedEvt
    | VesselDecommissioned of VesselDecommissionedEvt

and VesselRegisteredEvt = {
    Id: Guid
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: VesselPosition
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
    RegisteredAt: DateTimeOffset
}

and VesselPositionUpdatedEvt = { Position: VesselPosition; UpdatedAt: DateTimeOffset }

and VesselArrivedEvt = {
    PortId: Guid
    ReservationId: Guid
    ArrivedAt: DateTimeOffset
}

and VesselDepartedEvt = { FromPortId: Guid; DepartedAt: DateTimeOffset }

and VesselOperationalStatusUpdatedEvt = {
    Status: OperationalStatus
    Activity: VesselActivity
    UpdatedAt: DateTimeOffset
}

and VesselDecommissionedEvt = { DecommissionedAt: DateTimeOffset }

module private Validation =
    let validateMmsi (mmsi: int) : Result<int, VesselError> =
        if mmsi >= 100000000 && mmsi <= 999999999 then
            Ok mmsi
        else
            Error(ValidationError "MMSI must be a 9-digit number")

    let validateImo (imo: int option) : Result<int option, VesselError> =
        match imo with
        | Some i when i >= 1000000 && i <= 9999999 -> Ok imo
        | Some _ -> Error(ValidationError "IMO number must be 7 digits")
        | None -> Ok None

    let validateCrewSize (size: int) : Result<int, VesselError> =
        if size >= 0 && size <= 10000 then
            Ok size
        else
            Error(ValidationError "Crew size must be between 0 and 10000")

    let validateName (name: string) : Result<string, VesselError> =
        if String.IsNullOrWhiteSpace(name) then
            Error(ValidationError "Vessel name cannot be empty")
        elif name.Length > 200 then
            Error(ValidationError "Vessel name cannot exceed 200 characters")
        else
            Ok name

    let canDepartFromPort (state: VesselState) : Result<unit, VesselError> =
        match state.State, state.CurrentPortId with
        | OperationalStatus.Docked _, Some _ -> Ok()
        | OperationalStatus.Docked _, None ->
            Error(InvalidStateTransition("docked with port ID", "docked without port ID"))
        | _ -> Error(InvalidStateTransition("docked", "at sea"))

    let canArriveAtPort (state: VesselState) : Result<unit, VesselError> =
        match state.State with
        | OperationalStatus.AtSea -> Ok()
        | _ -> Error(InvalidStateTransition("at sea", string state.State))

let decide
    (state: VesselState option)
    (command: VesselCommand)
    : Result<VesselEvent list, VesselError> =
    match command, state with

    // Register new vessel
    | RegisterVessel cmd, None ->
        result {
            let! _ = Validation.validateName cmd.Name
            let! _ = Validation.validateMmsi cmd.Mmsi
            let! _ = Validation.validateImo cmd.Imo
            let! _ = Validation.validateCrewSize cmd.CrewSize

            return [
                VesselRegistered {
                    Id = cmd.Id
                    Name = cmd.Name
                    Mmsi = cmd.Mmsi
                    Imo = cmd.Imo
                    Flag = cmd.Flag
                    Position = cmd.Position
                    Length = cmd.Length
                    Beam = cmd.Beam
                    Draught = cmd.Draught
                    VesselType = cmd.VesselType
                    CrewSize = cmd.CrewSize
                    RegisteredAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | RegisterVessel _, Some _ -> Error VesselAlreadyExists

    | UpdatePosition cmd, Some _ ->
        Ok [
            VesselPositionUpdated { Position = cmd.Position; UpdatedAt = cmd.Metadata.Timestamp }
        ]

    | UpdatePosition _, None -> Error VesselNotFound

    | ArriveAtPort cmd, Some vessel ->
        match Validation.canArriveAtPort vessel with
        | Ok() ->
            Ok [
                VesselArrived {
                    PortId = cmd.PortId
                    ReservationId = cmd.ReservationId
                    ArrivedAt = cmd.Metadata.Timestamp
                }
                VesselOperationalStatusUpdated {
                    Status = OperationalStatus.Docked cmd.PortId
                    Activity = VesselActivity.Idle
                    UpdatedAt = cmd.Metadata.Timestamp
                }
            ]
        | Error err -> Error err

    | ArriveAtPort _, None -> Error VesselNotFound

    | DepartFromPort cmd, Some vessel ->
        result {
            let! _ = Validation.canDepartFromPort vessel
            let portId = vessel.CurrentPortId |> Option.defaultValue Guid.Empty
            return [
                VesselDeparted { FromPortId = portId; DepartedAt = cmd.Metadata.Timestamp }
                VesselOperationalStatusUpdated {
                    Status = OperationalStatus.AtSea
                    Activity = VesselActivity.Idle
                    UpdatedAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | DepartFromPort _, None -> Error VesselNotFound

    | UpdateOperationalStatus cmd, Some _ ->
        Ok [
            VesselOperationalStatusUpdated {
                Status = cmd.Status
                Activity = cmd.Activity
                UpdatedAt = cmd.Metadata.Timestamp
            }
        ]

    | UpdateOperationalStatus _, None -> Error VesselNotFound

    | DecommissionVessel cmd, Some _ ->
        Ok [
            VesselDecommissioned { DecommissionedAt = cmd.Metadata.Timestamp }
            VesselOperationalStatusUpdated {
                Status = OperationalStatus.Decommissioned
                Activity = VesselActivity.Idle
                UpdatedAt = cmd.Metadata.Timestamp
            }
        ]

    | DecommissionVessel _, None -> Error VesselNotFound

let evolve (state: VesselState option) (event: VesselEvent) : VesselState option =
    match event, state with
    | VesselRegistered evt, None ->
        Some {
            Id = evt.Id
            Version = 0L
            Name = evt.Name
            Mmsi = evt.Mmsi
            Imo = evt.Imo
            Flag = evt.Flag
            Position = evt.Position
            Length = evt.Length
            Beam = evt.Beam
            Draught = evt.Draught
            State = OperationalStatus.AtSea
            Activity = VesselActivity.Idle
            VesselType = evt.VesselType
            CrewSize = evt.CrewSize
            CurrentPortId = None
            RegisteredAt = evt.RegisteredAt
        }

    | VesselPositionUpdated evt, Some vessel -> Some { vessel with Position = evt.Position }

    | VesselArrived evt, Some vessel -> Some { vessel with CurrentPortId = Some evt.PortId }

    | VesselDeparted _, Some vessel -> Some { vessel with CurrentPortId = None }

    | VesselOperationalStatusUpdated evt, Some vessel ->
        Some { vessel with State = evt.Status; Activity = evt.Activity }

    | VesselDecommissioned _, Some vessel -> Some vessel

    | _, _ -> state
