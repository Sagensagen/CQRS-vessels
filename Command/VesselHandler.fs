module Command.VesselHandler

open System
open Domain.Vessel.Event
open FsToolkit.ErrorHandling
open Marten
open System.Linq
open Domain.Vessel
open Shared.Api.Vessel

type RegisterVessel = {
    Id: Guid
    Name: string
    Position: VesselPosition
    Mmsi: int
    Imo: int option
    Flag: string
    Length: float option
    Beam: float option
    Draught: float option
    VesselType: VesselType
    CrewSize: int
}

type DepartVessel = {
    VesselId: Guid
    FromPort: string
    Inserted: DateTimeOffset
}

type ArriveVessel = {
    VesselId: Guid
    AtPort: string
    Inserted: DateTimeOffset
}

type StartLoading = {
    VesselId: Guid
    CargoId: Guid
    Inserted: DateTimeOffset
}

type StartUnloading = {
    VesselId: Guid
    CargoId: Guid
    Inserted: DateTimeOffset
}

type CompleteLoading = {
    VesselId: Guid
    CargoId: Guid
    Inserted: DateTimeOffset
}

type CompleteUnloading = {
    VesselId: Guid
    CargoId: Guid
    Inserted: DateTimeOffset
}

type UpdateVesselPosition = {
    VesselId: Guid
    Position: VesselPosition
    Inserted: DateTimeOffset
}

type DecommissionVessel = { VesselId: Guid }

type VesselCommand =
    | RegisterVessel of RegisterVessel
    | DepartVessel of DepartVessel
    | ArriveVessel of ArriveVessel
    | StartLoading of StartLoading
    | StartUnloading of StartUnloading
    | CompleteLoading of CompleteLoading
    | CompleteUnloading of CompleteUnloading
    | UpdateVesselPosition of UpdateVesselPosition
    | DecommissionVessel of DecommissionVessel
    | Other

// ---------------------COMMAND HANDLERS---------------------
let private registerVessel
    (command: RegisterVessel)
    (session: IDocumentSession)
    : Async<Result<Guid, VesselCommandErrors>> =
    asyncResult {
        // Required unique id
        do!
            session.LoadAsync<VesselView>(command.Id)
            |> Async.AwaitTask
            |> Async.map (fun v -> if obj.ReferenceEquals(v, null) then None else Some v)
            |> AsyncResult.requireNone VesselIdAlreadyExists

        // Require unique name
        let vessels =
            session
                .Query<VesselView>()
                .Where(fun v -> v.Name.ToLower() = command.Name.ToLower())
                .ToArray()

        do! vessels |> Result.requireEmpty VesselCommandErrors.VesselNotFound

        let event: VesselRegistered = {
            Id = command.Id
            Name = command.Name
            Position = command.Position
            Mmsi = command.Mmsi
            Imo = command.Imo
            Flag = command.Flag
            Length = command.Length
            Beam = command.Beam
            Draught = command.Draught
            State = Docked "Oslo"
            VesselType = command.VesselType
            CrewSize = command.CrewSize
        }
        let stream =
            session.Events.StartStream<VesselRegistered>(command.Id, [| event :> obj |])
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private departVessel (command: DepartVessel) (session: IDocumentSession) =
    asyncResult {
        // Required vessel exist
        let! vessel = session.LoadAsync<VesselView>(command.VesselId) |> Async.AwaitTask

        match vessel.State with
        | Decommissioned ->
            // Do not allow a vessel out of service to depart!
            return! Error VesselAlreadyDecommissioned
        | AtSea ->
            // Do not depart twice. Maybe should only check if is at port, else error
            return! Error VesselAlreadyDeparted
        | _ ->
            let event: Event.VesselDeparted = { FromPort = command.FromPort }

            let stream = session.Events.Append(command.VesselId, event)
            do! session.SaveChangesAsync() |> Async.AwaitTask
            return stream.Id
    }

let private arriveVessel (command: ArriveVessel) (session: IDocumentSession) =
    asyncResult {
        printfn $"Command vesselid {command.VesselId}"
        let! vessel = session.LoadAsync<VesselView>(command.VesselId) |> Async.AwaitTask
        do! vessel.State = Decommissioned |> Result.requireFalse VesselAlreadyDecommissioned

        // Create new event here that updates port vessels+1??
        // USE OPTIMISTIC CONCURRENCY FOR PORT VALIDATION/EVENT
        match vessel.State with
        // Decommissioned ships should be allowed to port
        | AtSea ->
            printfn "Correct state on vessel"
            let event: Event.VesselArrived = { AtPort = command.AtPort }
            let stream = session.Events.Append(command.VesselId, event)

            do! session.SaveChangesAsync() |> Async.AwaitTask
            return stream.Id
        | _ ->
            printfn "Wrong state on vessel"
            return! Error VesselIsAlreadyArrived
    }

let private startLoading (command: StartLoading) (session: IDocumentSession) =
    asyncResult {
        let event: Event.VesselLoadingStarted = { CargoId = command.CargoId }
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private startUnloading (command: StartUnloading) (session: IDocumentSession) =
    asyncResult {
        let event: Event.VesselUnloadingCompleted = { CargoId = command.CargoId }
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private completeLoading (command: CompleteLoading) (session: IDocumentSession) =
    asyncResult {
        let event: Event.VesselLoadingCompleted = { CargoId = command.CargoId }
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private completeUnloading (command: CompleteUnloading) (session: IDocumentSession) =
    asyncResult {
        let event: Event.VesselUnloadingCompleted = { CargoId = command.CargoId }
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private updateVesselPosition (command: UpdateVesselPosition) (session: IDocumentSession) =
    asyncResult {
        let event: Event.VesselPositionUpdated = command.Position
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

let private decommissionVessel (command: DecommissionVessel) (session: IDocumentSession) =
    asyncResult {
        let event: VesselDecommissioned = { At = DateTimeOffset.UtcNow }
        let stream = session.Events.Append(command.VesselId, event)
        do! session.SaveChangesAsync() |> Async.AwaitTask
        return stream.Id
    }

/// <summary>
/// Command handler
/// </summary>
/// <param name="command"></param>
/// <param name="session"></param>
let decide command session : Async<Result<Guid, VesselCommandErrors>> =
    match command with
    | RegisterVessel c -> registerVessel c session
    | DepartVessel c -> departVessel c session
    | ArriveVessel c -> arriveVessel c session
    | StartLoading c -> startLoading c session
    | StartUnloading c -> startUnloading c session
    | CompleteLoading c -> completeLoading c session
    | CompleteUnloading c -> completeUnloading c session
    | UpdateVesselPosition c -> updateVesselPosition c session
    | DecommissionVessel c -> decommissionVessel c session
    | Other -> AsyncResult.error VesselCommandErrors.VesselNotFound
