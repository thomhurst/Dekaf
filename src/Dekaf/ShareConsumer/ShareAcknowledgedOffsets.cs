namespace Dekaf.ShareConsumer;

/// <summary>
/// Provides allocation-free access to the offsets covered by one acknowledgement result.
/// </summary>
public readonly struct ShareAcknowledgedOffsets
{
    private readonly List<AcknowledgementBatchData>? _batches;

    internal ShareAcknowledgedOffsets(List<AcknowledgementBatchData> batches)
    {
        _batches = batches;

        var length = 0;
        for (var i = 0; i < batches.Count; i++)
            length = checked(length + batches[i].AcknowledgeTypes.Length);

        Length = length;
    }

    /// <summary>
    /// Gets the number of acknowledged offsets.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the acknowledged offset at the specified index.
    /// </summary>
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
                destination[index++] = batch.FirstOffset + offsetIndex;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator over the acknowledged offsets.
    /// </summary>
    public Enumerator GetEnumerator() => new(_batches);

    /// <summary>
    /// Enumerates acknowledged offsets without allocating.
    /// </summary>
    public struct Enumerator
    {
        private readonly List<AcknowledgementBatchData>? _batches;
        private int _batchIndex;
        private int _offsetIndex;

        internal Enumerator(List<AcknowledgementBatchData>? batches)
        {
            _batches = batches;
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
                _offsetIndex++;
                if (_offsetIndex < batch.AcknowledgeTypes.Length)
                {
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
