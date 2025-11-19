module Server

#nowarn "20"

open System.Text.Json.Serialization
open Giraffe
open JasperFx.Events.Projections
open Marten
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Serilog
open Serilog.Events


module Program =
    let exitCode = 0

    let webApp = choose [ Handlers.Vessel.vesselHandler; Handlers.Port.vesselHandler ]

    [<EntryPoint>]
    let main args =
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Marten", LogEventLevel.Warning)
                .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger()

        let builder = WebApplication.CreateBuilder(args)
        builder.WebHost.UseUrls("http://localhost:5000") |> ignore

        builder.Services
            .AddSerilog()
            .AddMarten(fun (options: StoreOptions) ->
                options.Connection("Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres")

                options.AutoCreateSchemaObjects <- JasperFx.AutoCreate.All
                // options.Events.DatabaseSchemaName <- "events"

                options.Projections.Add(
                    Domain.Projections.VesselProjection.VesselViewProjection(),
                    ProjectionLifecycle.Inline
                )

                options.Projections.Add(
                    Domain.Projections.PortProjection.PortViewProjection(),
                    ProjectionLifecycle.Inline
                )

                let serializer = Marten.Services.SystemTextJsonSerializer()
                serializer.Configure(_.Converters.Add(JsonFSharpConverter()))

                options.Serializer serializer
                options.UseNewtonsoftForSerialization())
            .UseLightweightSessions()
        |> ignore

        let app = builder.Build()

        app
            .UseSerilogRequestLogging(fun options ->
                options.MessageTemplate <- "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms")

            .UseGiraffe(webApp)

        app.Run()

        exitCode
