using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
public sealed class ProducerLimitBoundaryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int BoundarySize = 1_024;
    private const int TraceparentValueLength = 55;
    private static readonly Header[] InjectedTraceHeaders =
        [new("traceparent", new byte[TraceparentValueLength])];

    [Test]
    public async Task MaxRequestSize_ExactRequestSucceeds_OneByteOverIsRejectedLocallyWithoutDrop()
    {
        using var activityListener = ListenToDekafActivities();
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var maxEncodedBatchSize = ProduceRequestSizeCalculator.GetMaxEncodedBatchSize(
            BoundarySize,
            transactionalId: null,
            topic);
        var exactPayload = CreatePayloadForEncodedBatchSize(maxEncodedBatchSize);
        var oversizedPayload = new byte[exactPayload.Length + 1];
        oversizedPayload.AsSpan().Fill(0x2A);
        var sentinel = new byte[] { 0x7F };

        await Assert.That(ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
            transactionalId: null,
            topic,
            GetEncodedBatchSize(exactPayload.Length))).IsEqualTo(BoundarySize);
        await Assert.That(ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
            transactionalId: null,
            topic,
            GetEncodedBatchSize(oversizedPayload.Length))).IsEqualTo(BoundarySize + 1);

        await using var producer = await Kafka.CreateProducer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("request-boundary-producer")
            .WithAcks(Acks.All)
            .WithBatchSize(BoundarySize * 2)
            .WithMaxRequestSize(BoundarySize)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var exactResult = await producer.ProduceAsync(topic, null, exactPayload);

        var exception = await Assert.That(async () =>
        {
            await producer.ProduceAsync(topic, null, oversizedPayload);
        }).Throws<ProduceException>();

        var sentinelResult = await producer.ProduceAsync(topic, null, sentinel);

        await Assert.That(exactResult.Offset).IsEqualTo(0);
        await Assert.That(sentinelResult.Offset).IsEqualTo(1);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(exception.Topic).IsEqualTo(topic);
        await Assert.That(exception.Partition).IsEqualTo(0);

        await AssertOnlyExpectedRecords(topic, exactPayload, sentinel);
    }

    [Test]
    public async Task BrokerMessageMaxBytes_ExactBatchSucceeds_OneByteOverIsRejectedWithoutDrop()
    {
        using var activityListener = ListenToDekafActivities();
        var topic = $"broker-size-boundary-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["max.message.bytes"] = BoundarySize.ToString()
                }
            }
        ]);

        var exactPayload = CreatePayloadForEncodedBatchSize(BoundarySize);
        var oversizedPayload = new byte[exactPayload.Length + 1];
        oversizedPayload.AsSpan().Fill(0x3B);
        var sentinel = new byte[] { 0x6E };

        await using var producer = await Kafka.CreateProducer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("broker-boundary-producer")
            .WithAcks(Acks.All)
            .WithBatchSize(BoundarySize * 4)
            .WithMaxRequestSize(BoundarySize * 4)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var exactResult = await producer.ProduceAsync(topic, null, exactPayload);

        var exception = await Assert.That(async () =>
        {
            await producer.ProduceAsync(topic, null, oversizedPayload);
        }).Throws<ProduceException>();

        var sentinelResult = await producer.ProduceAsync(topic, null, sentinel);

        await Assert.That(exactResult.Offset).IsEqualTo(0);
        await Assert.That(sentinelResult.Offset).IsEqualTo(1);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(exception.Topic).IsEqualTo(topic);
        await Assert.That(exception.Partition).IsEqualTo(0);

        await AssertOnlyExpectedRecords(topic, exactPayload, sentinel);
    }

    private async Task AssertOnlyExpectedRecords(string topic, byte[] firstValue, byte[] secondValue)
    {
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"boundary-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30));
        var second = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30));
        var unexpected = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Value).IsEquivalentTo(firstValue);
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.Value).IsEquivalentTo(secondValue);
        await Assert.That(unexpected).IsNull();
    }

    private static byte[] CreatePayloadForEncodedBatchSize(int targetSize)
    {
        for (var payloadLength = 0; payloadLength < targetSize; payloadLength++)
        {
            var encodedSize = GetEncodedBatchSize(payloadLength);
            if (encodedSize == targetSize)
                return new byte[payloadLength];
            if (encodedSize > targetSize)
                break;
        }

        throw new InvalidOperationException($"No single-record payload encodes to exactly {targetSize} bytes.");
    }

    private static int GetEncodedBatchSize(int payloadLength)
    {
        var bodySize = Record.ComputeBodySize(
            timestampDelta: 0,
            offsetDelta: 0,
            isKeyNull: true,
            keyLength: 0,
            isValueNull: false,
            valueLength: payloadLength,
            headers: InjectedTraceHeaders,
            headerCount: InjectedTraceHeaders.Length);
        return RecordBatch.TotalBatchHeaderSize + Record.VarIntSize(bodySize) + bodySize;
    }

    private static ActivityListener ListenToDekafActivities()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
