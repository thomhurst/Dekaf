using System.Buffers;
using Dekaf.Compression;
using Dekaf.Fuzzing;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

public class RecordBatchFuzzCorpusTests
{
    private static readonly string[] ExpectedCorpusNames =
    [
        "brotli-corrupt",
        "brotli-valid",
        "gzip-corrupt",
        "gzip-valid",
        "lz4-corrupt",
        "lz4-invalid-block-size",
        "lz4-valid",
        "none-corrupt",
        "none-valid",
        "record-batch-corrupt-crc",
        "record-batch-hostile-header-count",
        "record-batch-hostile-max-length",
        "record-batch-hostile-negative-length",
        "record-batch-hostile-record-count",
        "record-batch-truncated-header",
        "record-batch-truncated-record",
        "record-batch-valid",
        "record-batch-valid-crc",
        "snappy-corrupt",
        "snappy-valid",
        "zstd-corrupt",
        "zstd-valid"
    ];

    private static readonly RecordBatchFuzzOperation[] CodecOperations =
    [
        RecordBatchFuzzOperation.None,
        RecordBatchFuzzOperation.Gzip,
        RecordBatchFuzzOperation.Snappy,
        RecordBatchFuzzOperation.Lz4,
        RecordBatchFuzzOperation.Zstd,
        RecordBatchFuzzOperation.Brotli
    ];

    [Test]
    public async Task OperationCount_MatchesOperationEnum()
    {
        await Assert.That(RecordBatchFuzzTarget.OperationCount)
            .IsEqualTo(Enum.GetValues<RecordBatchFuzzOperation>().Length);
    }

    [Test]
    public void OperationDispatch_HandlesEveryEnumValue()
    {
        var input = new byte[64];

        foreach (var operation in Enum.GetValues<RecordBatchFuzzOperation>())
        {
            input[0] = RecordBatchFuzzTarget.GetSelector(operation);
            RecordBatchFuzzTarget.Run(input);

            input[0] = RecordBatchFuzzTarget.GetSelector(operation, segmented: true);
            RecordBatchFuzzTarget.Run(input);
        }
    }

    [Test]
    public async Task CodecOperations_CoverEveryCompressionType()
    {
        var compressionTypes = CodecOperations
            .Select(RecordBatchFuzzTarget.GetCompressionType)
            .OfType<CompressionType>();

        await Assert.That(compressionTypes).IsEquivalentTo(Enum.GetValues<CompressionType>());
    }

    [Test]
    public async Task CheckedInCorpus_ManifestIsExact()
    {
        await Assert.That(RecordBatchFuzzCorpus.LoadEmbedded().Select(seed => seed.Name))
            .IsEquivalentTo(ExpectedCorpusNames);
    }

    [Test]
    public async Task CheckedInCorpus_HasValidAndCorruptSeedForEveryCodec()
    {
        var seeds = RecordBatchFuzzCorpus.LoadEmbedded();

        foreach (var operation in CodecOperations)
        {
            var prefix = $"{operation.ToString().ToLowerInvariant()}-";
            var codecSeeds = seeds.Where(seed => seed.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

            await Assert.That(codecSeeds.Select(seed => seed.Name)).Contains($"{prefix}valid");
            await Assert.That(codecSeeds.Select(seed => seed.Name)).Contains($"{prefix}corrupt");
            foreach (var seed in codecSeeds)
            {
                await Assert.That(RecordBatchFuzzTarget.GetOperation(seed.Data[0]))
                    .IsEqualTo((RecordBatchFuzzOperation?)operation);
            }
        }
    }

    [Test]
    public async Task CheckedInCorpus_CoversBatchCrcAndTruncationPaths()
    {
        var names = RecordBatchFuzzCorpus.LoadEmbedded().Select(seed => seed.Name);

        await Assert.That(names).Contains("record-batch-valid");
        await Assert.That(names).Contains("record-batch-valid-crc");
        await Assert.That(names).Contains("record-batch-corrupt-crc");
        await Assert.That(names).Contains("record-batch-truncated-header");
        await Assert.That(names).Contains("record-batch-truncated-record");
        await Assert.That(names).Contains("record-batch-hostile-negative-length");
        await Assert.That(names).Contains("record-batch-hostile-max-length");
        await Assert.That(names).Contains("record-batch-hostile-record-count");
        await Assert.That(names).Contains("record-batch-hostile-header-count");
    }

    [Test]
    public void CheckedInValidSeeds_ExecuteWithoutExpectedParseFailure()
    {
        foreach (var seed in RecordBatchFuzzCorpus.LoadEmbedded().Where(seed => seed.Name.EndsWith("-valid", StringComparison.Ordinal)))
        {
            RecordBatchFuzzTarget.Execute(seed.Data);
        }
    }

    [Test]
    public void ValidCrcSeed_ExecutesWithoutFailure()
    {
        var seed = RecordBatchFuzzCorpus.LoadEmbedded().Single(seed => seed.Name == "record-batch-valid-crc");

        RecordBatchFuzzTarget.Execute(seed.Data);
    }

    [Test]
    public async Task CorruptCrcSeed_IsRejectedAsMalformedProtocolData()
    {
        var seed = RecordBatchFuzzCorpus.LoadEmbedded().Single(seed => seed.Name == "record-batch-corrupt-crc");

        await Assert.That(() => RecordBatchFuzzTarget.Execute(seed.Data))
            .ThrowsExactly<MalformedProtocolDataException>();
    }

    [Test]
    public async Task CorruptZstdSeed_IsRejectedAsInvalidData()
    {
        var seed = RecordBatchFuzzCorpus.LoadEmbedded().Single(seed => seed.Name == "zstd-corrupt");

        await Assert.That(() => RecordBatchFuzzTarget.Execute(seed.Data))
            .ThrowsExactly<InvalidDataException>();
    }

    [Test]
    public async Task MalformedLz4Seed_IsRejectedAsInvalidData()
    {
        var seed = RecordBatchFuzzCorpus.LoadEmbedded().Single(seed => seed.Name == "lz4-invalid-block-size");

        await Assert.That(() => RecordBatchFuzzTarget.Execute(seed.Data))
            .ThrowsExactly<InvalidDataException>();
    }

    [Test]
    public void CheckedInCorpus_ReplaysWithoutUnexpectedFailures()
    {
        foreach (var seed in RecordBatchFuzzCorpus.LoadEmbedded())
        {
            RecordBatchFuzzTarget.Run(seed.Data);

            var segmentedInput = seed.Data.ToArray();
            segmentedInput[0] |= RecordBatchFuzzTarget.SegmentedInputMask;
            RecordBatchFuzzTarget.Run(segmentedInput);
        }
    }

    [Test]
    public void DeterministicShortCodecInputs_DoNotThrowUnexpectedExceptions()
    {
        var random = new Random(1612);

        foreach (var operation in CodecOperations)
        {
            for (var payloadLength = 0; payloadLength <= 64; payloadLength++)
            {
                var input = new byte[payloadLength + 1];
                input[0] = RecordBatchFuzzTarget.GetSelector(
                    operation,
                    segmented: (payloadLength & 1) != 0);
                random.NextBytes(input.AsSpan(1));

                RecordBatchFuzzTarget.Run(input);
            }
        }
    }

    [Test]
    public async Task GzipExpansionBeyondOutputLimit_IsRejectedWithoutAllocatingUnboundedOutput()
    {
        var source = new byte[RecordBatchFuzzTarget.MaxOutputLength + 1];
        var compressed = new ArrayBufferWriter<byte>();
        new GzipCompressionCodec().Compress(new ReadOnlySequence<byte>(source), compressed);
        var input = new byte[compressed.WrittenCount + 1];
        input[0] = RecordBatchFuzzTarget.GetSelector(RecordBatchFuzzOperation.Gzip);
        compressed.WrittenSpan.CopyTo(input.AsSpan(1));

        await Assert.That(() => RecordBatchFuzzTarget.Execute(input))
            .ThrowsExactly<FuzzOutputLimitExceededException>();
    }

    [Test]
    public async Task CompressedBatchExpansionBeyondOutputLimit_IsRejected()
    {
        using var batch = new RecordBatch
        {
            Records =
            [
                new Record
                {
                    Value = new byte[RecordBatchFuzzTarget.MaxOutputLength + 1]
                }
            ]
        };
        var encodedBatch = new ArrayBufferWriter<byte>();
        batch.Write(encodedBatch, CompressionType.Gzip);
        var input = new byte[encodedBatch.WrittenCount + 1];
        input[0] = RecordBatchFuzzTarget.GetSelector(RecordBatchFuzzOperation.RecordBatch);
        encodedBatch.WrittenSpan.CopyTo(input.AsSpan(1));

        await Assert.That(() => RecordBatchFuzzTarget.Execute(input))
            .ThrowsExactly<FuzzOutputLimitExceededException>();
    }

    [Test]
    public async Task SequenceCrc_MatchesContiguousCrcAtEverySplit()
    {
        var data = Enumerable.Range(0, 257).Select(value => (byte)value).ToArray();
        var expected = Crc32C.Compute(data);

        for (var split = 0; split <= data.Length; split++)
        {
            var first = new CrcSegment(data.AsMemory(0, split));
            var last = first.Append(data.AsMemory(split));
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            await Assert.That(Crc32C.Compute(sequence)).IsEqualTo(expected);
        }
    }

    private sealed class CrcSegment : ReadOnlySequenceSegment<byte>
    {
        public CrcSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public CrcSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new CrcSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
