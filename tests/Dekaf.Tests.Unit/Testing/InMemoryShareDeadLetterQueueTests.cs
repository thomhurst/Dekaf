using Dekaf.Consumer;
using System.Text;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareDeadLetterQueueTests
{
    private static readonly Dictionary<string, string> EnabledDeadLetterTopic =
        new() { ["errors.deadletterqueue.group.enable"] = "true" };

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RejectedRecord_IsWrittenToDeadLetterTopicWithContextHeaders(bool copyRecord)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("dlq.orders", configs: EnabledDeadLetterTopic);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Key = "key",
            Value = "value",
            Headers = new Headers { { "app", "kept" }, { ShareGroupDeadLetterHeaders.GroupKey, "spoofed" } }
        });
        await producer.ProduceAsync("orders", "key", "accepted");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", DeadLetterQueueTopic = "dlq.orders", DeadLetterQueueCopyRecord = copyRecord
        });
        consumer.Subscribe("orders");

        await foreach (var record in consumer.PollAsync())
        {
            consumer.Acknowledge(record, record.Offset == 0 ? AcknowledgeType.Reject : AcknowledgeType.Accept);
            if (record.Offset == 1)
                break;
        }
        await consumer.CommitAsync();

        var deadLetter = cluster.ReadRecords("dlq.orders").Single();
        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(deadLetter.Headers, out var context)).IsTrue();
        await Assert.That(context).IsEqualTo(new ShareGroupDeadLetterContext(
            "orders", 0, 0, "workers", 1, "Offset rejected by client."));
        await Assert.That(deadLetter.IsKeyNull).IsEqualTo(!copyRecord);
        await Assert.That(deadLetter.IsValueNull).IsEqualTo(!copyRecord);
        await Assert.That(deadLetter.Headers.Count).IsEqualTo(6);
        await Assert.That(deadLetter.Headers.Any(header => header.Key == "app")).IsFalse();
        if (copyRecord)
            await Assert.That(Encoding.UTF8.GetString(deadLetter.Value)).IsEqualTo("value");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task StaleReject_DoesNotDeadLetterOrCompleteReplacementLease(bool withFault, bool sameMemberId)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("dlq.orders", configs: EnabledDeadLetterTopic);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "replacement");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", MemberId = "first", DeadLetterQueueTopic = "dlq.orders"
        });
        consumer.Subscribe("orders");

        // Populate the completion buffer with a valid Reject before reusing it for a stale one.
        var first = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(first, AcknowledgeType.Reject);
        await consumer.CommitAsync();
        await Assert.That(cluster.ReadRecords("dlq.orders").Count).IsEqualTo(1);

        var stale = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(stale, AcknowledgeType.Reject);
        var barrier = withFault
            ? cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge,
                "orders", partition: 0, groupId: "workers"))
            : null;
        var pendingCommit = withFault ? consumer.CommitAsync().AsTask() : null;
        if (barrier is not null)
            await barrier.WaitUntilEnteredAsync();

        // Invalidate only the cluster registration, retaining the old consumer's pending Reject.
        var registration = (ShareGroupMemberRegistration)typeof(InMemoryShareConsumer<string, string>)
            .GetField("_registration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        cluster.UnregisterShareGroupMember("workers", "first", registration);
        await using var replacement = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", MemberId = sameMemberId ? "first" : "second", DeadLetterQueueTopic = "dlq.orders"
        });
        replacement.Subscribe("orders");
        var current = await replacement.PollAsync().FirstAsync();
        await Assert.That(current.Offset).IsEqualTo(stale.Offset);
        await Assert.That(current.DeliveryCount).IsEqualTo(2);

        if (barrier is not null)
            barrier.Release();
        if (pendingCommit is not null)
            await pendingCommit;
        else
            await consumer.CommitAsync();

        await Assert.That(cluster.ReadRecords("dlq.orders").Count).IsEqualTo(1);
        var partition = new TopicPartition("orders", 0);
        await Assert.That(cluster.GetCommittedShareOffset("workers", partition)).IsEqualTo(1);
        await using var other = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = "workers", MemberId = "third" });
        other.Subscribe("orders");
        await Assert.That(await other.PollAsync().ToListAsync()).IsEmpty();

        replacement.Acknowledge(current, AcknowledgeType.Reject);
        await replacement.CommitAsync();
        await replacement.CommitAsync();
        var deadLetters = cluster.ReadRecords("dlq.orders");
        await Assert.That(deadLetters.Count).IsEqualTo(2);
        await Assert.That(ShareGroupDeadLetterHeaders.TryParse(deadLetters[1].Headers, out var context)).IsTrue();
        await Assert.That(context.Offset).IsEqualTo(stale.Offset);
        await Assert.That(context.DeliveryCount).IsEqualTo(2);
        await Assert.That(cluster.GetCommittedShareOffset("workers", partition)).IsEqualTo(2);
    }

    [Test]
    public async Task RejectedRecord_IsNotDeadLetteredWithoutConfiguredTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = "workers" });
        consumer.Subscribe("orders");

        await foreach (var record in consumer.PollAsync())
        {
            consumer.Acknowledge(record, AcknowledgeType.Reject);
            break;
        }
        await consumer.CommitAsync();

        await Assert.That(cluster.ListTopics().Single()).IsEqualTo("orders");
    }

    [Test]
    public async Task MissingDeadLetterTopic_DoesNotFailAcknowledgementWithoutAutoCreate()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { AutoCreateTopics = false });
        cluster.CreateTopic("orders");
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", DeadLetterQueueTopic = "dlq.orders"
        });
        consumer.Subscribe("orders");

        await foreach (var record in consumer.PollAsync())
        {
            consumer.Acknowledge(record, AcknowledgeType.Reject);
            break;
        }
        await consumer.CommitAsync();

        await Assert.That(cluster.ListTopics().Single()).IsEqualTo("orders");
    }

    [Test]
    public async Task MissingDeadLetterTopic_IsNotAutoCreated()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", DeadLetterQueueTopic = "dlq.orders"
        });
        consumer.Subscribe("orders");

        await foreach (var record in consumer.PollAsync())
        {
            consumer.Acknowledge(record, AcknowledgeType.Reject);
            break;
        }
        await consumer.CommitAsync();

        await Assert.That(cluster.ListTopics().Single()).IsEqualTo("orders");
    }

    [Test]
    [Arguments(null)]
    [Arguments("false")]
    public async Task DeadLetterTopicWithoutGroupEnable_ReceivesNoRecords(string? enabled)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("dlq.orders", configs: enabled is null
            ? null
            : new Dictionary<string, string> { ["errors.deadletterqueue.group.enable"] = enabled });
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "workers", DeadLetterQueueTopic = "dlq.orders"
        });
        consumer.Subscribe("orders");

        await foreach (var record in consumer.PollAsync())
        {
            consumer.Acknowledge(record, AcknowledgeType.Reject);
            break;
        }
        await consumer.CommitAsync();

        await Assert.That(cluster.ReadRecords("dlq.orders")).IsEmpty();
    }

    [Test]
    public async Task InvalidDeadLetterTopic_IsRejected()
    {
        await Assert.That(() => new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster(),
            new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, DeadLetterQueueTopic = "__dlq" })).Throws<ArgumentException>();
    }
}
