using System.Globalization;

namespace Dekaf.Tests.Integration;

internal sealed class ReassignmentSequenceOracle
{
    private readonly bool _allowDuplicates;
    private readonly int _messagesPerPartition;
    private readonly bool[][] _seenSequences;
    private readonly int[] _nextLogicalSequences;
    private readonly long[] _nextBrokerOffsets;
    private int _remainingUnique;

    public ReassignmentSequenceOracle(
        int partitionCount,
        int messagesPerPartition,
        bool allowDuplicates)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(messagesPerPartition, 1);

        _allowDuplicates = allowDuplicates;
        _messagesPerPartition = messagesPerPartition;
        _seenSequences = new bool[partitionCount][];
        for (var partition = 0; partition < partitionCount; partition++)
            _seenSequences[partition] = new bool[messagesPerPartition];

        _nextLogicalSequences = new int[partitionCount];
        _nextBrokerOffsets = new long[partitionCount];
        _remainingUnique = checked(partitionCount * messagesPerPartition);
    }

    public bool IsComplete => _remainingUnique == 0;

    public void Observe(
        string topic,
        int partition,
        long offset,
        string? key,
        string? value)
    {
        if ((uint)partition >= (uint)_seenSequences.Length)
            throw new InvalidOperationException($"Record for {topic} has invalid partition {partition}.");

        var expectedOffset = _nextBrokerOffsets[partition];
        if (offset != expectedOffset)
        {
            throw new InvalidOperationException(
                $"Broker offset gap for {topic}-{partition}: expected {expectedOffset}, actual {offset}.");
        }
        _nextBrokerOffsets[partition] = expectedOffset + 1;

        var sequence = ParseSequence(topic, partition, key, value);
        if (_seenSequences[partition][sequence])
        {
            if (_allowDuplicates)
                return;

            throw new InvalidOperationException(
                $"Unexpected duplicate for {topic}-{partition}@{offset}: sequence {sequence}.");
        }

        if (!_allowDuplicates)
        {
            var expectedSequence = _nextLogicalSequences[partition];
            if (sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    $"Unexpected logical sequence for {topic}-{partition}@{offset}: " +
                    $"expected {expectedSequence}, actual {sequence}.");
            }
            _nextLogicalSequences[partition] = expectedSequence + 1;
        }

        _seenSequences[partition][sequence] = true;
        _remainingUnique--;
    }

    public void EnsureComplete()
    {
        if (_remainingUnique != 0)
        {
            throw new InvalidOperationException(
                $"Timed out with {_remainingUnique} logical record(s) unread.");
        }
    }

    private int ParseSequence(
        string topic,
        int partition,
        string? key,
        string? value)
    {
        if (value is null || !string.Equals(key, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Corrupt record for {topic}-{partition}: key '{key}', value '{value}'.");
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0
            || separator == value.Length - 1
            || !int.TryParse(
                value.AsSpan(0, separator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var encodedPartition)
            || encodedPartition != partition
            || !int.TryParse(
                value.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var sequence)
            || (uint)sequence >= (uint)_messagesPerPartition)
        {
            throw new InvalidOperationException(
                $"Corrupt record for {topic}-{partition}: value '{value}'.");
        }

        return sequence;
    }
}
