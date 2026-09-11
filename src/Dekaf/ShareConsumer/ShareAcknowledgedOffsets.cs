namespace Dekaf.ShareConsumer;

/// <summary>
/// Provides allocation-free access to the offsets covered by one acknowledgement result.
/// </summary>
public readonly struct ShareAcknowledgedOffsets
{
    private readonly List<AcknowledgementBatchData>? _batches;
    private readonly bool _hasGaps;

    internal ShareAcknowledgedOffsets(List<AcknowledgementBatchData> batches)
    {
        _batches = batches;

        var length = 0;
        var hasGaps = false;
        for (var i = 0; i < batches.Count; i++)
        {
            var types = batches[i].AcknowledgeTypes;
            var count = types.Length;
            var firstGap = types.AsSpan().IndexOf((byte)AcknowledgeType.Gap);
            if (firstGap >= 0)
            {
                hasGaps = true;
                for (var offset = firstGap; offset < types.Length; offset++)
                    if (types[offset] == (byte)AcknowledgeType.Gap)
                        count--;
            }
            length = checked(length + count);
        }

        _hasGaps = hasGaps;
        Length = length;
    }

    /// <summary>
    /// Gets the number of acknowledged offsets.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the acknowledged offset at the specified index.
    /// </summary>
    /// <remarks>
    /// Each lookup scans acknowledgement batches from the beginning. With gaps, it also scans
    /// their offset entries, taking O(n) time per lookup and O(n²) for a full indexed traversal.
    /// Use <see cref="GetEnumerator"/> or <see cref="CopyTo"/> to traverse the entries once.
    /// </remarks>
    public long this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);

            var batches = _batches!;
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                if (_hasGaps)
                {
                    for (var offset = 0; offset < batch.AcknowledgeTypes.Length; offset++)
                    {
                        if (batch.AcknowledgeTypes[offset] == (byte)AcknowledgeType.Gap)
                            continue;
                        if (index-- == 0)
                            return batch.FirstOffset + offset;
                    }
                    continue;
                }
                if (index < batch.AcknowledgeTypes.Length)
                    return batch.FirstOffset + index;

                index -= batch.AcknowledgeTypes.Length;
            }

            throw new InvalidOperationException("Offset index was not present in the acknowledgement batches.");
        }
    }

    /// <summary>
    /// Copies the acknowledged offsets to <paramref name="destination"/>.
    /// </summary>
    public void CopyTo(Span<long> destination)
    {
        if (destination.Length < Length)
            throw new ArgumentException("Destination is shorter than the offset collection.", nameof(destination));

        var index = 0;
        var batches = _batches;
        if (batches is null)
            return;

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            for (var offsetIndex = 0; offsetIndex < batch.AcknowledgeTypes.Length; offsetIndex++)
                if (!_hasGaps || batch.AcknowledgeTypes[offsetIndex] != (byte)AcknowledgeType.Gap)
                    destination[index++] = batch.FirstOffset + offsetIndex;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator over the acknowledged offsets.
    /// </summary>
    public Enumerator GetEnumerator() => new(_batches, _hasGaps);

    /// <summary>
    /// Enumerates acknowledged offsets without allocating.
    /// </summary>
    public struct Enumerator
    {
        private readonly List<AcknowledgementBatchData>? _batches;
        private readonly bool _hasGaps;
        private int _batchIndex;
        private int _offsetIndex;

        internal Enumerator(List<AcknowledgementBatchData>? batches, bool hasGaps)
        {
            _batches = batches;
            _hasGaps = hasGaps;
            _batchIndex = 0;
            _offsetIndex = -1;
            Current = default;
        }

        /// <summary>
        /// Gets the current acknowledged offset.
        /// </summary>
        public long Current { get; private set; }

        /// <summary>
        /// Advances to the next acknowledged offset.
        /// </summary>
        public bool MoveNext()
        {
            var batches = _batches;
            while (batches is not null && _batchIndex < batches.Count)
            {
                var batch = batches[_batchIndex];
                while (++_offsetIndex < batch.AcknowledgeTypes.Length)
                {
                    if (_hasGaps && batch.AcknowledgeTypes[_offsetIndex] == (byte)AcknowledgeType.Gap)
                        continue;
                    Current = batch.FirstOffset + _offsetIndex;
                    return true;
                }

                _batchIndex++;
                _offsetIndex = -1;
            }

            return false;
        }
    }
}
