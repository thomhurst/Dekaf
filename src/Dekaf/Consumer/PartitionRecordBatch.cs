using System.Collections;

namespace Dekaf.Consumer;

// A handler borrows this view, including its records, until its ValueTask completes.
internal sealed class PartitionRecordBatch<TKey, TValue>(int capacity) : IReadOnlyList<ConsumeResult<TKey, TValue>>
{
    private ConsumeResult<TKey, TValue>[] _records = new ConsumeResult<TKey, TValue>[Math.Min(capacity, 16)];

    public int Count { get; private set; }

    public ConsumeResult<TKey, TValue> this[int index] => (uint)index < (uint)Count
        ? _records[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal void Add(ConsumeResult<TKey, TValue> record)
    {
        if (Count == _records.Length)
            Array.Resize(ref _records, (int)Math.Min((long)_records.Length * 2, capacity));
        _records[Count++] = record;
    }

    internal void Clear()
    {
        Array.Clear(_records, 0, Count);
        Count = 0;
    }

    public IEnumerator<ConsumeResult<TKey, TValue>> GetEnumerator() => new Enumerator(_records, Count);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Match the array enumerator's small per-enumeration object. A yield iterator
    // would additionally retain a full ConsumeResult in its Current field.
    private sealed class Enumerator(ConsumeResult<TKey, TValue>[] records, int count)
        : IEnumerator<ConsumeResult<TKey, TValue>>
    {
        private int _index = -1;

        public ConsumeResult<TKey, TValue> Current => (uint)_index < (uint)count
            ? records[_index]
            : throw new InvalidOperationException("Enumeration has not started or has already finished.");

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index < count)
                _index++;
            return _index < count;
        }

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}
