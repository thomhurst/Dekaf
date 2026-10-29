using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareIsolationTests
{
    private const string IsolationConfig = "share.isolation.level";
    private static readonly ConfigResource Workers = new() { Type = ConfigResourceType.Group, Name = "workers" };

    [Test]
    [Arguments(null, false)]
    [Arguments(null, true)]
    [Arguments("read_uncommitted", false)]
    [Arguments("read_uncommitted", true)]
    [Arguments("read_committed", false)]
    [Arguments("read_committed", true)]
    public async Task AbortedTransaction_UsesGroupIsolation(string? isolation, bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await ConfigureAsync(cluster, isolation);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync("orders", "key", "aborted");
            await transaction.AbortAsync();
        }
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        await ordinaryProducer.ProduceAsync("orders", "key", "ordinary");
        EnableFaultPath(cluster, withFault);
        await using var consumer = CreateConsumer(cluster);

        var record = await consumer.PollAsync().FirstAsync();

        await Assert.That(record.Value).IsEqualTo(isolation == "read_committed" ? "ordinary" : "aborted");
    }

    [Test]
    [Arguments(null, false)]
    [Arguments(null, true)]
    [Arguments("read_uncommitted", false)]
    [Arguments("read_uncommitted", true)]
    [Arguments("read_committed", false)]
    [Arguments("read_committed", true)]
    public async Task OpenTransaction_UsesGroupIsolationUntilCommit(string? isolation, bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await ConfigureAsync(cluster, isolation);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync("orders", "key", "transactional");
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        await ordinaryProducer.ProduceAsync("orders", "key", "ordinary");
        EnableFaultPath(cluster, withFault);
        await using var consumer = CreateConsumer(cluster);

        var records = await consumer.PollAsync().ToListAsync();
        if (isolation == "read_committed")
        {
            await Assert.That(records).IsEmpty();
            await transaction.CommitAsync();
            records = await consumer.PollAsync().ToListAsync();
        }

        await Assert.That(records.Select(record => record.Value)).IsEquivalentTo(["transactional", "ordinary"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfigDeletion_ChangesActiveGroupWithoutAffectingOtherGroups(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await ConfigureAsync(cluster, "read_committed");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync("orders", "key", "pending");
        EnableFaultPath(cluster, withFault);
        await using var consumer = CreateConsumer(cluster);
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await using var admin = new InMemoryAdminClient(cluster);
        await MutateAsync(admin, detailed: false, incremental: true, value: null);

        await Assert.That((await consumer.PollAsync().FirstAsync()).Value).IsEqualTo("pending");
        await MutateAsync(admin, detailed: false, incremental: true, "read_committed");
        await using var other = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = "other" });
        other.Subscribe("orders");
        await Assert.That((await other.PollAsync().FirstAsync()).Value).IsEqualTo("pending");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task AdminMutation_StoresGroupConfigAndHonorsValidation(bool detailed, bool incremental)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await AssertIsolationAsync(admin, Workers, "read_uncommitted", isDefault: true);
        await MutateAsync(admin, detailed, incremental, "read_committed", validateOnly: true);
        await AssertIsolationAsync(admin, Workers, "read_uncommitted", isDefault: true);
        await MutateAsync(admin, detailed, incremental, "read_committed");

        await using var otherAdmin = new InMemoryAdminClient(cluster);
        await AssertIsolationAsync(otherAdmin, Workers, "read_committed", isDefault: false);
        await AssertIsolationAsync(otherAdmin, new() { Type = ConfigResourceType.Group, Name = "other" }, "read_uncommitted", isDefault: true);
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [Workers] = [ConfigAlter.Set("share.heartbeat.interval.ms", "5000")]
        });
        await AssertIsolationAsync(otherAdmin, Workers, "read_committed", isDefault: false);
        await MutateAsync(admin, detailed, incremental, "read_uncommitted");
        await AssertIsolationAsync(otherAdmin, Workers, "read_uncommitted", isDefault: false);
        await MutateAsync(admin, detailed, incremental, "read_committed");
        await MutateAsync(admin, detailed, incremental, null, validateOnly: true);
        await AssertIsolationAsync(otherAdmin, Workers, "read_committed", isDefault: false);
        await MutateAsync(admin, detailed, incremental, null);
        await AssertIsolationAsync(otherAdmin, Workers, "read_uncommitted", isDefault: true);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task InvalidIsolation_DoesNotChangeGroup(bool detailed, bool incremental)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await MutateAsync(admin, detailed, incremental, "read_committed");
        if (detailed)
        {
            var result = await MutateDetailedAsync(admin, incremental, "invalid", validateOnly: false);
            await Assert.That(result[Workers].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        }
        else
        {
            var error = await Assert.ThrowsAsync<KafkaException>(() => MutateAsync(admin, detailed, incremental, "invalid"));
            await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        }
        await AssertIsolationAsync(admin, Workers, "read_committed", isDefault: false);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MixedConfigMutation_AppliesValidGroupsBeforeThrowing(bool incremental, bool invalidFirst)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await MutateAsync(admin, detailed: false, incremental, "read_committed");
        var other = new ConfigResource { Type = ConfigResourceType.Group, Name = "other" };
        var resources = invalidFirst ? new[] { Workers, other } : new[] { other, Workers };

        var error = await Assert.ThrowsAsync<KafkaException>(async () =>
        {
            if (incremental)
            {
                var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>();
                foreach (var resource in resources)
                    configs.Add(resource, [ConfigAlter.Set(IsolationConfig, resource == Workers ? "invalid" : "read_committed")]);
                await admin.IncrementalAlterConfigsAsync(configs);
            }
            else
            {
                var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>();
                foreach (var resource in resources)
                    configs.Add(resource, [new() { Name = IsolationConfig, Value = resource == Workers ? "invalid" : "read_committed" }]);
                await admin.AlterConfigsAsync(configs);
            }
        });

        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await AssertIsolationAsync(admin, Workers, "read_committed", isDefault: false);
        await AssertIsolationAsync(admin, other, "read_committed", isDefault: false);
    }

    [Test]
    [Arguments(ConfigAlterOperation.Append)]
    [Arguments(ConfigAlterOperation.Subtract)]
    public async Task ListOperation_IsRejectedForScalarIsolation(ConfigAlterOperation operation)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var result = await admin.IncrementalAlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [Workers] = [new() { Name = IsolationConfig, Value = "read_committed", Operation = operation }]
        });
        await Assert.That(result[Workers].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await AssertIsolationAsync(admin, Workers, "read_uncommitted", isDefault: true);
    }

    [Test]
    public async Task CancelledConfigMutation_DoesNotChangeIsolation()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, groupId: Workers.Name));
        using var cancellation = new CancellationTokenSource();
        var mutation = admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [Workers] = [ConfigAlter.Set(IsolationConfig, "read_committed")]
        }, cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => mutation);
        barrier.Release();
        await AssertIsolationAsync(admin, Workers, "read_uncommitted", isDefault: true);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task UncommittedCompletion_PreservesNextTransactionalRecord(bool abort, bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync("orders", "key", "first");
        await transaction.ProduceAsync("orders", "key", "second");
        if (abort)
            await transaction.AbortAsync();
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        var ordinary = await ordinaryProducer.ProduceAsync("orders", "key", "ordinary");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = Workers.Name, MaxPollRecords = 1 });
        consumer.Subscribe("orders");
        EnableFaultPath(cluster, withFault);

        var first = await consumer.PollAsync().FirstAsync();
        await Assert.That(first.Offset).IsEqualTo(0);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset(Workers.Name, new("orders", 0))).IsEqualTo(1);

        var second = await consumer.PollAsync().FirstAsync();
        await Assert.That(second.Offset).IsEqualTo(1);
        await Assert.That(second.Value).IsEqualTo("second");
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset(Workers.Name, new("orders", 0))).IsEqualTo(ordinary.Offset);
        await Assert.That((await consumer.PollAsync().FirstAsync()).Value).IsEqualTo("ordinary");
    }

    private static async Task AssertIsolationAsync(InMemoryAdminClient admin, ConfigResource resource, string value, bool isDefault)
    {
        var config = (await admin.DescribeConfigsAsync([resource]))[resource].Single(entry => entry.Name == IsolationConfig);
        await Assert.That(config.Value).IsEqualTo(value);
        await Assert.That(config.IsDefault).IsEqualTo(isDefault);
    }

    private static async Task MutateAsync(InMemoryAdminClient admin, bool detailed, bool incremental, string? value, bool validateOnly = false)
    {
        if (detailed)
        {
            var result = await MutateDetailedAsync(admin, incremental, value, validateOnly);
            await Assert.That(result[Workers].IsSuccess).IsTrue();
        }
        else if (incremental)
        {
            await admin.IncrementalAlterConfigsAsync(IncrementalChanges(value), new() { ValidateOnly = validateOnly });
        }
        else
        {
            await admin.AlterConfigsAsync(ReplacementChanges(value), new() { ValidateOnly = validateOnly });
        }
    }

    private static ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> MutateDetailedAsync(
        InMemoryAdminClient admin, bool incremental, string? value, bool validateOnly) => incremental
        ? admin.IncrementalAlterConfigsDetailedAsync(IncrementalChanges(value), new() { ValidateOnly = validateOnly })
        : admin.AlterConfigsDetailedAsync(ReplacementChanges(value), new() { ValidateOnly = validateOnly });

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> IncrementalChanges(string? value) => new()
    {
        [Workers] = [value is null ? ConfigAlter.Delete(IsolationConfig) : ConfigAlter.Set(IsolationConfig, value)]
    };

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> ReplacementChanges(string? value) => new()
    {
        [Workers] = value is null ? [] : [new() { Name = IsolationConfig, Value = value }]
    };

    private static async Task ConfigureAsync(InMemoryKafkaCluster cluster, string? isolation)
    {
        if (isolation is null)
            return;
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [Workers] = [ConfigAlter.Set(IsolationConfig, isolation)]
        });
    }

    private static InMemoryShareConsumer<string, string> CreateConsumer(InMemoryKafkaCluster cluster)
    {
        var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = Workers.Name });
        consumer.Subscribe("orders");
        return consumer;
    }

    private static void EnableFaultPath(InMemoryKafkaCluster cluster, bool enabled)
    {
        if (!enabled)
            return;
        // A pre-released barrier exercises fault acquisition without a timing dependency.
        cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "orders", 0, Workers.Name)).Release();
    }
}
