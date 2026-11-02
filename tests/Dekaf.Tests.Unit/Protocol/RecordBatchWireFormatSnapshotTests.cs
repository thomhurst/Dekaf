using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using VerifyTests;
using VerifyTUnit;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class RecordBatchWireFormatSnapshotTests
{
    [Test]
    public async Task Record_batches_match_golden_wire_format()
    {
        var registry = new CompressionCodecRegistry()
            .AddBrotli()
            .AddLz4()
            .AddSnappy()
            .AddZstd();
        var snapshots = new List<RecordBatchWireSnapshot>();

        foreach (var compression in Enum.GetValues<CompressionType>())
        {
            foreach (var includeHeaders in new[] { false, true })
            {
                using var batch = CreateRecordBatch(RecordBatchAttributes.None, includeHeaders);
                snapshots.Add(EncodeRecordBatch($"{compression}-headers-{includeHeaders}", batch, compression, registry));
            }
        }

        using (var transactional = CreateRecordBatch(RecordBatchAttributes.IsTransactional, includeHeaders: true))
        {
            snapshots.Add(EncodeRecordBatch("transactional", transactional, CompressionType.None, registry));
        }

        using (var transactionalLz4 = CreateRecordBatch(RecordBatchAttributes.IsTransactional, includeHeaders: true))
        {
            snapshots.Add(EncodeRecordBatch("transactional-lz4", transactionalLz4, CompressionType.Lz4, registry));
        }

        // Kafka control batches contain one uncompressed control record.
        using (var control = CreateControlBatch())
        {
            snapshots.Add(EncodeRecordBatch("control-commit-marker", control, CompressionType.None, registry));
        }

        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        await Verifier.Verify(snapshots, settings);
    }

    private static RecordBatch CreateRecordBatch(RecordBatchAttributes attributes, bool includeHeaders)
    {
        var headers = includeHeaders
            ? new[]
            {
                new Header("trace-id", "abc123"u8.ToArray()),
                new Header("empty", []),
                new Header("null", value: null)
            }
            : null;

        return new RecordBatch
        {
            BaseOffset = 42,
            PartitionLeaderEpoch = 3,
            Attributes = attributes,
            LastOffsetDelta = 1,
            BaseTimestamp = 1_700_000_000_000,
            MaxTimestamp = 1_700_000_000_001,
            ProducerId = 99,
            ProducerEpoch = 2,
            BaseSequence = 7,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    IsKeyNull = true,
                    Value = "golden-value"u8.ToArray(),
                    Headers = headers,
                    HeaderCount = headers?.Length ?? 0
                },
                new Record
                {
                    OffsetDelta = 1,
                    TimestampDelta = 1,
                    Key = "golden-key"u8.ToArray(),
                    IsValueNull = true
                }
            ]
        };
    }

    private static RecordBatch CreateControlBatch() => new()
    {
        BaseOffset = 43,
        PartitionLeaderEpoch = 3,
        Attributes = RecordBatchAttributes.IsTransactional | RecordBatchAttributes.IsControlBatch,
        BaseTimestamp = 1_700_000_000_002,
        MaxTimestamp = 1_700_000_000_002,
        ProducerId = 99,
        ProducerEpoch = 2,
        BaseSequence = 9,
        Records =
        [
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = new byte[] { 0, 0, 0, 1 },
                Value = new byte[] { 0, 0, 0, 0, 0, 7 }
            }
        ]
    };

    private static RecordBatchWireSnapshot EncodeRecordBatch(
        string scenario,
        RecordBatch batch,
        CompressionType compression,
        CompressionCodecRegistry registry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer, compression, registry);
        var bytes = buffer.WrittenSpan.ToArray();

        // The writer must produce a valid CRC before any normalization.
        var crc = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(CrcOffset));
        if (crc != Crc32C.Compute(bytes.AsSpan(CrcCoveredOffset)))
        {
            throw new InvalidOperationException($"Scenario {scenario} wrote an invalid batch CRC.");
        }

        if (compression == CompressionType.Gzip)
        {
            NormalizeGzipOperatingSystem(bytes);
        }

        return new RecordBatchWireSnapshot(scenario, Convert.ToHexString(bytes));
    }

    // The runtime's gzip writer stamps the host OS into the header (RFC 1952 OS field):
    // 3 on Linux, 10 on Windows, 19 on macOS. Pin it to 255 ("unknown") and recompute the
    // batch CRC that covers it, so one snapshot holds on every platform.
    private static void NormalizeGzipOperatingSystem(byte[] bytes)
    {
        var gzip = bytes.AsSpan(RecordsOffset);
        if (gzip.Length < GzipHeaderLength || gzip[0] != 0x1F || gzip[1] != 0x8B)
        {
            throw new InvalidOperationException("Gzip batch records do not start with a gzip header.");
        }

        gzip[GzipOperatingSystemOffset] = GzipUnknownOperatingSystem;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(CrcOffset), Crc32C.Compute(bytes.AsSpan(CrcCoveredOffset)));
    }

    // RecordBatch v2 layout: baseOffset(8) batchLength(4) partitionLeaderEpoch(4) magic(1) crc(4),
    // then the CRC-covered attributes(2) ... recordCount(4), then the records.
    private const int CrcOffset = 17;
    private const int CrcCoveredOffset = 21;
    private const int RecordsOffset = 61;
    private const int GzipHeaderLength = 10;
    private const int GzipOperatingSystemOffset = 9;
    private const byte GzipUnknownOperatingSystem = 0xFF;

    private sealed record RecordBatchWireSnapshot(string Scenario, string Bytes);
}
