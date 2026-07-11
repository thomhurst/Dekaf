namespace Dekaf.Consumer;

internal readonly record struct PrefetchFailureKey(int BrokerId, int ConnectionIndex);

internal readonly record struct PrefetchPosition(TopicPartition Partition, long Offset);

internal readonly record struct PrefetchFailureDecision(int DelayMs, bool IsTerminal, int Count);

internal sealed class PrefetchFailureTracker
{
    private readonly Dictionary<PrefetchFailureKey, FailureState> _failures = [];
    private readonly object _lock = new();
    private readonly int _terminalThreshold;
    private readonly int _initialDelayMs;
    private readonly int _maxDelayMs;

    public PrefetchFailureTracker(int terminalThreshold, int initialDelayMs, int maxDelayMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(terminalThreshold, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelayMs, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelayMs, initialDelayMs);

        _terminalThreshold = terminalThreshold;
        _initialDelayMs = initialDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public PrefetchFailureDecision Observe(
        PrefetchFailureKey key,
        PrefetchPosition[] positions,
        bool deterministic)
    {
        lock (_lock)
        {
            int count;
            PrefetchPosition[] storedPositions;
            if (_failures.TryGetValue(key, out var current)
                && current.Deterministic == deterministic
                && PositionsEqual(current.Positions, positions))
            {
                count = current.Count + 1;
                storedPositions = current.Positions;
            }
            else
            {
                count = 1;
                storedPositions = (PrefetchPosition[])positions.Clone();
            }

            _failures[key] = new FailureState(deterministic, storedPositions, count);

            var shift = Math.Min(count - 1, 30);
            var delayMs = (int)Math.Min(_maxDelayMs, (long)_initialDelayMs << shift);
            return new PrefetchFailureDecision(
                delayMs,
                IsTerminal: deterministic && count >= _terminalThreshold,
                count);
        }
    }

    public void Reset(PrefetchFailureKey key)
    {
        lock (_lock)
        {
            _failures.Remove(key);
        }
    }

    private static bool PositionsEqual(PrefetchPosition[] left, PrefetchPosition[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private readonly record struct FailureState(bool Deterministic, PrefetchPosition[] Positions, int Count);
}
