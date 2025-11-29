module Infrastructure.AkkaConfig

open Akka.Hosting
open Microsoft.Extensions.DependencyInjection

[<Literal>]
let akkaActorSystemName = "VesselManagementSystem"

let configureAkka (builder: AkkaConfigurationBuilder) =
    builder.WithActors(fun system registry -> ()) |> ignore

let addAkkaServices (services: IServiceCollection) =
    services.AddAkka(akkaActorSystemName, fun builder _ -> configureAkka builder)
    |> ignore

    services
