module Domain.VesselErrors

/// Domain-level errors for vessel operations
/// These represent business rule violations and domain constraints
type VesselError =
    | VesselNotFound
    | VesselAlreadyExists
    | VesselAlreadyDecommissioned
    | VesselAlreadyDeparted
    | VesselAlreadyArrived
    | InvalidStateTransition of expected: string * actual: string
    | PortNotAvailable
    | NoDockingSpace
    | CargoNotFound
    | ValidationError of message: string
    | PersistenceError of message: string
    | NoActiveRoute
    | NotInRoute
    | NoMoreWaypoints
    | RouteCalculationFailed of message: string
    | DestinationPortNotFound
