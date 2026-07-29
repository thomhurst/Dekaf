using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace Dekaf.StressTests.Scenarios;

internal readonly record struct RoundTripMessage(
    string Key,
    byte[] Value,
    int ExpectedPartition,
    long Sequence)
{
    /// <summary>
    /// Copies <see cref="Value"/> out of the factory's reused per-partition buffer.
    /// Required before retaining a message past the next
    /// <see cref="RoundTripMessageFactory.Create"/> call for the same partition.
    /// </summary>
    public RoundTripMessage Retain() => this with { Value = Value.ToArray() };
}

internal readonly record struct RoundTripMessageMetadata(
    int Partition,
    long Sequence,
    long Ordinal);

internal sealed class RoundTripMessageFactory
{
    private readonly int _partitionCount;
    private readonly string[] _partitionKeys;
    private readonly uint[] _partitionKeyChecksums;
    private readonly long[] _nextSequences;
    private readonly long[] _expectedPerPartition;
    // Reusable per-partition payload buffers and precomputed key checksums keep Create
    // allocation-free and cheap per message, so the lane's GC/CPU tables measure the
    // client rather than the harness (#2472: a fresh byte[128] here was 152 B/msg of
    // the reported 306 B/msg).
    private readonly byte[][] _payloadBuffers;

    public RoundTripMessageFactory(string runId, int partitionCount, int payloadSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(payloadSize, RoundTripMessageCodec.MinimumPayloadSize);

        _partitionCount = partitionCount;
        _partitionKeys = Enumerable.Range(0, partitionCount)
            .Select(partition => RoundTripMessageCodec.FindKeyForPartition(runId, partition, partitionCount))
            .ToArray();
        _partitionKeyChecksums = new uint[partitionCount];
        _payloadBuffers = new byte[partitionCount][];
        for (var partition = 0; partition < partitionCount; partition++)
        {
            _partitionKeyChecksums[partition] = RoundTripMessageCodec.ComputeUtf8Checksum(_partitionKeys[partition]);
            _payloadBuffers[partition] = new byte[payloadSize];
        }
        _nextSequences = new long[partitionCount];
        _expectedPerPartition = new long[partitionCount];
    }

    public IReadOnlyList<long> ExpectedPerPartition => _expectedPerPartition;

    /// <summary>
    /// Encodes the next sequenced message for <paramref name="partition"/> into that
    /// partition's reused payload buffer. The returned <see cref="RoundTripMessage.Value"/>
    /// is only valid until the next <see cref="Create"/> call for the same partition;
    /// callers that retain it longer must use <see cref="RoundTripMessage.Retain"/>.
    /// The produce loops rely on <c>FireAsync</c>/<c>Produce</c> copying key and value
    /// before their awaited call completes, so awaiting each send before the next
    /// Create makes the reuse safe.
    /// </summary>
    public RoundTripMessage Create(int partition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partition);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(partition, _partitionCount);

        var key = _partitionKeys[partition];
        var sequence = _nextSequences[partition]++;
        var ordinal = checked((sequence * _partitionCount) + partition);
        _expectedPerPartition[partition]++;

        var payload = _payloadBuffers[partition];
        RoundTripMessageCodec.EncodeInto(payload, _partitionKeyChecksums[partition], partition, sequence, ordinal);
        return new RoundTripMessage(key, payload, partition, sequence);
    }
}

internal static class RoundTripMessageCodec
{
    private const uint Magic = 0x54524b44; // "DKRT" in little-endian byte order
    private const byte Version = 1;
    private const int PartitionOffset = 5;
    private const int SequenceOffset = 9;
    private const int OrdinalOffset = 17;
    private const int KeyChecksumOffset = 25;
    private const int ChecksumSize = sizeof(uint);
    private const uint Murmur2Seed = 0x9747b28c;
    private const uint Murmur2Multiplier = 0x5bd1e995;
    private const int Murmur2Shift = 24;

    internal const int HeaderSize = 29;
    internal const int MinimumPayloadSize = HeaderSize + ChecksumSize;

    public static string FindKeyForPartition(string runId, int partition, int partitionCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentOutOfRangeException.ThrowIfNegative(partition);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(partition, partitionCount);

        for (var candidate = 0L; ; candidate++)
        {
            var key = $"roundtrip-{runId}-p{partition:D4}-{candidate:D8}";
            if (GetExpectedPartition(key, partitionCount) == partition)
            {
                return key;
            }
        }
    }

    public static int GetExpectedPartition(string key, int partitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);

        var byteCount = Encoding.UTF8.GetByteCount(key);
        byte[]? rented = null;
        Span<byte> keyBytes = byteCount <= 256
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(key, keyBytes);
            return GetMurmur2Partition(keyBytes[..written], partitionCount);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int GetMurmur2Partition(ReadOnlySpan<byte> key, int partitionCount)
    {
        var remaining = key.Length;
        var hash = Murmur2Seed ^ (uint)remaining;
        var offset = 0;

        while (remaining >= sizeof(uint))
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(key[offset..]);
            value *= Murmur2Multiplier;
            value ^= value >> Murmur2Shift;
            value *= Murmur2Multiplier;

            hash *= Murmur2Multiplier;
            hash ^= value;
            offset += sizeof(uint);
            remaining -= sizeof(uint);
        }

        switch (remaining)
        {
            case 3:
                hash ^= (uint)key[offset + 2] << 16;
                goto case 2;
            case 2:
                hash ^= (uint)key[offset + 1] << 8;
                goto case 1;
            case 1:
                hash ^= key[offset];
                hash *= Murmur2Multiplier;
                break;
        }

        hash ^= hash >> 13;
        hash *= Murmur2Multiplier;
        hash ^= hash >> 15;

        return (int)((hash & 0x7fff_ffff) % (uint)partitionCount);
    }

    /// <summary>
    /// Encodes a sequenced message into <paramref name="payload"/> without allocating.
    /// Every byte of the destination is overwritten.
    /// </summary>
    public static void EncodeInto(
        Span<byte> payload,
        string key,
        int partition,
        long sequence,
        long ordinal) =>
        EncodeInto(payload, ComputeUtf8Checksum(key), partition, sequence, ordinal);

    /// <summary>
    /// Core encode with a precomputed key checksum, so per-partition callers skip the
    /// per-message UTF-8 transcode + CRC of a fixed key.
    /// </summary>
    public static void EncodeInto(
        Span<byte> payload,
        uint keyChecksum,
        int partition,
        long sequence,
        long ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partition);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfLessThan(payload.Length, MinimumPayloadSize, nameof(payload));

        BinaryPrimitives.WriteUInt32LittleEndian(payload, Magic);
        payload[sizeof(uint)] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(payload[PartitionOffset..], partition);
        BinaryPrimitives.WriteInt64LittleEndian(payload[SequenceOffset..], sequence);
        BinaryPrimitives.WriteInt64LittleEndian(payload[OrdinalOffset..], ordinal);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[KeyChecksumOffset..], keyChecksum);

        var checksumOffset = payload.Length - ChecksumSize;
        // Filler byte at index i is (ordinal*31 + partition*13 + i*17) mod 256; the
        // running accumulator is the strength-reduced form of that same expression.
        var filler = unchecked((byte)((ordinal * 31) + (partition * 13) + (HeaderSize * 17)));
        for (var index = HeaderSize; index < checksumOffset; index++)
        {
            payload[index] = filler;
            filler = unchecked((byte)(filler + 17));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(
            payload[checksumOffset..],
            Crc32.HashToUInt32(payload[..checksumOffset]));
    }

    public static bool TryDecode(
        string? key,
        byte[]? payload,
        out RoundTripMessageMetadata metadata)
    {
        metadata = default;
        if (key is null || payload is null || payload.Length < MinimumPayloadSize)
        {
            return false;
        }

        var span = payload.AsSpan();
        if (BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic || span[sizeof(uint)] != Version)
        {
            return false;
        }

        var checksumOffset = span.Length - ChecksumSize;
        var expectedPayloadChecksum = BinaryPrimitives.ReadUInt32LittleEndian(span[checksumOffset..]);
        if (Crc32.HashToUInt32(span[..checksumOffset]) != expectedPayloadChecksum)
        {
            return false;
        }

        var expectedKeyChecksum = BinaryPrimitives.ReadUInt32LittleEndian(span[KeyChecksumOffset..]);
        if (ComputeUtf8Checksum(key) != expectedKeyChecksum)
        {
            return false;
        }

        metadata = new RoundTripMessageMetadata(
            BinaryPrimitives.ReadInt32LittleEndian(span[PartitionOffset..]),
            BinaryPrimitives.ReadInt64LittleEndian(span[SequenceOffset..]),
            BinaryPrimitives.ReadInt64LittleEndian(span[OrdinalOffset..]));
        return true;
    }

    internal static uint ComputeUtf8Checksum(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        byte[]? rented = null;
        Span<byte> bytes = byteCount <= 256
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(value, bytes);
            return Crc32.HashToUInt32(bytes[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}

internal sealed class RoundTripValidator
{
    private readonly long[] _expectedPerPartition;
    // Bit per expected sequence: an eager bool[] costs 1 B per expected message inside
    // the lane's measured GC window (~1 B/msg of the #2472 attribution); the bitmap
    // amortizes to ~0.125 B/msg with the same O(1) duplicate check.
    private readonly ulong[][] _seen;
    private readonly long[] _lastSequence;
    private readonly long[] _lastOffset;
    private long _consumedMessages;
    private long _uniqueMessages;
    private long _duplicateMessages;
    private long _corruptMessages;
    private long _outOfOrderMessages;
    private long _mispartitionedMessages;
    private long _unexpectedMessages;

    public RoundTripValidator(IReadOnlyList<long> expectedPerPartition)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedPerPartition.Count, 1);

        _expectedPerPartition = [.. expectedPerPartition];
        _seen = new ulong[_expectedPerPartition.Length][];
        _lastSequence = new long[_expectedPerPartition.Length];
        _lastOffset = new long[_expectedPerPartition.Length];
        Array.Fill(_lastSequence, -1);
        Array.Fill(_lastOffset, -1);

        for (var partition = 0; partition < _expectedPerPartition.Length; partition++)
        {
            var expected = checked((int)_expectedPerPartition[partition]);
            _seen[partition] = new ulong[(expected + 63) >> 6];
        }
    }

    public void Record(string? key, byte[]? payload, int actualPartition, long actualOffset)
    {
        _consumedMessages++;

        if (!RoundTripMessageCodec.TryDecode(key, payload, out var message))
        {
            _corruptMessages++;
            return;
        }

        if (message.Partition < 0 || message.Partition >= _expectedPerPartition.Length ||
            message.Sequence < 0 || message.Sequence >= _expectedPerPartition[message.Partition])
        {
            _unexpectedMessages++;
            return;
        }

        var expectedOrdinal = checked((message.Sequence * _expectedPerPartition.Length) + message.Partition);
        if (message.Ordinal != expectedOrdinal)
        {
            _corruptMessages++;
            return;
        }

        var expectedPartition = RoundTripMessageCodec.GetExpectedPartition(key!, _expectedPerPartition.Length);
        var actualPartitionInRange = actualPartition >= 0 && actualPartition < _expectedPerPartition.Length;
        if (message.Partition != expectedPartition || actualPartition != expectedPartition)
        {
            _mispartitionedMessages++;
        }

        var sequence = checked((int)message.Sequence);
        ref var seenWord = ref _seen[message.Partition][sequence >> 6];
        var seenBit = 1UL << (sequence & 63);
        if ((seenWord & seenBit) != 0)
        {
            _duplicateMessages++;
        }
        else
        {
            seenWord |= seenBit;
            _uniqueMessages++;
        }

        if (actualPartitionInRange && actualPartition == message.Partition)
        {
            if (message.Sequence < _lastSequence[actualPartition] || actualOffset <= _lastOffset[actualPartition])
            {
                _outOfOrderMessages++;
            }

            _lastSequence[actualPartition] = Math.Max(_lastSequence[actualPartition], message.Sequence);
            _lastOffset[actualPartition] = Math.Max(_lastOffset[actualPartition], actualOffset);
        }
    }

    public void RecordUnexpected()
    {
        _consumedMessages++;
        _unexpectedMessages++;
    }

    public RoundTripValidationSnapshot CreateSnapshot(bool timedOut)
    {
        var expectedMessages = _expectedPerPartition.Sum();
        return new RoundTripValidationSnapshot
        {
            ExpectedMessages = expectedMessages,
            ConsumedMessages = _consumedMessages,
            MissingMessages = Math.Max(0, expectedMessages - _uniqueMessages),
            DuplicateMessages = _duplicateMessages,
            CorruptMessages = _corruptMessages,
            OutOfOrderMessages = _outOfOrderMessages,
            MispartitionedMessages = _mispartitionedMessages,
            UnexpectedMessages = _unexpectedMessages,
            TimedOut = timedOut
        };
    }
}

internal sealed class RoundTripCompletionTracker
{
    private readonly long[] _startOffsets;
    private readonly long[] _endOffsets;
    private readonly bool[] _completed;

    public RoundTripCompletionTracker(long[] startOffsets, long[] endOffsets)
    {
        ArgumentNullException.ThrowIfNull(startOffsets);
        ArgumentNullException.ThrowIfNull(endOffsets);

        if (startOffsets.Length == 0 || startOffsets.Length != endOffsets.Length)
        {
            throw new ArgumentException("Start and end offset arrays must have the same non-zero length.");
        }

        _startOffsets = [.. startOffsets];
        _endOffsets = [.. endOffsets];
        _completed = new bool[startOffsets.Length];

        for (var partition = 0; partition < startOffsets.Length; partition++)
        {
            if (startOffsets[partition] < 0 || endOffsets[partition] < startOffsets[partition])
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endOffsets),
                    "Every offset range must be non-negative and end at or after its start.");
            }

            _completed[partition] = startOffsets[partition] >= endOffsets[partition];
            if (!_completed[partition])
            {
                RemainingPartitions++;
            }
        }
    }

    public int RemainingPartitions { get; private set; }

    public bool IsComplete => RemainingPartitions == 0;

    public bool IsTrackedPartition(int partition) =>
        (uint)partition < (uint)_completed.Length;

    public bool Record(int partition, long offset)
    {
        if (!IsTrackedPartition(partition))
        {
            return false;
        }

        var inRange = offset >= _startOffsets[partition] && offset < _endOffsets[partition];
        if (!_completed[partition] && inRange && offset >= _endOffsets[partition] - 1)
        {
            _completed[partition] = true;
            RemainingPartitions--;
        }

        return inRange;
    }
}

internal sealed class RoundTripValidationSnapshot
{
    public required long ExpectedMessages { get; init; }
    public required long ConsumedMessages { get; init; }
    public required long MissingMessages { get; init; }
    public required long DuplicateMessages { get; init; }
    public required long CorruptMessages { get; init; }
    public required long OutOfOrderMessages { get; init; }
    public required long MispartitionedMessages { get; init; }
    public required long UnexpectedMessages { get; init; }
    public required bool TimedOut { get; init; }

    public bool IsSuccess =>
        !TimedOut &&
        MissingMessages == 0 &&
        DuplicateMessages == 0 &&
        CorruptMessages == 0 &&
        OutOfOrderMessages == 0 &&
        MispartitionedMessages == 0 &&
        UnexpectedMessages == 0;
}
