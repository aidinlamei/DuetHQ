using Serilog.Core;
using Serilog.Events;

namespace DuetHQ.TestSupport;

/// <summary>
/// Keeps every emitted <see cref="LogEvent"/> raw (template, properties, destructured values), not rendered text,
/// so privacy tests can assert on what would reach a real sink (INV-10).
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private readonly Lock _gate = new();
    private readonly List<LogEvent> _events = [];

    /// <summary>A snapshot: later emits do not change a list that was already returned.</summary>
    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        lock (_gate)
        {
            _events.Add(logEvent);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }
}
