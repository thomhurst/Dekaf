#if NETSTANDARD2_0
namespace System.Buffers
{
    internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
    {
        private const int DefaultInitialBufferSize = 256;
        private const int ArrayMaxLength = 0x7FFFFFC7;

        private T[] _buffer;
        private int _index;

        public ArrayBufferWriter()
            : this(DefaultInitialBufferSize)
        {
        }

        public ArrayBufferWriter(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _buffer = initialCapacity == 0 ? Array.Empty<T>() : new T[initialCapacity];
        }

        public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);
        public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);
        public int WrittenCount => _index;
        public int Capacity => _buffer.Length;
        public int FreeCapacity => _buffer.Length - _index;

        public void Advance(int count)
        {
            if ((uint)count > (uint)FreeCapacity)
                throw new ArgumentException("Cannot advance past the end of the buffer.", nameof(count));

            _index += count;
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsMemory(_index);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsSpan(_index);
        }

        public void Clear()
        {
            _buffer.AsSpan(0, _index).Clear();
            _index = 0;
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                sizeHint = 1;

            if (sizeHint <= FreeCapacity)
                return;

            var currentLength = _buffer.Length;
            var growBy = Math.Max(sizeHint, currentLength);
            var newSize = currentLength == 0 ? growBy : currentLength + growBy;
            if ((uint)newSize > ArrayMaxLength)
            {
                newSize = Math.Max(currentLength + sizeHint, ArrayMaxLength);
            }

            if (newSize < currentLength + sizeHint)
                throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

            Array.Resize(ref _buffer, newSize);
        }
    }

    internal ref struct SequenceReader<T>
    {
        private readonly ReadOnlySequence<T> _sequence;
        private SequencePosition _position;
        private long _consumed;

        public SequenceReader(ReadOnlySequence<T> sequence)
        {
            _sequence = sequence;
            _position = sequence.Start;
            _consumed = 0;
        }

        public readonly long Consumed => _consumed;
        public readonly long Remaining => _sequence.Length - _consumed;
        public readonly bool End => Remaining == 0;
        public readonly ReadOnlySequence<T> UnreadSequence => _sequence.Slice(_position);
        public readonly ReadOnlySpan<T> UnreadSpan => UnreadSequence.First.Span;

        public bool TryRead(out T value)
        {
            if (End)
            {
                value = default!;
                return false;
            }

            var span = UnreadSpan;
            if (span.Length == 0)
            {
                value = default!;
                return false;
            }

            value = span[0];
            Advance(1);
            return true;
        }

        public readonly bool TryCopyTo(Span<T> destination)
        {
            if (Remaining < destination.Length)
                return false;

            UnreadSequence.Slice(0, destination.Length).CopyTo(destination);
            return true;
        }

        public void Advance(long count)
        {
            if ((ulong)count > (ulong)Remaining)
                throw new ArgumentOutOfRangeException(nameof(count));

            _position = _sequence.GetPosition(count, _position);
            _consumed += count;
        }
    }
}
#endif
