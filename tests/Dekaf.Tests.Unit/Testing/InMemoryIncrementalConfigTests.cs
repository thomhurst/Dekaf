using System.Collections;
using System.Reflection;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryIncrementalConfigTests
{
    private static readonly ConfigResource Orders = ConfigResource.Topic("orders");

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SetAndDelete_PreserveUnrelatedOverrides(bool detailed, bool validateOnly)
    {
        var cluster = CreateCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await MutateAsync(admin, detailed, Changes(Orders,
            ConfigAlter.Set("retention.ms", "2000"), ConfigAlter.Delete("segment.ms"),
            ConfigAlter.Set("max.message.bytes", "4096"), ConfigAlter.Delete("absent")), validateOnly);

        var configs = ReadConfigs(cluster);
        await Assert.That(configs["retention.ms"]).IsEqualTo(validateOnly ? "1000" : "2000");
        await Assert.That(configs["cleanup.policy"]).IsEqualTo("delete");
        await Assert.That(configs.ContainsKey("segment.ms")).IsEqualTo(validateOnly);
        await Assert.That(configs.ContainsKey("max.message.bytes")).IsEqualTo(!validateOnly);
        if (!validateOnly)
            await Assert.That(configs["max.message.bytes"]).IsEqualTo("4096");
        await Assert.That(configs.ContainsKey("absent")).IsFalse();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MissingTopic_IsRejectedWithoutCreatingTopic(bool detailed, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await AssertRejectedAsync(admin, detailed, Changes(Orders, ConfigAlter.Set("retention.ms", "2000")),
            validateOnly, ErrorCode.UnknownTopicOrPartition);
        await Assert.That(cluster.ListTopics()).IsEmpty();
        await AssertRejectedAsync(admin, detailed, Changes(Orders), validateOnly, ErrorCode.UnknownTopicOrPartition);
    }

    [Test]
    [Arguments(false, false, ConfigAlterOperation.Append)]
    [Arguments(false, true, ConfigAlterOperation.Append)]
    [Arguments(true, false, ConfigAlterOperation.Append)]
    [Arguments(true, true, ConfigAlterOperation.Append)]
    [Arguments(false, false, ConfigAlterOperation.Subtract)]
    [Arguments(false, true, ConfigAlterOperation.Subtract)]
    [Arguments(true, false, ConfigAlterOperation.Subtract)]
    [Arguments(true, true, ConfigAlterOperation.Subtract)]
    public async Task UnsupportedListOperation_RejectsWholeResource(bool detailed, bool validateOnly, ConfigAlterOperation operation)
    {
        var cluster = CreateCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await AssertRejectedAsync(admin, detailed, Changes(Orders,
            ConfigAlter.Set("retention.ms", "2000"),
            new() { Name = "cleanup.policy", Value = "compact", Operation = operation }), validateOnly, ErrorCode.InvalidConfig);
        await AssertOriginalConfigsAsync(cluster);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task NullSet_IsRejectedWithoutPartialMutation(bool detailed, bool validateOnly)
    {
        var cluster = CreateCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await AssertRejectedAsync(admin, detailed, Changes(Orders,
            ConfigAlter.Delete("segment.ms"), ConfigAlter.Set("retention.ms", null)), validateOnly, ErrorCode.InvalidConfig);
        await AssertOriginalConfigsAsync(cluster);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingResource_DoesNotPreventValidSiblingMutation(bool detailed)
    {
        var cluster = CreateCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var missing = ConfigResource.Topic("missing");
        var changes = Changes(missing, ConfigAlter.Set("retention.ms", "2000"));
        changes.Add(Orders, [ConfigAlter.Set("retention.ms", "3000")]);
        if (detailed)
        {
            var results = await admin.IncrementalAlterConfigsDetailedAsync(changes);
            await Assert.That(results[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            await Assert.That(results[Orders].IsSuccess).IsTrue();
        }
        else
        {
            var error = await Assert.ThrowsAsync<KafkaException>(() => admin.IncrementalAlterConfigsAsync(changes).AsTask());
            await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        }
        await Assert.That(ReadConfigs(cluster)["retention.ms"]).IsEqualTo("3000");
    }

    [Test]
    public async Task DetailedResourceFault_RejectsOnlyTargetAndPreservesStoredOverrides()
    {
        var cluster = CreateCluster();
        cluster.CreateTopic("other");
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: Orders.Name),
            new KafkaException(ErrorCode.TopicAuthorizationFailed, "denied"));
        var other = ConfigResource.Topic("other");
        var changes = Changes(Orders, ConfigAlter.Set("retention.ms", "2000"));
        changes.Add(other, [ConfigAlter.Set("retention.ms", "3000")]);
        var results = await admin.IncrementalAlterConfigsDetailedAsync(changes);
        await Assert.That(results[Orders].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results[Orders].ErrorMessage).IsEqualTo("denied");
        await Assert.That(results[other].IsSuccess).IsTrue();
        await AssertOriginalConfigsAsync(cluster);
        await Assert.That(ReadConfigs(cluster, other.Name)["retention.ms"]).IsEqualTo("3000");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancelledPausedResource_PreservesEarlierMutation(bool detailed)
    {
        var cluster = CreateCluster();
        cluster.CreateTopic("paused");
        await using var admin = new InMemoryAdminClient(cluster);
        using var cancellation = new CancellationTokenSource();
        var paused = ConfigResource.Topic("paused");
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: paused.Name));
        var changes = Changes(Orders, ConfigAlter.Set("retention.ms", "2000"));
        changes.Add(paused, [ConfigAlter.Set("retention.ms", "3000")]);
        var pending = detailed
            ? admin.IncrementalAlterConfigsDetailedAsync(changes, cancellationToken: cancellation.Token).AsTask()
            : CancelSimpleAsync();
        try
        {
            await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (detailed)
            {
                await Assert.That(results[Orders].IsSuccess).IsTrue();
                await Assert.That(results[paused].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            }
        }
        finally
        {
            barrier.Release();
        }
        await Assert.That(ReadConfigs(cluster)["retention.ms"]).IsEqualTo("2000");
        await Assert.That(ReadConfigs(cluster, paused.Name)).IsEmpty();

        async Task<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> CancelSimpleAsync()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                admin.IncrementalAlterConfigsAsync(changes, cancellationToken: cancellation.Token).AsTask());
            return new Dictionary<ConfigResource, AdminMutationResult>();
        }
    }

    private static InMemoryKafkaCluster CreateCluster()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Orders.Name, configs: new Dictionary<string, string>
        {
            ["retention.ms"] = "1000", ["segment.ms"] = "500", ["cleanup.policy"] = "delete"
        });
        return cluster;
    }

    // DescribeConfigs topic support is tracked separately in #3952. Inspect the actual shared store
    // so these tests remain independent of that API and detect unapplied successful mutations.
    private static Dictionary<string, string> ReadConfigs(InMemoryKafkaCluster cluster, string topic = "orders")
    {
        var topics = (IDictionary)typeof(InMemoryKafkaCluster)
            .GetField("_topics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cluster)!;
        var state = topics[topic]!;
        var configs = (Dictionary<string, string>)state.GetType().GetProperty("Configs")!.GetValue(state)!;
        return new(configs, StringComparer.Ordinal);
    }

    private static async Task AssertOriginalConfigsAsync(InMemoryKafkaCluster cluster)
    {
        var configs = ReadConfigs(cluster);
        await Assert.That(configs.Count).IsEqualTo(3);
        await Assert.That(configs["retention.ms"]).IsEqualTo("1000");
        await Assert.That(configs["segment.ms"]).IsEqualTo("500");
        await Assert.That(configs["cleanup.policy"]).IsEqualTo("delete");
    }

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> Changes(ConfigResource resource, params ConfigAlter[] changes) =>
        new() { [resource] = changes };

    private static async Task MutateAsync(InMemoryAdminClient admin, bool detailed,
        Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> changes, bool validateOnly)
    {
        if (detailed)
        {
            var results = await admin.IncrementalAlterConfigsDetailedAsync(changes, new() { ValidateOnly = validateOnly });
            foreach (var result in results.Values)
                await Assert.That(result.IsSuccess).IsTrue();
        }
        else
            await admin.IncrementalAlterConfigsAsync(changes, new() { ValidateOnly = validateOnly });
    }

    private static async Task AssertRejectedAsync(InMemoryAdminClient admin, bool detailed,
        Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> changes, bool validateOnly, ErrorCode expected)
    {
        if (detailed)
        {
            var results = await admin.IncrementalAlterConfigsDetailedAsync(changes, new() { ValidateOnly = validateOnly });
            await Assert.That(results[Orders].ErrorCode).IsEqualTo(expected);
        }
        else
        {
            var error = await Assert.ThrowsAsync<KafkaException>(() =>
                admin.IncrementalAlterConfigsAsync(changes, new() { ValidateOnly = validateOnly }).AsTask());
            await Assert.That(error!.ErrorCode).IsEqualTo(expected);
        }
    }
}
