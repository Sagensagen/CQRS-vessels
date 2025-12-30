module ActorPaths

open System

[<Literal>]
let VesselActorPrefix = "vessel"

[<Literal>]
let PortActorPrefix = "port"

[<Literal>]
let CargoActorPrefix = "cargo"

[<Literal>]
let DockingSagaPrefix = "docking-saga"

[<Literal>]
let CargoLoadingSagaPrefix = "cargo-loading-saga"

[<Literal>]
let CargoUnloadingSagaPrefix = "cargo-unloading-saga"

let vesselActorName (vesselId: Guid) = sprintf "%s-%s" VesselActorPrefix (vesselId.ToString())

let portActorName (portId: Guid) = sprintf "%s-%s" PortActorPrefix (portId.ToString())

let cargoActorName (cargoId: Guid) = sprintf "%s-%s" CargoActorPrefix (cargoId.ToString())

let dockingSagaName (sagaId: Guid) = sprintf "%s-%s" DockingSagaPrefix (sagaId.ToString())

let cargoLoadingSagaName (sagaId: Guid) = sprintf "%s-%s" CargoLoadingSagaPrefix (sagaId.ToString())

let cargoUnloadingSagaName (sagaId: Guid) =
    sprintf "%s-%s" CargoUnloadingSagaPrefix (sagaId.ToString())

let vesselActorPath (vesselId: Guid) = sprintf "/user/%s-%s" VesselActorPrefix (vesselId.ToString())

let portActorPath (portId: Guid) = sprintf "/user/%s-%s" PortActorPrefix (portId.ToString())

let cargoActorPath (cargoId: Guid) = sprintf "/user/%s-%s" CargoActorPrefix (cargoId.ToString())
