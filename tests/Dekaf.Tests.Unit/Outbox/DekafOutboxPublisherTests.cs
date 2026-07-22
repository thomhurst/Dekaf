using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Outbox;

public class DekafOutboxPublisherTests
{
    private const string HeaderName = "x-outbox-message-id";

    private static OutboxMessage Row(long id, byte[]? headers = null) => new()
    {
        Id = id,
        MessageId = Guid.NewGuid(),
        Bucket = 0,
        Topic = "topic",
        Key = [1],
        Value = [2],
        Headers = headers,
        CreatedAtUtc = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
    };

    private static RecordMetadata Metadata { get; } = new()
    {
        Topic = "topic",
        Partition = 0,
        Offset = 1,
        Timestamp = DateTimeOffset.UnixEpoch
    };

    [Test]
    public async Task Publish_EmptyBatch_ReturnsZeroWithoutProducing()
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(0);
        await Assert.That(result.FirstError).IsNull();
        await producer.DidNotReceiveWithAnyArgs()
            .ProduceAsync(default(ProducerMessage<byte[]?, byte[]?>)!, CancellationToken.None);
    }

    [Test]
    public async Task Publish_StampsMessageIdHeaderAndPreservesExistingHeaders()
    {
        var captured = new List<ProducerMessage<byte[]?, byte[]?>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured.Add(callInfo.Arg<ProducerMessage<byte[]?, byte[]?>>()!);
                return new ValueTask<RecordMetadata>(Metadata);
            });
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var existingHeaders = OutboxHeaderCodec.Encode(new Headers().Add("trace", "abc"));
        var row = Row(1, existingHeaders);
        var result = await publisher.PublishAsync([row], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(result.FirstError).IsNull();
        await Assert.That(captured.Count).IsEqualTo(1);

        var message = captured[0];
        await Assert.That(message.Topic).IsEqualTo("topic");
        await Assert.That(message.Key).IsEquivalentTo(row.Key!);
        await Assert.That(message.Value).IsEquivalentTo(row.Value!);
        await Assert.That(message.Timestamp).IsEqualTo(row.CreatedAtUtc);

        var headers = message.Headers!;
        await Assert.That(headers.Count).IsEqualTo(2);
        await Assert.That(headers[0].Key).IsEqualTo("trace");
        await Assert.That(headers[1].Key).IsEqualTo(HeaderName);
        var stampedId = System.Text.Encoding.UTF8.GetString(headers[1].Value.Span);
        await Assert.That(stampedId).IsEqualTo(row.MessageId.ToString("D"));
    }

    [Test]
    public async Task Publish_FirstRecordFails_AcksNothingEvenWhenLaterRecordsSucceed()
    {
        var failure = new InvalidOperationException("broker rejected");
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => ValueTask.FromException<RecordMetadata>(failure),
                _ => new ValueTask<RecordMetadata>(Metadata),
                _ => new ValueTask<RecordMetadata>(Metadata));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([Row(1), Row(2), Row(3)], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(0);
        await Assert.That(result.FirstError).IsEqualTo(failure);
    }

    [Test]
    public async Task Publish_MiddleRecordFails_AcksOnlyContiguousPrefix()
    {
        var failure = new InvalidOperationException("broker rejected");
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => new ValueTask<RecordMetadata>(Metadata),
                _ => ValueTask.FromException<RecordMetadata>(failure),
                _ => new ValueTask<RecordMetadata>(Metadata));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([Row(1), Row(2), Row(3)], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(result.FirstError).IsEqualTo(failure);
    }

    [Test]
    public async Task Publish_CallerSuppliedReservedHeader_IsReplacedByStampedId()
    {
        var captured = new List<ProducerMessage<byte[]?, byte[]?>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured.Add(callInfo.Arg<ProducerMessage<byte[]?, byte[]?>>()!);
                return new ValueTask<RecordMetadata>(Metadata);
            });
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        // A colliding caller header must not shadow the stamped id: consumers read the
        // first header with this name for dedup.
        var collidingHeaders = OutboxHeaderCodec.Encode(new Headers()
            .Add(HeaderName, "spoofed")
            .Add("trace", "abc"));
        var row = Row(1, collidingHeaders);
        await publisher.PublishAsync([row], HeaderName);

        var headers = captured[0].Headers!;
        var stamped = headers.GetAll(HeaderName).Select(h => h.GetValueAsString()).ToList();

        await Assert.That(stamped.Count).IsEqualTo(1);
        await Assert.That(stamped[0]).IsEqualTo(row.MessageId.ToString("D"));
        await Assert.That(headers.GetFirst("trace")).IsNotNull();
    }

    [Test]
    public async Task Publish_CancelledDuringShutdown_StopsPrefixWithoutRecordingError()
    {
        using var cts = new CancellationTokenSource();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => new ValueTask<RecordMetadata>(Metadata),
                _ =>
                {
                    cts.Cancel();
                    return ValueTask.FromException<RecordMetadata>(new TaskCanceledException());
                },
                _ => new ValueTask<RecordMetadata>(Metadata));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([Row(1), Row(2), Row(3)], HeaderName, cts.Token);

        // Shutdown is not a broker failure (no misleading error), but the acked prefix must
        // still stop at the cancelled record so no later row is marked past it.
        await Assert.That(result.FirstError).IsNull();
        await Assert.That(result.AckedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Publish_SynchronousProduceThrow_StillAwaitsAlreadyStartedTasks()
    {
        var failure = new ObjectDisposedException("producer");
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => new ValueTask<RecordMetadata>(Metadata),
                _ => throw failure);
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([Row(1), Row(2), Row(3)], HeaderName);

        // The first record's ack still counts, the sync throw is surfaced, and no produce
        // was attempted past the failure.
        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(result.FirstError).IsEqualTo(failure);
        await producer.ReceivedWithAnyArgs(2)
            .ProduceAsync(default(ProducerMessage<byte[]?, byte[]?>)!, CancellationToken.None);
    }

    [Test]
    public async Task Publish_CorruptHeaderBlob_StillAwaitsAlreadyStartedTasks()
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(Metadata));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        // Row 2 carries an unreadable persisted header blob (unknown format version).
        var corrupt = OutboxHeaderCodec.Encode(new Headers().Add("a", "b"))!;
        corrupt[0] = 99;

        var result = await publisher.PublishAsync([Row(1), Row(2, corrupt), Row(3)], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(1);
        await Assert.That(result.FirstError).IsNotNull();
        await Assert.That(result.FirstError).IsTypeOf<FormatException>();
        await producer.ReceivedWithAnyArgs(1)
            .ProduceAsync(default(ProducerMessage<byte[]?, byte[]?>)!, CancellationToken.None);
    }

    [Test]
    public async Task Publish_AllSucceed_AcksEverything()
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(Metadata));
        await using var publisher = new DekafOutboxPublisher(producer, ownsProducer: false);

        var result = await publisher.PublishAsync([Row(1), Row(2), Row(3)], HeaderName);

        await Assert.That(result.AckedCount).IsEqualTo(3);
        await Assert.That(result.FirstError).IsNull();
    }
}
