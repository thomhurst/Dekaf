using System.Diagnostics.Tracing;

namespace Dekaf.Benchmarks;

[EventSource(Name = "Dekaf-AdminEvidence-Phases")]
public sealed class PhaseEvents : EventSource
{
    public static readonly PhaseEvents Log = new();

    [Event(1, Level = EventLevel.Informational)]
    public void Phase(string name) => WriteEvent(1, name);

    [Event(2, Level = EventLevel.Informational)]
    public void Clock(long timestamp) => WriteEvent(2, timestamp);
}
