using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class TransactionIntrospectionMessageTests
{
    [Test]
    public async Task DescribeProducersRequest_V0_EncodesTopicsAndPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeProducersRequest
        {
            Topics =
            [
                new DescribeProducersRequestTopic
                {
                    Name = "orders",
                    PartitionIndexes = [0, 2]
                }
            ]
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var topicsLengthPlus1 = reader.ReadUnsignedVarInt();
        var topicName = reader.ReadCompactNonNullableString();
        var partitionsLengthPlus1 = reader.ReadUnsignedVarInt();
        var partition0 = reader.ReadInt32();
        var partition1 = reader.ReadInt32();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();

        await Assert.That(topicsLengthPlus1).IsEqualTo(2);
        await Assert.That(topicName).IsEqualTo("orders");
        await Assert.That(partitionsLengthPlus1).IsEqualTo(3);
        await Assert.That(partition0).IsEqualTo(0);
        await Assert.That(partition1).IsEqualTo(2);
    }

    [Test]
    public async Task DescribeProducersResponse_V0_ParsesActiveProducerState()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(15);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("orders");
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(2);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt64(1234);
        writer.WriteInt32(5);
        writer.WriteInt32(42);
        writer.WriteInt64(1700000000000);
        writer.WriteInt32(9);
        writer.WriteInt64(88);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeProducersResponse)DescribeProducersResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(15);
        await Assert.That(response.Topics[0].Name).IsEqualTo("orders");
        var partition = response.Topics[0].Partitions[0];
        await Assert.That(partition.PartitionIndex).IsEqualTo(2);
        await Assert.That(partition.ErrorCode).IsEqualTo(ErrorCode.None);
        var producer = partition.ActiveProducers[0];
        await Assert.That(producer.ProducerId).IsEqualTo(1234);
        await Assert.That(producer.ProducerEpoch).IsEqualTo(5);
        await Assert.That(producer.LastSequence).IsEqualTo(42);
        await Assert.That(producer.CurrentTxnStartOffset).IsEqualTo(88);
    }

    [Test]
    public async Task DescribeTransactionsResponse_V0_ParsesTransactionPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString("tx-orders");
        writer.WriteCompactString("Ongoing");
        writer.WriteInt32(60000);
        writer.WriteInt64(1700000000000);
        writer.WriteInt64(77);
        writer.WriteInt16(3);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("orders");
        writer.WriteUnsignedVarInt(3);
        writer.WriteInt32(0);
        writer.WriteInt32(1);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeTransactionsResponse)DescribeTransactionsResponse.Read(ref reader, version: 0);

        var state = response.TransactionStates[0];
        await Assert.That(state.TransactionalId).IsEqualTo("tx-orders");
        await Assert.That(state.TransactionState).IsEqualTo("Ongoing");
        await Assert.That(state.ProducerId).IsEqualTo(77);
        await Assert.That(state.ProducerEpoch).IsEqualTo((short)3);
        await Assert.That(state.Topics[0].Topic).IsEqualTo("orders");
        await Assert.That(state.Topics[0].Partitions).IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task ListTransactionsRequest_V2_EncodesAllFilters()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListTransactionsRequest
        {
            StateFilters = ["Ongoing"],
            ProducerIdFilters = [77],
            DurationFilterMs = 30000,
            TransactionalIdPattern = "tx-.*"
        };

        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();
        var state = reader.ReadCompactNonNullableString();
        var producerIdsLengthPlus1 = reader.ReadUnsignedVarInt();
        var producerId = reader.ReadInt64();
        var durationFilterMs = reader.ReadInt64();
        var pattern = reader.ReadCompactString();
        reader.SkipTaggedFields();

        await Assert.That(statesLengthPlus1).IsEqualTo(2);
        await Assert.That(state).IsEqualTo("Ongoing");
        await Assert.That(producerIdsLengthPlus1).IsEqualTo(2);
        await Assert.That(producerId).IsEqualTo(77);
        await Assert.That(durationFilterMs).IsEqualTo(30000);
        await Assert.That(pattern).IsEqualTo("tx-.*");
    }

    [Test]
    public async Task ListTransactionsRequest_V0_OmitsDurationAndTransactionalIdPattern()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListTransactionsRequest
        {
            StateFilters = ["Ongoing"],
            ProducerIdFilters = [77],
            DurationFilterMs = 30000,
            TransactionalIdPattern = "tx-.*"
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();
        var state = reader.ReadCompactNonNullableString();
        var producerIdsLengthPlus1 = reader.ReadUnsignedVarInt();
        var producerId = reader.ReadInt64();
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;

        await Assert.That(statesLengthPlus1).IsEqualTo(2);
        await Assert.That(state).IsEqualTo("Ongoing");
        await Assert.That(producerIdsLengthPlus1).IsEqualTo(2);
        await Assert.That(producerId).IsEqualTo(77);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ListTransactionsRequest_V1_EncodesDurationAndOmitsTransactionalIdPattern()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListTransactionsRequest
        {
            StateFilters = ["Ongoing"],
            ProducerIdFilters = [77],
            DurationFilterMs = 30000,
            TransactionalIdPattern = "tx-.*"
        };

        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();
        var state = reader.ReadCompactNonNullableString();
        var producerIdsLengthPlus1 = reader.ReadUnsignedVarInt();
        var producerId = reader.ReadInt64();
        var durationFilterMs = reader.ReadInt64();
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;

        await Assert.That(statesLengthPlus1).IsEqualTo(2);
        await Assert.That(state).IsEqualTo("Ongoing");
        await Assert.That(producerIdsLengthPlus1).IsEqualTo(2);
        await Assert.That(producerId).IsEqualTo(77);
        await Assert.That(durationFilterMs).IsEqualTo(30000);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ListTransactionsResponse_V2_ParsesUnknownFiltersAndStates()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(5);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("UnknownState");
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString("tx-orders");
        writer.WriteInt64(77);
        writer.WriteCompactString("Ongoing");
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ListTransactionsResponse)ListTransactionsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(5);
        await Assert.That(response.UnknownStateFilters).IsEquivalentTo(["UnknownState"]);
        await Assert.That(response.TransactionStates[0].TransactionalId).IsEqualTo("tx-orders");
        await Assert.That(response.TransactionStates[0].ProducerId).IsEqualTo(77);
        await Assert.That(response.TransactionStates[0].TransactionState).IsEqualTo("Ongoing");
    }
}
