namespace Dekaf.StressTests.Scenarios;

/// <summary>Validates the loaded fault experiment independently of consumer positions.</summary>
internal sealed class FollowerRecoveryOracle
{
    internal const int FaultInterval = 16;
    private readonly long[] _endOffsets;
    private readonly long[] _nextOffsets;
    private readonly long[] _pendingRetries;
    private readonly int[] _followerResponses;
    private long _faults;
    private long _retries;
    private long _progressRetries;
    private long _unmatchedLeaderResponses;
    private long _passes;
    private int _completedPartitions;
    private int _epoch;
    private int _rewinding;
    private long _violations;
    private string? _lastViolation;

    public int Epoch
    {
        get
        {
            var epoch = Volatile.Read(ref _epoch);
            return Volatile.Read(ref _rewinding) == 0 ? epoch : -1;
        }
    }
    public string? LastViolation => Volatile.Read(ref _lastViolation);
    public void BeginPhase() => Interlocked.Increment(ref _epoch);
    public void CompleteRewind() => Volatile.Write(ref _rewinding, 0);

    public FollowerRecoveryOracle(long[] endOffsets)
    {
        if (endOffsets.Length == 0 || Array.Exists(endOffsets, static offset => offset <= 0 || offset > int.MaxValue))
            throw new ArgumentException("Every recovery partition must contain seeded records.", nameof(endOffsets));
        _endOffsets = (long[])endOffsets.Clone();
        _nextOffsets = new long[endOffsets.Length];
        _pendingRetries = new long[endOffsets.Length];
        Array.Fill(_pendingRetries, -1);
        _followerResponses = new int[endOffsets.Length];
    }

    public bool ObserveFollowerData(int partition, long requestedOffset, int epoch)
    {
        if (epoch < 0 || epoch != Epoch)
            return false;
        if (requestedOffset < 0 || requestedOffset >= _endOffsets[partition])
            throw Fail("Follower returned data outside the seeded interval.");
        if (Interlocked.Increment(ref _followerResponses[partition]) % FaultInterval != 0)
            return false;
        // The finite seeded interval fits in 32 bits. Pack the replay/phase generation
        // with its offset so a response racing a seek cannot leave a live stale fault.
        var expected = Volatile.Read(ref _pendingRetries[partition]);
        if (expected != -1 && (int)(expected >> 32) == epoch)
            return false;
        var pending = ((long)epoch << 32) | (uint)requestedOffset;
        if (Interlocked.CompareExchange(ref _pendingRetries[partition], pending, expected) != expected)
            return false;
        Interlocked.Increment(ref _faults);
        return true;
    }

    public void ObserveLeaderResponse(int partition, long requestedOffset, int epoch)
    {
        var expected = Volatile.Read(ref _pendingRetries[partition]);
        if (epoch < 0 || epoch != Epoch || expected == -1 || (int)(expected >> 32) != epoch)
            return;
        var faultOffset = (long)(uint)expected;
        // A response alone cannot establish request causality across overlapping
        // fetches and replay seeks. Count exact matches and advances already proven
        // by record delivery; leave other responses unmatched. The strict sequence
        // oracle, progress watchdog and full-phase checks remain authoritative.
        if (requestedOffset != faultOffset
            && (requestedOffset < faultOffset || requestedOffset > Volatile.Read(ref _nextOffsets[partition])))
        {
            Interlocked.Increment(ref _unmatchedLeaderResponses);
            return;
        }
        if (Interlocked.CompareExchange(ref _pendingRetries[partition], -1, expected) == expected)
        {
            if (requestedOffset == faultOffset)
                Interlocked.Increment(ref _retries);
            else
                Interlocked.Increment(ref _progressRetries);
        }
    }

    public bool RecordConsumed(int partition, long offset)
    {
        if (offset != _nextOffsets[partition] || offset >= _endOffsets[partition])
            throw Fail($"Replay gap or duplicate in partition {partition}: got {offset}, expected {_nextOffsets[partition]}.");
        _nextOffsets[partition]++;
        if (_nextOffsets[partition] != _endOffsets[partition] || ++_completedPartitions != _endOffsets.Length)
            return false;
        Volatile.Write(ref _rewinding, 1);
        Interlocked.Increment(ref _epoch);
        Array.Clear(_nextOffsets);
        _completedPartitions = 0;
        _passes++;
        return true;
    }

    public FollowerRecoverySnapshot Snapshot() => new(
        Interlocked.Read(ref _faults), Interlocked.Read(ref _retries), _passes,
        Interlocked.Read(ref _progressRetries), Interlocked.Read(ref _violations), Interlocked.Read(ref _unmatchedLeaderResponses));

    private InvalidOperationException Fail(string message)
    {
        Volatile.Write(ref _lastViolation, message);
        Interlocked.Increment(ref _violations);
        return new InvalidOperationException(message);
    }
}

internal sealed record FollowerRecoverySnapshot(long InjectedFaults, long MatchingLeaderResponses, long CompletePasses,
    long LeaderResponsesAfterVerifiedProgress = 0, long Violations = 0, long UnmatchedLeaderResponses = 0)
{
    public FollowerRecoverySnapshot Since(FollowerRecoverySnapshot start) => new(
        InjectedFaults - start.InjectedFaults,
        MatchingLeaderResponses - start.MatchingLeaderResponses,
        CompletePasses - start.CompletePasses,
        LeaderResponsesAfterVerifiedProgress - start.LeaderResponsesAfterVerifiedProgress,
        Violations - start.Violations,
        UnmatchedLeaderResponses - start.UnmatchedLeaderResponses);
}
