module Command.Tests.MartenFixture

open System
open System.Text.Json.Serialization
open JasperFx.Events
open Weasel.Core
open Testcontainers.PostgreSql
open Marten
open Marten.Services
open JasperFx

/// <summary>
/// Lightweight postgresql testcontainer for testing. Should be identical to production config
/// </summary>
type MartenFixture() =
    let container =
        PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build()

    do container.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

    member _.CreateStore() =
        DocumentStore.For(fun options ->
            options.Connection(container.GetConnectionString())
            options.AutoCreateSchemaObjects <- AutoCreate.All
            options.DatabaseSchemaName <- "test_events"

            options.Events.StreamIdentity <- StreamIdentity.AsGuid

            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselRegisteredEvt>)
            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselPositionUpdatedEvt>)
            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselArrivedEvt>)
            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselDepartedEvt>)
            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselOperationalStatusUpdatedEvt>)
            options.Events.AddEventType(typeof<Domain.VesselAggregate.VesselDecommissionedEvt>)

            options.Events.AddEventType(typeof<Domain.PortAggregate.PortRegisteredEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.VesselDockingReservedEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.DockingConfirmedEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.DockingReservationExpiredEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.VesselUndockedEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.PortOpenedEvt>)
            options.Events.AddEventType(typeof<Domain.PortAggregate.PortClosedEvt>)

            Query.VesselProjection.registerProjection options
            Query.PortProjection.registerProjection options

            options.UseSystemTextJsonForSerialization()
            let serializer = SystemTextJsonSerializer()
            serializer.Configure(fun opts -> opts.Converters.Add(JsonFSharpConverter()))
            options.Serializer serializer

            options.Advanced.DuplicatedFieldEnumStorage <- EnumStorage.AsString
            options.Advanced.DuplicatedFieldUseTimestampWithoutTimeZoneForDateTime <- false)

    interface IDisposable with
        member _.Dispose() =
            container.DisposeAsync().AsTask() |> Async.AwaitTask |> Async.RunSynchronously
