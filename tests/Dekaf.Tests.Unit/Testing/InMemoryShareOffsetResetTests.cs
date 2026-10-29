using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareOffsetResetTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DefaultReset_InitializesAtFirstPollAndPersistsAcrossMembers(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("jobs", "key", "before-subscribe");
        await using var first = new InMemoryShareConsumer<string, string>(cluster);
        first.Subscribe("jobs");
        await producer.ProduceAsync("jobs", "key", "before-poll");
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "jobs", 0, "dekaf-in-memory-share")).Release();

        await Assert.That(await first.PollAsync().ToListAsync()).IsEmpty();
        await using var admin = new InMemoryAdminClient(cluster);
        var offsets = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["dekaf-in-memory-share"] = new() });
        await Assert.That(offsets["dekaf-in-memory-share"].Offsets[new TopicPartition("jobs", 0)].StartOffset).IsEqualTo(2);
        await producer.ProduceAsync("jobs", "key", "after-poll");
        await using var second = new InMemoryShareConsumer<string, string>(cluster);
        second.Subscribe("jobs");
        var record = await second.PollAsync().FirstAsync();
        await Assert.That(record.Value).IsEqualTo("after-poll");
        second.Acknowledge(record, AcknowledgeType.Release);
        await second.CommitAsync();
        await Assert.That((await first.PollAsync().FirstAsync()).Value).IsEqualTo("after-poll");
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false)]
    [Arguments(AutoOffsetReset.Earliest, true)]
    [Arguments(AutoOffsetReset.Latest, false)]
    [Arguments(AutoOffsetReset.Latest, true)]
    [Arguments(AutoOffsetReset.ByDuration, false)]
    [Arguments(AutoOffsetReset.ByDuration, true)]
    public async Task ExplicitReset_IsSharedWithMembersThatPreserveGroupPolicy(AutoOffsetReset policy, bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "old",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "recent",
            Timestamp = DateTimeOffset.UtcNow.AddHours(1)
        });
        await using var configured = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.FromHours(1) : null
        });
        configured.Subscribe("jobs");
        await using var member = new InMemoryShareConsumer<string, string>(cluster);
        member.Subscribe("jobs");
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "jobs", 0, "dekaf-in-memory-share")).Release();

        var records = await member.PollAsync().ToListAsync();
        var expected = policy switch
        {
            AutoOffsetReset.Earliest => new[] { "old", "recent" },
            AutoOffsetReset.ByDuration => new[] { "recent" },
            _ => Array.Empty<string>()
        };
        await Assert.That(records.Select(record => record.Value)).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false)]
    [Arguments(AutoOffsetReset.Earliest, true)]
    [Arguments(AutoOffsetReset.ByDuration, false)]
    [Arguments(AutoOffsetReset.ByDuration, true)]
    public async Task DeleteGroup_ClearsResetPolicy(AutoOffsetReset policy, bool pollBeforeDeletion)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "before",
            Timestamp = DateTimeOffset.UtcNow.AddHours(1)
        });
        await using (var configured = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            GroupId = "workers",
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.FromHours(1) : null
        }))
        {
            configured.Subscribe("jobs");
            if (pollBeforeDeletion)
                await Assert.That((await configured.PollAsync().ToListAsync()).Count).IsEqualTo(1);
        }

        await using var admin = new InMemoryAdminClient(cluster);
        var deleted = await admin.DeleteShareGroupsAsync(["workers"]);
        await Assert.That(deleted["workers"].ErrorCode).IsEqualTo(ErrorCode.None);
        await using var recreated = new InMemoryShareConsumer<string, string>(cluster, new() { GroupId = "workers" });
        recreated.Subscribe("jobs");
        await Assert.That(await recreated.PollAsync().ToListAsync()).IsEmpty();
        var offsets = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["workers"] = new() });
        await Assert.That(offsets["workers"].Offsets[new TopicPartition("jobs", 0)].StartOffset).IsEqualTo(1);
        await producer.ProduceAsync("jobs", "key", "after");
        await Assert.That((await recreated.PollAsync().FirstAsync()).Value).IsEqualTo("after");
    }

    [Test]
    public async Task DeleteActiveGroup_PreservesResetPolicy()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("jobs", "key", "before");
        await using var configured = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            GroupId = "workers",
            AutoOffsetReset = AutoOffsetReset.Earliest
        });
        configured.Subscribe("jobs");
        await using var admin = new InMemoryAdminClient(cluster);
        var deleted = await admin.DeleteShareGroupsAsync(["workers"]);
        await Assert.That(deleted["workers"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await using var member = new InMemoryShareConsumer<string, string>(cluster, new() { GroupId = "workers" });
        member.Subscribe("jobs");
        await Assert.That((await member.PollAsync().FirstAsync()).Value).IsEqualTo("before");
    }

    [Test]
    public async Task ByDuration_NoMatchingTimestampPersistsHighWatermark()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "old",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            AutoOffsetReset = AutoOffsetReset.ByDuration,
            AutoOffsetResetDuration = TimeSpan.FromHours(1)
        });
        consumer.Subscribe("jobs");
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await producer.ProduceAsync("jobs", "key", "new");
        await Assert.That((await consumer.PollAsync().FirstAsync()).Value).IsEqualTo("new");
    }

    [Test]
    [Arguments(null, false, false)]
    [Arguments(null, true, false)]
    [Arguments(AutoOffsetReset.Latest, false, false)]
    [Arguments(AutoOffsetReset.Latest, true, false)]
    [Arguments(AutoOffsetReset.ByDuration, false, false)]
    [Arguments(AutoOffsetReset.ByDuration, true, false)]
    [Arguments(AutoOffsetReset.ByDuration, false, true)]
    [Arguments(AutoOffsetReset.ByDuration, true, true)]
    public async Task ReadCommittedReset_PreservesRecordsBeyondStableOffset(
        AutoOffsetReset? policy, bool withFault, bool matchingTimestamp)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var group = new ConfigResource { Type = ConfigResourceType.Group, Name = "workers" };
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [group] = [ConfigAlter.Set("share.isolation.level", "read_committed")]
        });
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-2);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "before",
            Timestamp = oldTimestamp
        });
        await using var transactionalProducer = new InMemoryProducer<string, string>(cluster);
        await transactionalProducer.InitTransactionsAsync();
        await using var transaction = transactionalProducer.BeginTransaction();
        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "transactional",
            Timestamp = oldTimestamp
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "ordinary",
            Timestamp = matchingTimestamp ? DateTimeOffset.UtcNow.AddHours(1) : oldTimestamp
        });
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            GroupId = "workers",
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.FromHours(1) : null
        });
        consumer.Subscribe("jobs");
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "jobs", 0, "workers")).Release();

        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        var offsets = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["workers"] = new() });
        await Assert.That(offsets["workers"].Offsets[new TopicPartition("jobs", 0)].StartOffset).IsEqualTo(1);
        await transaction.CommitAsync();
        var records = await consumer.PollAsync().ToListAsync();
        await Assert.That(records.Select(record => record.Value)).IsEquivalentTo(["transactional", "ordinary"]);
    }

    [Test]
    public async Task ResetChange_AppliesOnlyToUninitializedPartitions()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("jobs", "key", "old");
        await producer.ProduceAsync("other", "key", "other");
        await using var first = new InMemoryShareConsumer<string, string>(cluster);
        first.Subscribe("jobs");
        await Assert.That(await first.PollAsync().ToListAsync()).IsEmpty();
        await using var changed = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            AutoOffsetReset = AutoOffsetReset.Earliest
        });
        changed.Subscribe("jobs", "other");
        var records = await changed.PollAsync().ToListAsync();
        await Assert.That(records.Count).IsEqualTo(1);
        await Assert.That(records[0].Value).IsEqualTo("other");
        await producer.ProduceAsync("jobs", "key", "new");
        await Assert.That((await first.PollAsync().FirstAsync()).Value).IsEqualTo("new");
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest)]
    [Arguments(AutoOffsetReset.Latest)]
    [Arguments(AutoOffsetReset.ByDuration)]
    public async Task ExistingOffset_WinsOverReset(AutoOffsetReset policy)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("jobs", "key", "skip");
        await producer.ProduceAsync("jobs", "key", "keep");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterShareGroupOffsetsAsync("workers", [new() { TopicPartition = new("jobs", 0), StartOffset = 1 }]);
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new()
        {
            GroupId = "workers",
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.Zero : null
        });
        consumer.Subscribe("jobs");
        await Assert.That((await consumer.PollAsync().FirstAsync()).Value).IsEqualTo("keep");
    }

    [Test]
    [Arguments(null, 1)]
    [Arguments(AutoOffsetReset.Earliest, 1)]
    [Arguments(AutoOffsetReset.Latest, 1)]
    [Arguments(AutoOffsetReset.ByDuration, null)]
    [Arguments(AutoOffsetReset.ByDuration, -1)]
    [Arguments(AutoOffsetReset.None, null)]
    [Arguments((AutoOffsetReset)123, null)]
    public async Task InvalidReset_IsRejected(AutoOffsetReset? policy, int? seconds)
    {
        await Assert.That(() => new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster(), new()
        {
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = seconds is { } value ? TimeSpan.FromSeconds(value) : null
        })).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(false, AutoOffsetReset.Earliest)]
    [Arguments(true, AutoOffsetReset.Earliest)]
    [Arguments(false, AutoOffsetReset.Latest)]
    [Arguments(true, AutoOffsetReset.Latest)]
    [Arguments(false, AutoOffsetReset.ByDuration)]
    [Arguments(true, AutoOffsetReset.ByDuration)]
    public async Task DependencyInjection_PreservesBuilderPolicy(bool keyed, AutoOffsetReset policy)
    {
        var services = new ServiceCollection();
        services.AddDekaf(dekaf =>
        {
            void Configure(ShareConsumerBuilder<string, string> builder)
            {
                builder.WithBootstrapServers("unused:9092").WithGroupId("workers")
                    .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
                if (policy == AutoOffsetReset.ByDuration)
                    builder.WithAutoOffsetResetByDuration(TimeSpan.FromHours(1));
                else
                    builder.WithAutoOffsetReset(policy);
            }
            if (keyed)
                dekaf.AddShareConsumer<string, string>("worker", Configure);
            else
                dekaf.AddShareConsumer<string, string>(Configure);
        });
        services.AddSingleton(new InMemoryShareConsumerOptions { MemberId = "configured-member" });
        services.AddDekafInMemory();
        await using var provider = services.BuildServiceProvider();
        var cluster = provider.GetRequiredService<InMemoryKafkaCluster>();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "old",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "jobs",
            Key = "key",
            Value = "recent",
            Timestamp = DateTimeOffset.UtcNow.AddHours(1)
        });
        var consumer = keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("worker")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>();
        consumer.Subscribe("jobs");
        await Assert.That(consumer.MemberId).IsEqualTo("configured-member");
        var records = await consumer.PollAsync().ToListAsync();
        await Assert.That(records.Count).IsEqualTo(policy == AutoOffsetReset.Earliest ? 2 : policy == AutoOffsetReset.ByDuration ? 1 : 0);
        if (policy == AutoOffsetReset.ByDuration)
            await Assert.That(records[0].Value).IsEqualTo("recent");
        if (records.Count > 0)
        {
            await consumer.CommitAsync();
            await using var peer = new InMemoryShareConsumer<string, string>(cluster, new() { GroupId = "workers" });
            peer.Subscribe("jobs");
            await Assert.That(await peer.PollAsync().ToListAsync()).IsEmpty();
            consumer.Acknowledge(records[0], AcknowledgeType.Renew);
            await consumer.CommitAsync();
        }
        await using var admin = new InMemoryAdminClient(cluster);
        var offsets = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec> { ["workers"] = new() });
        await Assert.That(offsets["workers"].Offsets.ContainsKey(new("jobs", 0))).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostedRegistration_PreservesResetPolicyAndExplicitAcknowledgements(bool keyed)
    {
        var services = new ServiceCollection();
        services.AddDekaf(dekaf =>
        {
            if (keyed)
                dekaf.AddShareConsumerService<ResetWorker, string, string>("worker", builder => builder
                    .WithBootstrapServers("unused:9092").WithGroupId("workers").WithAutoOffsetReset(AutoOffsetReset.Earliest));
            else
                dekaf.AddShareConsumerService<ResetWorker, string, string>(builder => builder
                    .WithBootstrapServers("unused:9092").WithGroupId("workers").WithAutoOffsetReset(AutoOffsetReset.Earliest));
        });
        services.AddSingleton(new InMemoryShareConsumerOptions { MemberId = "configured-member" });
        services.AddDekafInMemory();
        await using var provider = services.BuildServiceProvider();
        var cluster = provider.GetRequiredService<InMemoryKafkaCluster>();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("jobs", "key", "before-poll");
        var consumer = provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(keyed ? "worker" : null);
        consumer.Subscribe("jobs");
        await Assert.That(consumer.MemberId).IsEqualTo("configured-member");
        var record = await consumer.PollAsync().FirstAsync();
        await Assert.That(record.Value).IsEqualTo("before-poll");
        consumer.Acknowledge(record, AcknowledgeType.Renew);
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().FirstAsync()).IsSameReferenceAs(record);
    }

    private sealed class ResetWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        protected override IEnumerable<string> Topics => ["jobs"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
