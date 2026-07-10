using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Fuzzing;

public static class RecordBatchFuzzTarget
{
    private const int MaxInputLength = 1024 * 1024;
    internal const byte SegmentedInputMask = 0x80;
    internal const int MaxOutputLength = 4 * 1024 * 1024;
    internal static readonly int OperationCount = Enum.GetValues<RecordBatchFuzzOperation>().Length;

    private static readonly IReadOnlyDictionary<RecordBatchFuzzOperation, ICompressionCodec> Codecs =
        CreateCodecs();
    private static readonly CompressionCodecRegistry BatchCodecs = CreateBatchCodecs();

    public static void Run(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty || input.Length > MaxInputLength)
        {
            return;
        }

        if (GetOperation(input[0]) is not { } operation)
        {
            return;
        }

        try
        {
            Execute(operation, (input[0] & SegmentedInputMask) != 0, input[1..]);
        }
        catch (InsufficientDataException)
        {
            // Truncated protocol input is expected.
        }
        catch (MalformedProtocolDataException)
        {
            // Invalid RecordBatch or record structure is expected.
        }
        catch (NotSupportedException)
        {
            // Unknown magic bytes and unregistered compression attributes are expected.
        }
        catch (InvalidDataException)
        {
            // Corrupt compressed streams are expected.
        }
        catch (EndOfStreamException)
        {
            // Truncated LZ4 frames are expected.
        }
        catch (FuzzOutputLimitExceededException)
        {
            // A decompression bomb reaching the fixed output cap is expected.
        }
    }

    internal static void Execute(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty || GetOperation(input[0]) is not { } operation)
        {
            throw new ArgumentException("Input does not select a RecordBatch fuzz operation.", nameof(input));
        }

        Execute(operation, (input[0] & SegmentedInputMask) != 0, input[1..]);
    }

    internal static RecordBatchFuzzOperation? GetOperation(byte selector)
    {
        var value = selector & ~SegmentedInputMask;
        return value < OperationCount ? (RecordBatchFuzzOperation)value : null;
    }

    internal static byte GetSelector(RecordBatchFuzzOperation operation, bool segmented = false)
    {
        var value = (int)operation;
        if ((uint)value >= (uint)OperationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return segmented ? (byte)(value | SegmentedInputMask) : (byte)value;
    }

    internal static CompressionType? GetCompressionType(RecordBatchFuzzOperation operation) =>
        Codecs.TryGetValue(operation, out var codec) ? codec.Type : null;

    private static void Execute(RecordBatchFuzzOperation operation, bool segmented, ReadOnlySpan<byte> payload)
    {
        if (operation == RecordBatchFuzzOperation.RecordBatch)
        {
            ExecuteRecordBatch(CreateSequence(payload, segmented));
            return;
        }

        if (operation == RecordBatchFuzzOperation.Crc32C)
        {
            ValidateRecordBatchCrc(CreateSequence(payload, segmented));
            return;
        }

        var output = new OutputLimitedBufferWriter(MaxOutputLength);
        Codecs[operation].Decompress(CreateSequence(payload, segmented), output);
    }

    private static void ExecuteRecordBatch(ReadOnlySequence<byte> input)
    {
        var reader = new KafkaProtocolReader(input);
        using var batch = RecordBatch.Read(ref reader, BatchCodecs, checked((int)input.Length));

        foreach (var record in batch.Records)
        {
            _ = record.Key.Length;
            _ = record.Value.Length;
            _ = record.EffectiveHeaderCount;
        }
    }

    private static void ValidateRecordBatchCrc(ReadOnlySequence<byte> input)
    {
        const int batchLengthOffset = 8;
        const int crcOffset = 17;
        const int crcContentOffset = 21;
        const int minimumBatchLength = 49;

        if (input.Length < crcContentOffset)
        {
            throw new InsufficientDataException();
        }

        Span<byte> header = stackalloc byte[crcContentOffset];
        input.Slice(0, header.Length).CopyTo(header);
        var batchLength = BinaryPrimitives.ReadInt32BigEndian(header[batchLengthOffset..]);
        var totalLength = 12L + batchLength;
        if (batchLength < minimumBatchLength || totalLength > input.Length)
        {
            throw new MalformedProtocolDataException($"Invalid RecordBatch length {batchLength}");
        }

        var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(header[crcOffset..]);
        var computedCrc = Crc32C.Compute(input.Slice(crcContentOffset, batchLength - 9));
        if (storedCrc != computedCrc)
        {
            throw new MalformedProtocolDataException(
                $"RecordBatch CRC mismatch: expected 0x{storedCrc:X8}, computed 0x{computedCrc:X8}");
        }
    }

    private static ReadOnlySequence<byte> CreateSequence(ReadOnlySpan<byte> input, bool segmented)
    {
        if (!segmented || input.IsEmpty)
        {
            return new ReadOnlySequence<byte>(input.ToArray());
        }

        return SegmentedFuzzInput.Create(input);
    }

    private static IReadOnlyDictionary<RecordBatchFuzzOperation, ICompressionCodec> CreateCodecs() =>
        new Dictionary<RecordBatchFuzzOperation, ICompressionCodec>
        {
            [RecordBatchFuzzOperation.None] = new NoneCompressionCodec(),
            [RecordBatchFuzzOperation.Gzip] = new GzipCompressionCodec(),
            [RecordBatchFuzzOperation.Snappy] = new SnappyCompressionCodec(),
            [RecordBatchFuzzOperation.Lz4] = new Lz4CompressionCodec(),
            [RecordBatchFuzzOperation.Zstd] = new ZstdCompressionCodec(),
            [RecordBatchFuzzOperation.Brotli] = new BrotliCompressionCodec()
        };

    private static CompressionCodecRegistry CreateBatchCodecs()
    {
        var registry = new CompressionCodecRegistry();
        foreach (var codec in Codecs.Values)
        {
            registry.Register(new OutputLimitedCompressionCodec(codec));
        }

        return registry;
    }

    private sealed class OutputLimitedCompressionCodec(ICompressionCodec inner) : ICompressionCodec
    {
        public CompressionType Type => inner.Type;

        public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination) =>
            inner.Compress(source, destination);

        public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination) =>
            inner.Decompress(source, new OutputLimitedBufferWriter(destination, MaxOutputLength));
    }
}

internal enum RecordBatchFuzzOperation : byte
{
    RecordBatch = 0,
    Crc32C = 1,
    None = 2,
    Gzip = 3,
    Snappy = 4,
    Lz4 = 5,
    Zstd = 6,
    Brotli = 7
}

internal sealed class FuzzOutputLimitExceededException : Exception;

internal sealed class OutputLimitedBufferWriter(IBufferWriter<byte> inner, int limit) : IBufferWriter<byte>
{
    private int _written;

    public OutputLimitedBufferWriter(int limit)
        : this(new ArrayBufferWriter<byte>(), limit)
    {
    }

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        EnsureAvailable(count);
        inner.Advance(count);
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var normalizedSizeHint = NormalizeSizeHint(sizeHint);
        EnsureAvailable(normalizedSizeHint);
        var memory = inner.GetMemory(normalizedSizeHint);
        return memory[..Math.Min(memory.Length, limit - _written)];
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var normalizedSizeHint = NormalizeSizeHint(sizeHint);
        EnsureAvailable(normalizedSizeHint);
        var span = inner.GetSpan(normalizedSizeHint);
        return span[..Math.Min(span.Length, limit - _written)];
    }

    private static int NormalizeSizeHint(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        return sizeHint == 0 ? 1 : sizeHint;
    }

    private void EnsureAvailable(int sizeHint)
    {
        if (sizeHint > limit - _written)
        {
            throw new FuzzOutputLimitExceededException();
        }
    }
}
