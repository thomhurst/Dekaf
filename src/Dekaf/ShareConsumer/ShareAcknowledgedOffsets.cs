namespace Dekaf.ShareConsumer;

/// <summary>
/// Provides allocation-free access to the offsets covered by one acknowledgement result.
/// </summary>
public readonly struct ShareAcknowledgedOffsets
{
    private readonly List<AcknowledgementBatchData>? _batches;
    private readonly bool _hasGaps;
    private readonly bool _requiresOffsetScan;

    internal ShareAcknowledgedOffsets(List<AcknowledgementBatchData> batches)
    {
        _batches = batches;

        var length = 0;
        var hasGaps = false;
        var hasCompactRanges = false;
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var types = batch.AcknowledgeTypes;
            var count = batch.OffsetCount;
            hasCompactRanges |= count != types.Length;
            var firstGap = types.AsSpan().IndexOf((byte)AcknowledgeType.Gap);
            if (firstGap >= 0)
            {
                hasGaps = true;
                count -= types.Length == 1 ? count : CountGaps(types.AsSpan(firstGap));
            }
            length = checked(length + count);
        }

        _hasGaps = hasGaps;
        _requiresOffsetScan = hasGaps || hasCompactRanges;
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
    /// Sparse lookups count blocks of entries to skip acknowledged offsets without building an index.
    /// Each lookup still takes O(n) time in the worst case, and a full indexed traversal takes O(n²).
    /// Enumeration and <see cref="CopyTo"/> take O(n) time. Copies retain the same immutable batch data;
    /// indexing adds no storage, cursor state or pool lifetime requirements.
    /// </remarks>
    public long this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);

            if (_requiresOffsetScan)
                return GetSparseOffset(_batches!, index);

            var batches = _batches!;
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                var count = batch.AcknowledgeTypes.Length;
                if (index < count)
                    return batch.FirstOffset + index;

                index -= count;
            }

            throw new InvalidOperationException("Offset index was not present in the acknowledgement batches.");
        }
    }

    // Pass fields so the JIT can keep the view in registers when inlining the indexer.
    private static long GetSparseOffset(List<AcknowledgementBatchData> batches, int index)
    {
        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            var types = batch.AcknowledgeTypes;
            if (types.Length == 1)
            {
                if (types[0] == (byte)AcknowledgeType.Gap)
                    continue;
                if (index < batch.OffsetCount)
                    return batch.FirstOffset + index;
                index -= batch.OffsetCount;
                continue;
            }
            var offset = 0;
            // Keep short logical suffixes scalar so counting setup stays amortized over a prefix.
            while (index >= 16 && offset < types.Length)
            {
                // At most index + 1 entries cannot pass the requested logical offset.
                // A match inside this prefix therefore means every entry was acknowledged.
                var prefixLength = Math.Min(index + 1, types.Length - offset);
                var acknowledged = prefixLength - CountGaps(types.AsSpan(offset, prefixLength));
                if (index < acknowledged)
                    return batch.FirstOffset + offset + index;
                index -= acknowledged;
                offset += prefixLength;
            }

            for (; offset < types.Length; offset++)
            {
                if (types[offset] == (byte)AcknowledgeType.Gap)
                    continue;
                if (index-- == 0)
                    return batch.FirstOffset + offset;
            }
        }

        throw new InvalidOperationException("Offset index was not present in the acknowledgement batches.");
    }

    private static int CountGaps(ReadOnlySpan<byte> types)
    {
#if NET8_0_OR_GREATER
        return types.Count((byte)AcknowledgeType.Gap);
#else
        var count = 0;
        for (var index = 0; index < types.Length; index++)
            count += types[index] == (byte)AcknowledgeType.Gap ? 1 : 0;
        return count;
#endif
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
            var types = batch.AcknowledgeTypes;
            if (types.Length == 1)
            {
                if (types[0] != (byte)AcknowledgeType.Gap)
                {
                    var count = batch.OffsetCount;
                    for (var offsetIndex = 0; offsetIndex < count; offsetIndex++)
                        destination[index++] = batch.FirstOffset + offsetIndex;
                }
                continue;
            }
            for (var offsetIndex = 0; offsetIndex < types.Length; offsetIndex++)
                if (!_hasGaps || types[offsetIndex] != (byte)AcknowledgeType.Gap)
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
        private int _offsetCount;
        private long _firstOffset;
        private byte[]? _types;

        internal Enumerator(List<AcknowledgementBatchData>? batches, bool hasGaps)
        {
            _batches = batches;
            _hasGaps = hasGaps;
            _batchIndex = 0;
            _offsetIndex = -1;
            _offsetCount = 0;
            _firstOffset = 0;
            _types = null;
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
            while (true)
            {
                while (++_offsetIndex < _offsetCount)
                {
                    if (_types is not null && _types[_offsetIndex] == (byte)AcknowledgeType.Gap)
                        continue;
                    Current = _firstOffset + _offsetIndex;
                    return true;
                }

                if (!MoveToNextBatch())
                    return false;
            }
        }

        // Resolve compact ranges and gap-free batches only at the batch boundary.
        private bool MoveToNextBatch()
        {
            var batches = _batches;
            if (batches is null || _batchIndex >= batches.Count)
            {
                _offsetCount = 0;
                _offsetIndex = -1;
                return false;
            }

            var batch = batches[_batchIndex++];
            var types = batch.AcknowledgeTypes;
            _offsetCount = types.Length == 1 && types[0] == (byte)AcknowledgeType.Gap ? 0 : batch.OffsetCount;
            _firstOffset = batch.FirstOffset;
            _types = _hasGaps && types.Length != 1 ? types : null;
            _offsetIndex = -1;
            return true;
        }
    }
}
