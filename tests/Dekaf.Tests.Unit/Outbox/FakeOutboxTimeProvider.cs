namespace Dekaf.Tests.Unit.Outbox;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> for outbox tests, covering both the wall clock
/// (used by the EF store for lease expiry) and the timestamp clock (used by the relay for
/// lease age). <see cref="Advance"/> moves both together.
/// </summary>
internal sealed class FakeOutboxTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
    // Starts nonzero: the relay treats timestamp 0 as "never acquired".
    private long _timestamp = TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => Volatile.Read(ref _timestamp);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan delta)
    {
        _now += delta;
        Interlocked.Add(ref _timestamp, delta.Ticks);
    }
}
