using System.Buffers;
using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

[NotInParallel("MeterListener")]
public sealed class FetchResponseParseErrorTests
{
    [Test]
    public async Task Read_CrcMismatch_SurfacesPartitionParseError()
    {
        var buffer = CreateResponseWithCrcMismatch();

        var response = ReadResponse(buffer, checkCrcs: true);
        var partition = response.Responses[0].Partitions[0];

        await Assert.That(partition.RecordParseError).IsNotNull();
        await Assert.That(partition.RecordParseError!.InnerException).IsTypeOf<InvalidDataException>();
        await Assert.That(partition.Records).IsNull();

        response.ReturnToPool();
    }

    [Test]
    public async Task Read_MalformedRecordBatch_ThrowsTypedErrorAndRecordsMetric()
    {
        long parseErrors = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                    instrument.Name == "messaging.consumer.batch.parse.errors")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => parseErrors += value);
        listener.Start();

        var buffer = CreateResponseWithMalformedBatch();

        var response = ReadResponse(buffer);
        var partition = response.Responses[0].Partitions[0];

        await Assert.That(partition.RecordParseError).IsNotNull();
        await Assert.That(partition.RecordParseError!.Message).Contains("partition 7");
        await Assert.That(partition.Records).IsNull();
        await Assert.That(response.Responses[0].Partitions).Count().IsEqualTo(2);
        await Assert.That(response.Responses[0].Partitions[1].PartitionIndex).IsEqualTo(8);
        await Assert.That(response.Responses[0].Partitions[1].RecordParseError).IsNull();
        await Assert.That(parseErrors).IsEqualTo(1);

        response.ReturnToPool();
    }

    private static FetchResponse ReadResponse(ReadOnlyMemory<byte> buffer, bool checkCrcs = false)
    {
        using var memory = PooledResponseMemory.Create(
            buffer.ToArray(),
            buffer.Length,
            isPooled: false,
            offset: 0);
        using var scope = ResponseParsingContext.SetPooledMemory(memory, checkCrcs);
        var reader = new KafkaProtocolReader(memory.Memory);
        return (FetchResponse)FetchResponse.Read(ref reader, version: 16);
    }

    private static ReadOnlyMemory<byte> CreateResponseWithCrcMismatch()
    {
        var batchBuffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch
        {
            BaseOffset = 0,
            PartitionLeaderEpoch = -1,
            BaseTimestamp = 1_000,
            MaxTimestamp = 1_000,
            Records = [new Record { OffsetDelta = 0, TimestampDelta = 0, Value = "value"u8.ToArray() }]
        })
        {
            batch.Write(batchBuffer);
        }

        var batchBytes = batchBuffer.WrittenSpan.ToArray();
        batchBytes[^1] ^= 0x01;

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(Guid.NewGuid());
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(7);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(10);
        writer.WriteInt64(10);
        writer.WriteInt64(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteInt32(-1);
        writer.WriteCompactNullableBytes(batchBytes, isNull: false);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);

        return buffer.WrittenMemory;
    }

    private static ReadOnlyMemory<byte> CreateResponseWithMalformedBatch()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(Guid.NewGuid());
        writer.WriteUnsignedVarInt(3);
        writer.WriteInt32(7);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(10);
        writer.WriteInt64(10);
        writer.WriteInt64(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteInt32(-1);
        writer.WriteCompactNullableBytes(new byte[RecordBatch.TotalBatchHeaderSize], isNull: false);
        writer.WriteUnsignedVarInt(0);
        writer.WriteInt32(8);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(10);
        writer.WriteInt64(10);
        writer.WriteInt64(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteInt32(-1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);

        return buffer.WrittenMemory;
    }
}
