module Domain.PortErrors

/// Domain-level errors for port operations
/// These represent business rule violations and domain constraints
type PortError =
    | PortNotFound
    | PortAlreadyExists
    | PortAlreadyOpen
    | PortAlreadyClosed
    | NoDockingSpaceAvailable
    | VesselNotDocked
    | VesselAlreadyDocked
    | ReservationNotFound
    | ReservationAlreadyExists
    | InvalidStateTransition of expected: string * actual: string
    | ValidationError of message: string
    | PersistenceError of message: string
