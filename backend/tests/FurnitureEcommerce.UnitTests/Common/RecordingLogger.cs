using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.UnitTests.Common;

/// <summary>Keeps what was logged, so a test can check that an event someone must notice is written.</summary>
public class RecordingLogger<T> : ILogger<T>
{
    public record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

    private readonly List<Entry> entries = [];

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (entries)
            {
                return [.. entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (entries)
        {
            entries.Add(new Entry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    /// <summary>The entries of one event, by its name.</summary>
    public IReadOnlyList<Entry> Of(EventId eventId) =>
        Entries.Where(entry => entry.EventId.Name == eventId.Name).ToList();
}
