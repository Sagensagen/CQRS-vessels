module ActorPaths

open System

[<Literal>]
let VesselActorPrefix = "vessel"

[<Literal>]
let PortActorPrefix = "port"

[<Literal>]
let SagaCoordinatorName = "saga-coordinator"

[<Literal>]
let DockingSagaPrefix = "docking-saga"

let vesselActorName (vesselId: Guid) = sprintf "%s-%s" VesselActorPrefix (vesselId.ToString())

let portActorName (portId: Guid) = sprintf "%s-%s" PortActorPrefix (portId.ToString())

let dockingSagaName (sagaId: Guid) = sprintf "%s-%s" DockingSagaPrefix (sagaId.ToString())

let vesselActorPath (vesselId: Guid) = sprintf "/user/%s-%s" VesselActorPrefix (vesselId.ToString())

let portActorPath (portId: Guid) = sprintf "/user/%s-%s" PortActorPrefix (portId.ToString())

let sagaCoordinatorPath = sprintf "/user/%s" SagaCoordinatorName
