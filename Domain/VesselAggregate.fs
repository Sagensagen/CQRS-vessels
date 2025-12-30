module Domain.VesselAggregate

open System
open Shared.Api
open Shared.Api.Shared
open Shared.Api.Vessel
open Domain.EventMetadata
open FsToolkit.ErrorHandling

type CargoCapacity = {
    MaxWeight: Cargo.Weight
    MaxVolume: Cargo.Volume
    MaxContainers: int
}

type LoadedCargoInfo = {
    CargoId: Guid
    Spec: Cargo.CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
}

type VesselState = {
    Id: Guid
    Version: int64
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: LatLong
    Length: float option
    Beam: float option
    Draught: float option
    State: OperationalStatus
    VesselType: VesselType
    CrewSize: int
    CurrentPortId: Guid option
    MaxCargoCapacity: CargoCapacity
    CurrentCargo: LoadedCargoInfo option
    RegisteredAt: DateTimeOffset
}

type VesselCommand =
    | RegisterVessel of RegisterVesselCmd
    | UpdatePosition of UpdatePositionCmd
    | ArriveAtPort of ArriveAtPortCmd
    | DepartFromPort of DepartFromPortCmd
    | UpdateOperationalStatus of UpdateOperationalStatusCmd
    | DecommissionVessel of DecommissionVesselCmd
    | AdvanceRouteWaypoint of AdvanceRouteWaypointCmd
    | LoadCargo of LoadCargoCmd
    | UnloadCargo of UnloadCargoCmd

and RegisterVesselCmd = {
    Id: Guid
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: LatLong
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
    Metadata: EventMetadata
}

and UpdatePositionCmd = {
    AggregateId: Guid
    Position: LatLong
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
    Metadata: EventMetadata
}

and DecommissionVesselCmd = { AggregateId: Guid; Metadata: EventMetadata }

and AdvanceRouteWaypointCmd = { AggregateId: Guid; Metadata: EventMetadata }

and LoadCargoCmd = {
    AggregateId: Guid
    CargoId: Guid
    CargoSpec: Cargo.CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
    Metadata: EventMetadata
}

and UnloadCargoCmd = {
    AggregateId: Guid
    CargoId: Guid
    Metadata: EventMetadata
}

type VesselEvent =
    | VesselRegistered of VesselRegisteredEvt
    | VesselPositionUpdated of VesselPositionUpdatedEvt
    | VesselArrived of VesselArrivedEvt
    | VesselDeparted of VesselDepartedEvt
    | VesselOperationalStatusUpdated of VesselOperationalStatusUpdatedEvt
    | VesselDecommissioned of VesselDecommissionedEvt
    | CargoLoaded of CargoLoadedEvt
    | CargoUnloaded of CargoUnloadedEvt

and VesselRegisteredEvt = {
    Id: Guid
    Name: string
    Mmsi: int
    Imo: int option
    Flag: string
    Position: LatLong
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
    RegisteredAt: DateTimeOffset
}

and VesselPositionUpdatedEvt = { Position: LatLong; UpdatedAt: DateTimeOffset }

and VesselArrivedEvt = {
    PortId: Guid
    ReservationId: Guid
    ArrivedAt: DateTimeOffset
}

and VesselDepartedEvt = { FromPortId: Guid; DepartedAt: DateTimeOffset }

and VesselOperationalStatusUpdatedEvt = { Status: OperationalStatus; UpdatedAt: DateTimeOffset }

and VesselDecommissionedEvt = { DecommissionedAt: DateTimeOffset }

and CargoLoadedEvt = {
    CargoId: Guid
    Spec: Cargo.CargoSpec
    OriginPortId: Guid
    DestinationPortId: Guid
    LoadedAt: DateTimeOffset
}

and CargoUnloadedEvt = { CargoId: Guid; UnloadedAt: DateTimeOffset }

module private Validation =
    let validateMmsi (mmsi: int) : Result<int, VesselCommandErrors> =
        if mmsi >= 100000000 && mmsi <= 999999999 then
            Ok mmsi
        else
            Error(Shared.Api.Vessel.ValidationError "MMSI must be a 9-digit number")

    let validateImo (imo: int option) : Result<int option, VesselCommandErrors> =
        match imo with
        | Some i when i >= 1000000 && i <= 9999999 -> Ok imo
        | Some _ -> Error(Shared.Api.Vessel.ValidationError "IMO number must be 7 digits")
        | None -> Ok None

    let validateCrewSize (size: int) : Result<int, VesselCommandErrors> =
        if size >= 0 && size <= 10000 then
            Ok size
        else
            Error(Shared.Api.Vessel.ValidationError "Crew size must be between 0 and 10000")

    let validateName (name: string) : Result<string, VesselCommandErrors> =
        if String.IsNullOrWhiteSpace(name) then
            Error(Shared.Api.Vessel.ValidationError "Vessel name cannot be empty")
        elif name.Length > 200 then
            Error(Shared.Api.Vessel.ValidationError "Vessel name cannot exceed 200 characters")
        else
            Ok name

    let validatePosition (position: LatLong) : Result<LatLong, VesselCommandErrors> =
        if NavigationBounds.isWithinBounds position then
            Ok position
        else
            Error(
                Shared.Api.Vessel.ValidationError
                    $"Position ({position.Latitude}, {position.Longitude}) is outside navigable network bounds (Lat: {NavigationBounds.MinLatitude} to {NavigationBounds.MaxLatitude}, Lon: {NavigationBounds.MinLongitude} to {NavigationBounds.MaxLongitude})"
            )

    let canDepartFromPort (state: VesselState) : Result<unit, VesselCommandErrors> =
        match state.State, state.CurrentPortId with
        | OperationalStatus.Docked _, Some _ -> Ok()
        | OperationalStatus.Docked _, None ->
            Error(InvalidStateTransition("docked with port ID", "docked without port ID"))
        | _ -> Error(InvalidStateTransition("docked", "at sea"))

    let canArriveAtPort (state: VesselState) : Result<unit, VesselCommandErrors> =
        match state.State with
        | OperationalStatus.AtSea -> Ok()
        | OperationalStatus.InRoute route ->
            if route.CurrentWaypointIndex + 1 = route.Waypoints.Length then
                Ok()
            else
                Error(
                    InvalidStateTransition(
                        "Not there yet, keep advancing on route",
                        string state.State
                    )
                )
        | _ -> Error(InvalidStateTransition("at sea or in route", string state.State))

    let canLoadCargo (vessel: VesselState) (cmd: LoadCargoCmd) : Result<unit, VesselCommandErrors> =
        result {
            // Vessel must be docked to load cargo
            match vessel.CurrentPortId with
            | None -> return! Error(InvalidStateTransition("docked at port", "at sea"))
            | Some currentPort ->
                // Vessel must be at cargo's origin port
                if currentPort <> cmd.OriginPortId then
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Vessel is at port {currentPort}, but cargo origin is {cmd.OriginPortId}"
                        )

                // Vessel can only carry one cargo at a time
                match vessel.CurrentCargo with
                | Some cargo ->
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Vessel already has cargo {cargo.CargoId} loaded"
                        )
                | None -> ()

                // Validate capacity constraints
                if cmd.CargoSpec.TotalWeight > vessel.MaxCargoCapacity.MaxWeight then
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Cargo weight {cmd.CargoSpec.TotalWeight} exceeds vessel capacity {vessel.MaxCargoCapacity.MaxWeight}"
                        )

                if cmd.CargoSpec.TotalVolume > vessel.MaxCargoCapacity.MaxVolume then
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Cargo volume {cmd.CargoSpec.TotalVolume} exceeds vessel capacity {vessel.MaxCargoCapacity.MaxVolume}"
                        )

                if cmd.CargoSpec.ContainerLoad.Count > vessel.MaxCargoCapacity.MaxContainers then
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Container count {cmd.CargoSpec.ContainerLoad.Count} exceeds vessel capacity {vessel.MaxCargoCapacity.MaxContainers}"
                        )

                return ()
        }

    let canUnloadCargo (vessel: VesselState) (cargoId: Guid) : Result<unit, VesselCommandErrors> =
        result {
            // Vessel must be docked to unload cargo
            match vessel.CurrentPortId with
            | None -> return! Error(InvalidStateTransition("docked at port", "at sea"))
            | Some currentPort ->
                // Cargo must be on the vessel
                match vessel.CurrentCargo with
                | Some cargo when cargo.CargoId = cargoId ->
                    // Vessel must be at cargo's destination port
                    if currentPort <> cargo.DestinationPortId then
                        return!
                            Error(
                                Shared.Api.Vessel.ValidationError
                                    $"Vessel is at port {currentPort}, but cargo destination is {cargo.DestinationPortId}"
                            )
                    return ()
                | Some cargo ->
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Different cargo {cargo.CargoId} is loaded on vessel"
                        )
                | None ->
                    return! Error(Shared.Api.Vessel.ValidationError "No cargo loaded on vessel")
        }

    let canStartRoute
        (vessel: VesselState)
        (destinationPortId: Guid)
        : Result<unit, VesselCommandErrors> =
        result {
            // If vessel has cargo, route destination must match cargo destination
            match vessel.CurrentCargo with
            | Some cargo ->
                if destinationPortId <> cargo.DestinationPortId then
                    return!
                        Error(
                            Shared.Api.Vessel.ValidationError
                                $"Route destination {destinationPortId} does not match cargo destination {cargo.DestinationPortId}"
                        )
                return ()
            | None -> return ()
        }

/// <summary>
/// Validates the incoming command with the vessel state given. Returns a list of events that shall
/// be created from this command if validation is passed
/// </summary>
/// <param name="state">Current vessel state</param>
/// <param name="command">Command to process</param>
/// <returns>List of events to apply, or validation error</returns>
let decide
    (state: VesselState option)
    (command: VesselCommand)
    : Result<VesselEvent list, VesselCommandErrors> =
    match command, state with

    // Register new vessel
    | RegisterVessel cmd, None ->
        result {
            let! _ = Validation.validateName cmd.Name
            let! _ = Validation.validateMmsi cmd.Mmsi
            let! _ = Validation.validateImo cmd.Imo
            let! _ = Validation.validateCrewSize cmd.CrewSize
            let! _ = Validation.validatePosition cmd.Position

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

    | RegisterVessel _, Some _ -> Error VesselIdAlreadyExists

    | UpdatePosition cmd, Some _ ->
        Ok [
            VesselPositionUpdated { Position = cmd.Position; UpdatedAt = cmd.Metadata.Timestamp }
        ]

    | UpdatePosition _, None -> Error Shared.Api.Vessel.VesselNotFound

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
                    UpdatedAt = cmd.Metadata.Timestamp
                }
            ]
        | Error err -> Error err

    | ArriveAtPort _, None -> Error Shared.Api.Vessel.VesselNotFound

    | DepartFromPort cmd, Some vessel ->
        result {
            let! _ = Validation.canDepartFromPort vessel
            let portId = vessel.CurrentPortId |> Option.defaultValue Guid.Empty
            return [
                VesselDeparted { FromPortId = portId; DepartedAt = cmd.Metadata.Timestamp }
                VesselOperationalStatusUpdated {
                    Status = OperationalStatus.AtSea
                    UpdatedAt = cmd.Metadata.Timestamp // Force this to happen after departed :)
                }
            ]
        }

    | DepartFromPort _, None -> Error Shared.Api.Vessel.VesselNotFound

    | UpdateOperationalStatus cmd, Some vessel ->
        result {
            // If starting a route, validate destination matches cargo destination
            match cmd.Status with
            | OperationalStatus.InRoute route ->
                let! _ = Validation.canStartRoute vessel route.DestinationPortId
                ()
            | _ -> ()

            return [
                VesselOperationalStatusUpdated {
                    Status = cmd.Status
                    UpdatedAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | UpdateOperationalStatus _, None -> Error Shared.Api.Vessel.VesselNotFound

    | DecommissionVessel cmd, Some _ ->
        Ok [
            VesselDecommissioned { DecommissionedAt = cmd.Metadata.Timestamp }
            VesselOperationalStatusUpdated {
                Status = OperationalStatus.Decommissioned
                UpdatedAt = cmd.Metadata.Timestamp
            }
        ]

    | DecommissionVessel _, None -> Error Shared.Api.Vessel.VesselNotFound

    | AdvanceRouteWaypoint cmd, Some vessel ->
        match vessel.State with
        | OperationalStatus.InRoute route ->

            let nextIndex = route.CurrentWaypointIndex + 1
            if nextIndex >= route.Waypoints.Length then
                Error NoMoreWaypoints
            else
                let updatedRoute = { route with CurrentWaypointIndex = nextIndex }
                let nextWaypoint = route.Waypoints.[nextIndex]

                Ok [
                    VesselPositionUpdated {
                        Position = nextWaypoint
                        UpdatedAt = cmd.Metadata.Timestamp
                    }
                    VesselOperationalStatusUpdated {
                        Status = OperationalStatus.InRoute updatedRoute
                        UpdatedAt = cmd.Metadata.Timestamp
                    }
                ]
        | _ -> Error NotInRoute

    | AdvanceRouteWaypoint _, None -> Error Shared.Api.Vessel.VesselNotFound

    | LoadCargo cmd, Some vessel ->
        result {
            let! _ = Validation.canLoadCargo vessel cmd

            return [
                CargoLoaded {
                    CargoId = cmd.CargoId
                    Spec = cmd.CargoSpec
                    OriginPortId = cmd.OriginPortId
                    DestinationPortId = cmd.DestinationPortId
                    LoadedAt = cmd.Metadata.Timestamp
                }
            ]
        }

    | LoadCargo _, None -> Error Shared.Api.Vessel.VesselNotFound

    | UnloadCargo cmd, Some vessel ->
        result {
            let! _ = Validation.canUnloadCargo vessel cmd.CargoId

            return [ CargoUnloaded { CargoId = cmd.CargoId; UnloadedAt = cmd.Metadata.Timestamp } ]
        }

    | UnloadCargo _, None -> Error Shared.Api.Vessel.VesselNotFound

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
            VesselType = evt.VesselType
            CrewSize = evt.CrewSize
            CurrentPortId = None
            MaxCargoCapacity = {
                MaxWeight = 50000.0<Shared.Api.Cargo.kg>
                MaxVolume = 1000.0<Shared.Api.Cargo.m^3>
                MaxContainers = 100
            }
            CurrentCargo = None
            RegisteredAt = evt.RegisteredAt
        }

    | VesselPositionUpdated evt, Some vessel -> Some { vessel with Position = evt.Position }

    | VesselArrived evt, Some vessel -> Some { vessel with CurrentPortId = Some evt.PortId }

    | VesselDeparted _, Some vessel -> Some { vessel with CurrentPortId = None }

    | VesselOperationalStatusUpdated evt, Some vessel -> Some { vessel with State = evt.Status }

    | VesselDecommissioned _, Some vessel -> Some vessel

    | CargoLoaded evt, Some vessel ->
        let cargoInfo = {
            CargoId = evt.CargoId
            Spec = evt.Spec
            OriginPortId = evt.OriginPortId
            DestinationPortId = evt.DestinationPortId
        }
        Some { vessel with CurrentCargo = Some cargoInfo }

    | CargoUnloaded _, Some vessel -> Some { vessel with CurrentCargo = None }

    | _, _ -> state
