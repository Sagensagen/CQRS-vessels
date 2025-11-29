module Infrastructure.Logger

open Serilog
open Serilog.Events
open Serilog.Sinks.SystemConsole.Themes

let configureLogging () =
    LoggerConfiguration()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        .MinimumLevel.Override("Marten", LogEventLevel.Information)
        .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
        .MinimumLevel.Override("Akka", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console(theme = AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput = true)
