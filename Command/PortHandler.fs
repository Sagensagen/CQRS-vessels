module Command.PortHandler

open System
open System.Linq
open Domain.Port
open Domain.Port.Event
open FsToolkit.ErrorHandling
open Marten
open Shared.Api.Port

type RegisterPort = {
    Id: Guid
    Name: string
    Locode: string option
    Country: string
    Latitude: float
    Longitude: float
    Timezone: string option
    MaxDocks: int
}

type PortCommand = RegisterPort of RegisterPort
// | DockVessel
// | UndockVessel
// | OpenPort
// | ClosePort

let private registerPort (command: RegisterPort) (session: IDocumentSession) =
    asyncResult {
        try
            // do!
            //     session.LoadAsync<PortView>(command.Id)
            //     |> Async.AwaitTask
            //     |> Async.map (fun v -> if obj.ReferenceEquals(v, null) then None else Some v)
            //     |> AsyncResult.requireNone "PortWithIdAlreadyExist"
            //
            // let ports =
            //     session
            //         .Query<PortView>()
            //         .Where(fun v -> v.Name.ToLower() = command.Name.ToLower())
            //         .ToArray()

            // do! ports |> Result.requireEmpty "Port with name already exist"
            let event: Event.PortRegistered = {
                Id = command.Id
                Name = command.Name
                Locode = command.Locode
                Country = command.Country
                Latitude = command.Latitude
                Longitude = command.Longitude
                Timezone = command.Timezone
                MaxDocks = command.MaxDocks
            }

            let stream =
                session.Events.StartStream<Event.PortRegistered>(command.Id, [| event :> obj |])

            do! session.SaveChangesAsync() |> Async.AwaitTask
            return stream.Id
        with e ->
            printfn $"Failed here: {e}"
            return! failwith e.Message
    }

let decide command session : Async<Result<Guid, string>> =
    match command with
    | RegisterPort newPort -> registerPort newPort session
// | DockVessel ->
// | UndockVessel ->
// | OpenPort ->
// | ClosePort ->
