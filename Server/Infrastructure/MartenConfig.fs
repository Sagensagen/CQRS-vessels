module Infrastructure.MartenConfig

open System.Text.Json.Serialization
open JasperFx
open Marten
open Microsoft.Extensions.DependencyInjection
open Weasel.Core

let configureMarten (services: IServiceCollection) =
    services.AddMarten(fun (options: StoreOptions) ->
        let connectionString =
            "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres"

        options.Connection(connectionString)

        options.AutoCreateSchemaObjects <- AutoCreate.All

        options.UseSystemTextJsonForSerialization()

        let serializer = Marten.Services.SystemTextJsonSerializer()
        serializer.Configure(_.Converters.Add(JsonFSharpConverter()))
        options.Serializer serializer

        Query.VesselProjection.registerProjection options
        Query.PortProjection.registerProjection options


        options.Advanced.DuplicatedFieldEnumStorage <- EnumStorage.AsString
        options.Advanced.DuplicatedFieldUseTimestampWithoutTimeZoneForDateTime <- false

    )
    |> ignore

    services
