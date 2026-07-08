using System.Buffers;
using System.Collections;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Regression tests for the PRODUCE framing guard (#1559). The records length prefix is
/// computed in a first pass over the batches and the batch bytes are written in a second
/// pass. If a batch mutates between the passes (concurrent lifecycle race / pooled-object
/// recycling), the declared length no longer matches the emitted bytes and the frame would
/// desync the connection's outgoing byte stream — the broker misparses every subsequent
/// request (InvalidRequestException: "Error reading byte array of N bytes") and closes the
/// socket. The guard must fail the request locally before it reaches the wire.
/// </summary>
public sealed class ProduceRequestFramingGuardTests
{
    [Test]
    public async Task PartitionData_Write_BatchGrowsBetweenSizeAndWritePass_ThrowsBeforeWire()
    {
        var caught = WriteWithMutation(static batch =>
        {
            var originalRecordsLength = batch.GetEncodedSize() - RecordBatch.TotalBatchHeaderSize;
            batch.SetPreEncodedRecords(new byte[originalRecordsLength + 64]);
        });

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(ErrorCode.CorruptMessage);
        await Assert.That(caught.IsRetriable).IsTrue();
        await Assert.That(caught.Message).Contains("framing mismatch");
    }

    [Test]
    public async Task PartitionData_Write_BatchShrinksBetweenSizeAndWritePass_ThrowsBeforeWire()
    {
        var caught = WriteWithMutation(static batch =>
        {
            var originalRecordsLength = batch.GetEncodedSize() - RecordBatch.TotalBatchHeaderSize;
            batch.SetPreEncodedRecords(new byte[originalRecordsLength - 1]);
        });

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(ErrorCode.CorruptMessage);
    }

    [Test]
    public async Task PartitionData_Write_StableBatch_DeclaredLengthMatchesEmittedBytes()
    {
        var batch = CreateBatch();
        var partitionData = new ProduceRequestPartitionData
        {
            Index = 0,
            Records = new[] { batch }
        };

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        reader.ReadInt32(); // partition index
        var declaredRecordsLength = reader.ReadUnsignedVarInt() - 1;
        reader.ReadRawBytes(declaredRecordsLength);
        reader.ReadUnsignedVarInt(); // tagged fields
        var readerEnd = reader.End;

        // Tracked writer count must equal the actual buffer bytes — this is the outer frame
        // size prefix consistency that keeps the connection frame-aligned.
        await Assert.That(writer.BytesWritten).IsEqualTo(buffer.WrittenCount);
        await Assert.That(readerEnd).IsTrue();
    }

    /// <summary>
    /// Serializes a single-batch partition where the batch is mutated after the size pass
    /// read it but before the write pass serializes it, mimicking the cross-thread
    /// mutation/recycle race. Returns the caught guard exception, or null if none was thrown.
    /// </summary>
    private static KafkaException? WriteWithMutation(Action<RecordBatch> mutate)
    {
        var batch = CreateBatch();
        var partitionData = new ProduceRequestPartitionData
        {
            Index = 7,
            Records = new SecondAccessMutatingList(batch, mutate)
        };

        try
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            partitionData.Write(ref writer, ProduceRequest.HighestSupportedVersion);
            return null;
        }
        catch (KafkaException ex)
        {
            return ex;
        }
    }

    private static RecordBatch CreateBatch() => new()
    {
        BaseOffset = 0,
        BaseTimestamp = 1000,
        MaxTimestamp = 1000,
        LastOffsetDelta = 0,
        ProducerId = -1,
        ProducerEpoch = -1,
        BaseSequence = -1,
        Records =
        [
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = "key"u8.ToArray(),
                Value = "value"u8.ToArray()
            }
        ]
    };

    /// <summary>
    /// Single-batch list that applies a mutation on the second indexer access. The size pass
    /// reads Records[0] once; the write pass reads it again, so the mutation lands exactly
    /// between sizing and writing — the same window the production race hits.
    /// </summary>
    private sealed class SecondAccessMutatingList(RecordBatch batch, Action<RecordBatch> mutate)
        : IReadOnlyList<RecordBatch>
    {
        private int _accessCount;

        public RecordBatch this[int index]
        {
            get
            {
                if (++_accessCount == 2)
                {
                    mutate(batch);
                }

                return batch;
            }
        }

        public int Count => 1;

        public IEnumerator<RecordBatch> GetEnumerator()
        {
            yield return batch;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
