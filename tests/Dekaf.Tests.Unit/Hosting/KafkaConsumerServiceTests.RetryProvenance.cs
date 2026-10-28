using System.Text;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

/// <summary>
/// Retry-control headers are trusted only on records from a configured retry topic, and the
/// original source topic comes from the retry-topic mapping, never from a header.
/// </summary>
public sealed partial class KafkaConsumerServiceTests
{
    private static readonly TimeSpan[] ProvenanceDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    [Test]
    public async Task RetryProvenance_SourceRecordWithFutureDueTime_IsProcessedWithoutPostponing()
    {
        var consumer = CreateConsumerSubstitute();
        await using var service = new TestConsumerService(consumer, ["orders"],
            deadLetterOptions: ProvenanceDeadLetterOptions());
        var result = CreateResult("orders", partition: 1, offset: 42, headers:
        [
            StringHeader(RetryTopicHeaders.DueTimestampMsKey,
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        ]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(1);
        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
    }

    [Test]
    public async Task RetryProvenance_RetryRecordWithFutureDueTime_IsPostponed()
    {
        var consumer = CreateConsumerSubstitute();
        await using var service = new TestConsumerService(consumer, ["orders"],
            deadLetterOptions: ProvenanceDeadLetterOptions());
        var headers = RetryTopicHeaders.Build(CreateResult("orders"), 1, ProvenanceDelays[0],
            DateTimeOffset.UtcNow.AddMinutes(5));
        var result = CreateResult("orders-retry-5s", partition: 1, offset: 42, headers: headers.ToList());

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(0);
    }

    [Test]
    public async Task RetryProvenance_SourceRecordWithForgedSourceTopic_RoutesToItsOwnFirstRetryTier()
    {
        var (service, sent) = CreateProvenanceService();
        await using var _ = service;
        var result = CreateResult("orders", partition: 1, offset: 42, headers:
        [
            StringHeader("app", "kept"),
            StringHeader(RetryTopicHeaders.SourceTopicKey, "unrelated"),
            StringHeader(RetryTopicHeaders.SourcePartitionKey, "7"),
            StringHeader(RetryTopicHeaders.SourceOffsetKey, "700")
        ]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(sent).Count().IsEqualTo(1);
        var message = sent[0];
        await Assert.That(message.Topic).IsEqualTo("orders-retry-5s");
        await Assert.That(message.Headers!.GetFirstAsString("app")).IsEqualTo("kept");
        await Assert.That(message.Headers!.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(message.Headers!.GetFirstAsString(RetryTopicHeaders.SourcePartitionKey)).IsEqualTo("1");
        await Assert.That(message.Headers!.GetFirstAsString(RetryTopicHeaders.SourceOffsetKey)).IsEqualTo("42");
        await Assert.That(message.Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("1");
        await Assert.That(CountHeaders(message.Headers, RetryTopicHeaders.SourceTopicKey)).IsEqualTo(1);
    }

    [Test]
    public async Task RetryProvenance_SourceRecordWithFailureCountBeyondTiers_StartsAtFirstRetryTier()
    {
        var (service, sent) = CreateProvenanceService();
        await using var _ = service;
        var result = CreateResult("orders", partition: 1, offset: 42, headers:
        [
            StringHeader(RetryTopicHeaders.FailureCountKey, "9")
        ]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(sent).Count().IsEqualTo(1);
        await Assert.That(sent[0].Topic).IsEqualTo("orders-retry-5s");
        await Assert.That(sent[0].Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("1");
    }

    [Test]
    public async Task RetryProvenance_RetryRecordWithForgedSourceTopic_UsesMappedSourceForNextTierAndDeadLetter()
    {
        var (service, sent) = CreateProvenanceService();
        await using var _ = service;
        var firstTierHeaders = new List<Header>(RetryTopicHeaders.Build(CreateResult("orders", partition: 1, offset: 42), 1,
            ProvenanceDelays[0], DateTimeOffset.UtcNow.AddSeconds(-1)));
        ReplaceHeader(firstTierHeaders, RetryTopicHeaders.SourceTopicKey, "unrelated");
        var secondTierHeaders = new List<Header>(RetryTopicHeaders.Build(CreateResult("orders", partition: 1, offset: 42), 2,
            ProvenanceDelays[1], DateTimeOffset.UtcNow.AddSeconds(-1)));
        ReplaceHeader(secondTierHeaders, RetryTopicHeaders.SourceTopicKey, "unrelated");

        await ProcessWithRetriesAsync(service, CreateResult("orders-retry-5s", 0, 10, firstTierHeaders), CancellationToken.None);
        await ProcessWithRetriesAsync(service, CreateResult("orders-retry-30s", 0, 20, secondTierHeaders), CancellationToken.None);

        await Assert.That(sent).Count().IsEqualTo(2);
        await Assert.That(sent[0].Topic).IsEqualTo("orders-retry-30s");
        await Assert.That(sent[0].Headers!.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(sent[1].Topic).IsEqualTo("orders.DLQ");
        await Assert.That(sent[1].Headers!.GetFirstAsString(DeadLetterHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(sent[1].Headers!.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders");
    }

    [Test]
    public async Task RetryProvenance_LegitimateMultiTierRetries_AdvanceThroughEveryTierToDeadLetter()
    {
        var (service, sent) = CreateProvenanceService();
        await using var _ = service;

        await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), CancellationToken.None);
        await ProcessWithRetriesAsync(service, CreateResult(sent[0].Topic, 0, 10, AsDue(sent[0].Headers!)), CancellationToken.None);
        await ProcessWithRetriesAsync(service, CreateResult(sent[1].Topic, 0, 20, AsDue(sent[1].Headers!)), CancellationToken.None);

        await Assert.That(sent).Count().IsEqualTo(3);
        await Assert.That(sent[0].Topic).IsEqualTo("orders-retry-5s");
        await Assert.That(sent[1].Topic).IsEqualTo("orders-retry-30s");
        await Assert.That(sent[2].Topic).IsEqualTo("orders.DLQ");
        await Assert.That(sent[1].Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("2");
        await Assert.That(sent[1].Headers!.GetFirstAsString(RetryTopicHeaders.SourcePartitionKey)).IsEqualTo("1");
        await Assert.That(sent[1].Headers!.GetFirstAsString(RetryTopicHeaders.SourceOffsetKey)).IsEqualTo("42");
        await Assert.That(sent[2].Headers!.GetFirstAsString(DeadLetterHeaders.FailureCountKey)).IsEqualTo("3");
        await Assert.That(sent[2].Headers!.GetFirstAsString(DeadLetterHeaders.SourcePartitionKey)).IsEqualTo("1");
        await Assert.That(sent[2].Headers!.GetFirstAsString(DeadLetterHeaders.SourceOffsetKey)).IsEqualTo("42");
    }

    [Test]
    public async Task RetryProvenance_RetryTopicsDisabled_DeadLetterIgnoresForgedRetryHeaders()
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        ProducerMessage<byte[]?, byte[]?>? sent = null;
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent = call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0); return new ValueTask<RecordMetadata>(default(RecordMetadata)); });
        await using var service = new FailingConsumerService(CreateConsumerSubstitute(), ["orders"], new DeadLetterOptions());
        SetDlqProducer(service, producer);
        var result = CreateResult("orders", partition: 1, offset: 42, headers:
        [
            StringHeader("app", "kept"),
            StringHeader(RetryTopicHeaders.SourceTopicKey, "unrelated"),
            StringHeader(RetryTopicHeaders.SourceOffsetKey, "700"),
            StringHeader(RetryTopicHeaders.FailureCountKey, "9")
        ]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.Topic).IsEqualTo("orders.DLQ");
        await Assert.That(sent.Headers!.GetFirstAsString("app")).IsEqualTo("kept");
        await Assert.That(sent.Headers!.GetFirstAsString(DeadLetterHeaders.SourceTopicKey)).IsEqualTo("orders");
        await Assert.That(sent.Headers!.GetFirstAsString(DeadLetterHeaders.SourceOffsetKey)).IsEqualTo("42");
        await Assert.That(sent.Headers!.GetFirstAsString(DeadLetterHeaders.FailureCountKey)).IsEqualTo("1");
        await Assert.That(sent.Headers!.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsNull();
        await Assert.That(sent.Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsNull();
    }

    [Test]
    public async Task RetryProvenance_ExplicitSourceTopicNamedLikeRetryTopic_IsTreatedAsSource()
    {
        var (service, sent) = CreateProvenanceService(["orders", "orders-retry-5s"]);
        await using var _ = service;
        var result = CreateResult("orders-retry-5s", partition: 1, offset: 42, headers:
        [
            StringHeader(RetryTopicHeaders.SourceTopicKey, "orders"),
            StringHeader(RetryTopicHeaders.FailureCountKey, "1")
        ]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(sent).Count().IsEqualTo(1);
        await Assert.That(sent[0].Topic).IsEqualTo("orders-retry-5s-retry-5s");
        await Assert.That(sent[0].Headers!.GetFirstAsString(RetryTopicHeaders.SourceTopicKey)).IsEqualTo("orders-retry-5s");
        await Assert.That(sent[0].Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey)).IsEqualTo("1");
    }

    private static DeadLetterOptions ProvenanceDeadLetterOptions() => new()
    {
        RetryTopics = new RetryTopicOptions { Delays = ProvenanceDelays }
    };

    private static (FailingConsumerService Service, List<ProducerMessage<byte[]?, byte[]?>> Sent) CreateProvenanceService(
        string[]? topics = null)
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        var sent = new List<ProducerMessage<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent.Add(call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0)); return new ValueTask<RecordMetadata>(default(RecordMetadata)); });
        var service = new FailingConsumerService(CreateConsumerSubstitute(), topics ?? ["orders"], ProvenanceDeadLetterOptions());
        SetDlqProducer(service, producer);
        return (service, sent);
    }

    private static Header StringHeader(string key, string value) => new(key, Encoding.UTF8.GetBytes(value));

    private static void ReplaceHeader(List<Header> headers, string key, string value)
    {
        var index = headers.FindIndex(header => header.Key == key);
        headers[index] = StringHeader(key, value);
    }

    /// <summary>Copies routed retry headers as they read once the retry delay has elapsed.</summary>
    private static List<Header> AsDue(IReadOnlyList<Header> headers)
    {
        var copy = new List<Header>(headers);
        ReplaceHeader(copy, RetryTopicHeaders.DueTimestampMsKey,
            DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        return copy;
    }

    private static int CountHeaders(IReadOnlyList<Header> headers, string key)
    {
        var count = 0;
        for (var index = 0; index < headers.Count; index++)
        {
            if (headers[index].Key == key)
                count++;
        }

        return count;
    }
}
