using System.Globalization;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

internal sealed class LeaderHandoffSequenceOracle
{
    private readonly IReadOnlyList<RecordMetadata> _acknowledgements;
    private long _nextOffset;
    private int _nextSequence;

    public LeaderHandoffSequenceOracle(
        IReadOnlyList<RecordMetadata> acknowledgements,
        int messageCount,
        bool enableIdempotence)
    {
        if (acknowledgements.Count != messageCount)
            throw new InvalidOperationException(
                $"Expected {messageCount} acknowledgements, actual {acknowledgements.Count}.");

        long previousOffset = -1;
        for (var index = 0; index < acknowledgements.Count; index++)
        {
            var offset = acknowledgements[index].Offset;
            if (offset <= previousOffset || (enableIdempotence && offset != index))
                throw new InvalidOperationException(
                    $"Invalid acknowledgement offset at index {index}: previous {previousOffset}, actual {offset}.");

            previousOffset = offset;
        }

        _acknowledgements = acknowledgements;
    }

    public bool IsComplete => _nextSequence == _acknowledgements.Count;

    public void Observe(int partition, long offset, string? key, string? value)
    {
        if (IsComplete)
            throw new InvalidOperationException($"Unexpected extra record at offset {offset}: {value}.");

        var expected = _nextSequence.ToString(CultureInfo.InvariantCulture);
        if (partition != 0 || offset != _nextOffset || key != expected || value != expected)
            throw new InvalidOperationException(
                $"Record mismatch: expected sequence {_nextSequence} at offset {_nextOffset}, " +
                $"actual partition={partition}, offset={offset}, key={key}, value={value}.");

        // Each ProduceAsync is awaited before the next input. Retry copies must precede
        // its acknowledged copy, and the next input starts immediately after that offset.
        if (offset == _acknowledgements[_nextSequence].Offset)
            _nextSequence++;

        _nextOffset++;
    }

    public void EnsureComplete()
    {
        if (!IsComplete)
            throw new InvalidOperationException(
                $"Timed out with {_acknowledgements.Count - _nextSequence} acknowledged record(s) unread.");
    }
}
