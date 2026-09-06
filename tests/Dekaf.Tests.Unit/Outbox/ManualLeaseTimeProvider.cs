using System.Threading.Channels;

namespace Dekaf.Tests.Unit.Outbox;

internal sealed class ManualLeaseTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<LeaseTimer> _timers = [];
    private readonly Channel<TimeSpan> _scheduled = Channel.CreateUnbounded<TimeSpan>();
    private long _ticks = TimeSpan.TicksPerSecond;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new LeaseTimer(this, callback, state);
        lock (_gate)
            _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public async Task WaitForTimerAsync(TimeSpan dueTime)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (await _scheduled.Reader.ReadAsync(timeout.Token) != dueTime) { }
    }

    public void Advance(TimeSpan elapsed)
    {
        List<LeaseTimer> due = [];
        lock (_gate)
        {
            Interlocked.Add(ref _ticks, elapsed.Ticks);
            for (var index = 0; index < _timers.Count; index++)
            {
                var timer = _timers[index];
                if (timer.Due > _ticks)
                    continue;
                timer.Due = timer.Period > TimeSpan.Zero ? _ticks + timer.Period.Ticks : long.MaxValue;
                due.Add(timer);
            }
        }

        foreach (var timer in due)
            timer.Invoke();
    }

    private sealed class LeaseTimer(ManualLeaseTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        internal long Due { get; set; } = long.MaxValue;
        internal TimeSpan Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner._ticks + dueTime.Ticks;
                Period = period;
            }

            if (dueTime != Timeout.InfiniteTimeSpan)
                owner._scheduled.Writer.TryWrite(dueTime);
            return true;
        }

        internal void Invoke() => callback(state);
        public void Dispose()
        {
            lock (owner._gate)
                owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
