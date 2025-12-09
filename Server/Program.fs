module Server

#nowarn "20"

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Serilog

module Program =

    let webApp =
        choose
            [ Api.Vessel.vesselHandler
              Api.Port.portHandler
              Simulation.simulationHandler
              Api.Route.routeHandler ]

    [<EntryPoint>]
    let main args =
        try
            Log.Logger <- Infrastructure.Logger.configureLogging().CreateLogger()

            Log.Information("Starting CQRS Vessel Management System")

            let builder = WebApplication.CreateBuilder(args)
            builder.WebHost.UseUrls("http://localhost:5000") |> ignore

            builder.Services.AddSerilog()
            |> Infrastructure.AkkaConfig.addAkkaServices
            |> Infrastructure.MartenConfig.configureMarten

            builder.Services.AddSingleton<CommandGateway.CommandGateway>(fun sp ->
                let actorSystem = sp.GetRequiredService<Akka.Actor.ActorSystem>()
                let documentStore = sp.GetRequiredService<Marten.IDocumentStore>()
                CommandGateway.CommandGateway(actorSystem, documentStore))

            let app = builder.Build()

            app.UseSerilogRequestLogging().UseGiraffe(webApp)

            Log.Information("CQRS Vessel Management System started successfully on http://localhost:5000")
            Log.Information("Event Store: PostgreSQL via Marten")
            Log.Information("Actor System: Akka.NET with manual Marten persistence")

            app.Run()

            0

        with ex ->
            Log.Fatal(ex, "Application terminated unexpectedly")
            1
