using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FurnitureEcommerce.Api.Logging;

/// <summary>
/// Where the logs go. Levels come from the <c>Serilog</c> section of the settings, so they can change without a
/// build. The outputs depend on the environment, and are chosen here because settings files merge sink lists by
/// position, which would mix the outputs of two environments.
/// </summary>
public static class LoggingSetup
{
    private const string ReadableTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}";

    // Alerts carry their event name (such as ManualRefundRequired), which is what to search for.
    private const string AlertTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {EventId} {Message:lj} <{SourceContext}>{NewLine}{Exception}";

    public static LoggerConfiguration WriteToOutputs(
        this LoggerConfiguration logger,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            var folder = Path.Combine(environment.ContentRootPath, "logs");

            return logger
                .WriteTo.Console(outputTemplate: ReadableTemplate)
                .WriteTo.File(
                    Path.Combine(folder, "api-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: AlertTemplate)
                // Warnings and worse in a file of their own, so a refund to make by hand is not lost among requests.
                .WriteTo.File(
                    Path.Combine(folder, "alerts-.log"),
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    outputTemplate: AlertTemplate);
        }

        if (environment.IsEnvironment("E2E") || environment.IsEnvironment("Testing"))
        {
            return logger.WriteTo.Console(outputTemplate: ReadableTemplate);
        }

        // A deployed API writes one JSON object per line. Hosting platforms collect the console and can search
        // these by level, event name or order id. Disk may not survive a restart there, so no files.
        return logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
}
