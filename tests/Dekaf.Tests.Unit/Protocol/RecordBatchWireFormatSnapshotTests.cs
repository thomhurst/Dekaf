using System.Buffers;
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

        using (var control = CreateControlBatch())
        {
            snapshots.Add(EncodeRecordBatch("control-commit-marker", control, CompressionType.None, registry));
        }

        var platform = OperatingSystem.IsWindows() ? "windows" : "unix";
        await Verifier.Verify(snapshots, SnapshotSettings(platform));
    }

    private static VerifySettings SnapshotSettings(string platform)
    {
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        settings.UseTextForParameters(platform);
        return settings;
    }

    private static RecordBatch CreateRecordBatch(RecordBatchAttributes attributes, bool includeHeaders)
    {
        var headers = includeHeaders
            ? new[]
            {
                new Header("trace-id", "abc123"u8.ToArray()),
                new Header("empty", [])
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
        return new RecordBatchWireSnapshot(scenario, Convert.ToHexString(buffer.WrittenSpan));
    }

    private sealed record RecordBatchWireSnapshot(string Scenario, string Bytes);
}
