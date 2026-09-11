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
                count -= CountGaps(types.AsSpan(firstGap));
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

            if (_hasGaps)
                return GetSparseOffset(_batches!, index);

            var batches = _batches!;
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                if (index < batch.AcknowledgeTypes.Length)
                    return batch.FirstOffset + index;

                index -= batch.AcknowledgeTypes.Length;
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
