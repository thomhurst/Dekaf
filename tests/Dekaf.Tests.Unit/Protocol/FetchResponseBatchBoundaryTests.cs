using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class FetchResponseBatchBoundaryTests
{
    [Test]
    public async Task RecordBatch_Read_HeaderStubs_DoNotCrossAvailableBoundary()
    {
        for (var stubLength = 1; stubLength < RecordBatch.TotalBatchHeaderSize; stubLength++)
        {
            var (threw, consumed) = ReadHeaderStub(stubLength);
            await Assert.That(threw).IsTrue();
            await Assert.That(consumed).IsEqualTo(stubLength);
        }
    }

    [Test]
    public async Task FetchResponse_Read_HeaderStub_PreservesNextPartitionAlignment()
    {
        var firstBatch = CreateBatchBytes(baseOffset: 10, "first");
        var secondBatch = CreateBatchBytes(baseOffset: 20, "second");
        var firstPartitionRecords = new byte[firstBatch.Length + 60];
        firstBatch.CopyTo(firstPartitionRecords, 0);
        firstBatch.AsSpan(0, 60).CopyTo(firstPartitionRecords.AsSpan(firstBatch.Length));

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);                     // throttle time
        writer.WriteInt16((short)ErrorCode.None); // error code
        writer.WriteInt32(0);                     // session id
        writer.WriteUnsignedVarInt(2);            // one topic
        writer.WriteUuid(Guid.NewGuid());
        writer.WriteUnsignedVarInt(3);            // two partitions
        WritePartition(ref writer, partitionIndex: 0, firstPartitionRecords);
        WritePartition(ref writer, partitionIndex: 1, secondBatch);
        writer.WriteUnsignedVarInt(0);             // topic tagged fields
        writer.WriteUnsignedVarInt(0);             // response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (FetchResponse)FetchResponse.Read(ref reader, version: 13);

        int partitionCount;
        int firstBatchCount;
        int secondBatchCount;
        long secondBaseOffset;
        var consumed = reader.Consumed;
        try
        {
            partitionCount = response.Responses[0].Partitions.Count;
            firstBatchCount = response.Responses[0].Partitions[0].Records!.Count;
            secondBatchCount = response.Responses[0].Partitions[1].Records!.Count;
            secondBaseOffset = response.Responses[0].Partitions[1].Records![0].BaseOffset;
        }
        finally
        {
            ReturnRecords(response);
            response.ReturnToPool();
        }

        await Assert.That(partitionCount).IsEqualTo(2);
        await Assert.That(firstBatchCount).IsEqualTo(1);
        await Assert.That(secondBatchCount).IsEqualTo(1);
        await Assert.That(secondBaseOffset).IsEqualTo(20);
        await Assert.That(consumed).IsEqualTo(buffer.WrittenCount);
    }

    private static byte[] CreateBatchBytes(long baseOffset, string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var batch = new RecordBatch
        {
            BaseOffset = baseOffset,
            PartitionLeaderEpoch = -1,
            BaseTimestamp = 1_000,
            MaxTimestamp = 1_000,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Value = System.Text.Encoding.UTF8.GetBytes(value)
                }
            ]
        };
        batch.Write(buffer);
        return buffer.WrittenSpan.ToArray();
    }

    private static (bool Threw, long Consumed) ReadHeaderStub(int stubLength)
    {
        var batchBytes = CreateBatchBytes(baseOffset: 0, "first");
        var combined = new byte[batchBytes.Length + RecordBatch.TotalBatchHeaderSize];
        batchBytes.AsSpan(0, stubLength).CopyTo(combined);
        combined.AsSpan(stubLength).Fill(0xFF);
        var reader = new KafkaProtocolReader(combined);

        try
        {
            RecordBatch.Read(ref reader, availableBytes: stubLength);
            return (false, reader.Consumed);
        }
        catch (InsufficientDataException)
        {
            return (true, reader.Consumed);
        }
    }

    private static void WritePartition(ref KafkaProtocolWriter writer, int partitionIndex, ReadOnlySpan<byte> records)
    {
        writer.WriteInt32(partitionIndex);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(100);             // high watermark
        writer.WriteInt64(100);             // last stable offset
        writer.WriteInt64(0);               // log start offset
        writer.WriteUnsignedVarInt(1);      // empty aborted transactions
        writer.WriteInt32(-1);              // preferred read replica
        writer.WriteUnsignedVarInt(records.Length + 1);
        writer.WriteRawBytes(records);
        writer.WriteUnsignedVarInt(0);      // partition tagged fields
    }

    private static void ReturnRecords(FetchResponse response)
    {
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.Records is not List<RecordBatch> batches)
                {
                    continue;
                }

                foreach (var batch in batches)
                {
                    batch.Dispose();
                }

                FetchResponsePartition.ReturnRecordBatchList(batches);
            }
        }
    }
}
