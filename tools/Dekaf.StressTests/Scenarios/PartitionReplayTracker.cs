namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Tracks per-partition consumption against fixed end offsets so consumer scenarios can
/// replay a pre-seeded topic in a loop instead of racing a live feeder. This bookkeeping
/// IS the comparison methodology: every client scenario must share it, or the harness
/// itself skews the Dekaf-vs-Confluent numbers.
/// </summary>
internal sealed class PartitionReplayTracker
{
    private readonly long[] _endOffsets;
    private readonly bool[] _partitionsAtEnd;
    private int _partitionsAtEndCount;

    public PartitionReplayTracker(long[] endOffsets)
    {
        _endOffsets = endOffsets;
        _partitionsAtEnd = new bool[endOffsets.Length];
        Reset();
    }

    /// <summary>
    /// Records a consumed offset. Returns true when every partition has reached its end
    /// offset — the caller should seek back to the beginning; the tracker self-resets.
    /// </summary>
    public bool RecordConsumed(int partition, long offset)
    {
        if (_partitionsAtEnd[partition] || offset + 1 < _endOffsets[partition])
        {
            return false;
        }

        _partitionsAtEnd[partition] = true;
        if (++_partitionsAtEndCount < _partitionsAtEnd.Length)
        {
            return false;
        }

        Reset();
        return true;
    }

    private void Reset()
    {
        Array.Clear(_partitionsAtEnd);
        _partitionsAtEndCount = 0;

        // Partitions the seeder never wrote to would otherwise block the rewind forever.
        for (var p = 0; p < _endOffsets.Length; p++)
        {
            if (_endOffsets[p] == 0)
            {
                _partitionsAtEnd[p] = true;
                _partitionsAtEndCount++;
            }
        }
    }
}
