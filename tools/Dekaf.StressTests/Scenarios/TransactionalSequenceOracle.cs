using System.Collections;
using System.Globalization;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Verifies the byte-visible outcome of the transactional stress scenario. Workload
/// keys carry committed/aborted ordinals, while one committed sentinel per partition
/// proves the read_committed consumer advanced past every earlier record.
/// </summary>
internal sealed class TransactionalSequenceOracle
{
    private const int MaxFailureSamples = 10;

    private readonly string _keyPrefix;
    private readonly long _committedMessages;
    private readonly long _abortedMessages;
    private readonly BitArray _seenCommitted;
    private readonly BitArray _seenSentinels;
    private readonly List<string> _failureSamples = [];
    private long _deliveredMessages;
    private long _duplicateMessages;
    private long _leakedAbortedMessages;
    private long _unexpectedMessages;
    private int _sentinelsSeen;

    internal TransactionalSequenceOracle(
        string runId,
        long committedMessages,
        long abortedMessages,
        int partitionCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(committedMessages);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(committedMessages, int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfNegative(abortedMessages);
        ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);

        _keyPrefix = $"{runId}:";
        _committedMessages = committedMessages;
        _abortedMessages = abortedMessages;
        _seenCommitted = new BitArray((int)committedMessages);
        _seenSentinels = new BitArray(partitionCount);
    }

    internal bool AllSentinelsSeen => _sentinelsSeen == _seenSentinels.Length;

    internal static string CommittedKey(string runId, long index) =>
        $"{runId}:c:{index.ToString(CultureInfo.InvariantCulture)}";

    internal static string AbortedKey(string runId, long index) =>
        $"{runId}:a:{index.ToString(CultureInfo.InvariantCulture)}";

    internal static string SentinelKey(string runId, int partition) =>
        $"{runId}:s:{partition.ToString(CultureInfo.InvariantCulture)}";

    internal void Observe(string? key)
    {
        if (key is null || !key.StartsWith(_keyPrefix, StringComparison.Ordinal))
            return;

        var payload = key.AsSpan(_keyPrefix.Length);
        if (payload.Length < 3 || payload[1] != ':')
        {
            RecordUnexpected(key);
            return;
        }

        switch (payload[0])
        {
            case 'c':
                ObserveCommitted(key, payload[2..]);
                break;
            case 'a':
                ObserveAborted(key, payload[2..]);
                break;
            case 's':
                ObserveSentinel(key, payload[2..]);
                break;
            default:
                RecordUnexpected(key);
                break;
        }
    }

    internal TransactionVerificationSnapshot CreateSnapshot(long acceptedMessages)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedMessages);

        var samples = new List<string>(_failureSamples);
        for (var index = 0; index < _seenCommitted.Length && samples.Count < MaxFailureSamples; index++)
        {
            if (!_seenCommitted[index])
                samples.Add($"Missing committed index {index:N0}.");
        }

        for (var partition = 0; partition < _seenSentinels.Length && samples.Count < MaxFailureSamples; partition++)
        {
            if (!_seenSentinels[partition])
                samples.Add($"Missing terminal sentinel for partition {partition}.");
        }

        return new TransactionVerificationSnapshot
        {
            AcceptedMessages = acceptedMessages,
            CommittedMessages = _committedMessages,
            AbortedMessages = _abortedMessages,
            DeliveredMessages = _deliveredMessages,
            DuplicateMessages = _duplicateMessages,
            ShortfallMessages = _committedMessages - _deliveredMessages,
            LeakedAbortedMessages = _leakedAbortedMessages,
            UnexpectedMessages = _unexpectedMessages,
            MissingSentinelPartitions = _seenSentinels.Length - _sentinelsSeen,
            FailureSamples = samples
        };
    }

    private void ObserveCommitted(string key, ReadOnlySpan<char> indexText)
    {
        if (!long.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            index < 0 || index >= _committedMessages)
        {
            RecordUnexpected(key);
            return;
        }

        if (_seenCommitted[(int)index])
        {
            _duplicateMessages++;
            AddFailure($"Duplicate committed index {index:N0}.");
            return;
        }

        _seenCommitted[(int)index] = true;
        _deliveredMessages++;
    }

    private void ObserveAborted(string key, ReadOnlySpan<char> indexText)
    {
        if (!long.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            index < 0 || index >= _abortedMessages)
        {
            RecordUnexpected(key);
            return;
        }

        _leakedAbortedMessages++;
        AddFailure($"Aborted index {index:N0} leaked into read_committed output.");
    }

    private void ObserveSentinel(string key, ReadOnlySpan<char> partitionText)
    {
        if (!int.TryParse(partitionText, NumberStyles.None, CultureInfo.InvariantCulture, out var partition) ||
            partition < 0 || partition >= _seenSentinels.Length)
        {
            RecordUnexpected(key);
            return;
        }

        if (_seenSentinels[partition])
        {
            _duplicateMessages++;
            AddFailure($"Duplicate terminal sentinel for partition {partition}.");
            return;
        }

        _seenSentinels[partition] = true;
        _sentinelsSeen++;
    }

    private void RecordUnexpected(string key)
    {
        _unexpectedMessages++;
        AddFailure($"Unexpected transactional key '{key}'.");
    }

    private void AddFailure(string sample)
    {
        if (_failureSamples.Count < MaxFailureSamples)
            _failureSamples.Add(sample);
    }
}
