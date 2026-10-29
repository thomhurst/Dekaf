using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConfigTests
{
    [Test]
    public async Task DescribeConfigs_ReturnsStoredTopicConfigsAndIndependentSnapshots()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.CreateTopicsAsync([new()
        {
            Name = "orders",
            Configs = new Dictionary<string, string> { ["retention.ms"] = "1000", ["cleanup.policy"] = "delete" }
        }]);
        cluster.CreateTopic("empty");
        var resource = ConfigResource.Topic("orders");

        var result = await admin.DescribeConfigsAsync([resource, ConfigResource.Topic("empty")]);

        await Assert.That(result[resource].Count).IsEqualTo(2);
        var retention = result[resource].Single(entry => entry.Name == "retention.ms");
        await Assert.That(retention.Value).IsEqualTo("1000");
        await Assert.That(retention.Source).IsEqualTo(ConfigSource.DynamicTopicConfig);
        await Assert.That(retention.IsDefault).IsFalse();
        await Assert.That(retention.IsReadOnly).IsFalse();
        await Assert.That(result[ConfigResource.Topic("empty")]).IsEmpty();

        await admin.AlterConfigsAsync(Changes(resource, new ConfigEntry() { Name = "retention.ms", Value = "2000" }));

        await Assert.That(retention.Value).IsEqualTo("1000");
        await Assert.That((await admin.DescribeConfigsAsync([resource]))[resource].Single().Value).IsEqualTo("2000");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task AlterConfigs_ReplacesTopicOverridesAndHonorsValidateOnly(bool detailed, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", configs: new Dictionary<string, string>
        {
            ["retention.ms"] = "1000", ["cleanup.policy"] = "delete", ["segment.ms"] = "5000"
        });
        await using var admin = new InMemoryAdminClient(cluster);
        await using var otherAdmin = new InMemoryAdminClient(cluster);
        var resource = ConfigResource.Topic("orders");
        var changes = Changes(resource,
            new() { Name = "retention.ms", Value = "2000" },
            new() { Name = "segment.ms", Value = null });

        await AlterAsync(admin, changes, detailed, validateOnly);

        var configs = (await otherAdmin.DescribeConfigsAsync([resource]))[resource];
        await Assert.That(configs.Single(entry => entry.Name == "retention.ms").Value).IsEqualTo(validateOnly ? "1000" : "2000");
        await Assert.That(configs.Count).IsEqualTo(validateOnly ? 3 : 1);

        await AlterAsync(admin, Changes(resource), detailed, validateOnly);

        await Assert.That((await otherAdmin.DescribeConfigsAsync([resource]))[resource].Count).IsEqualTo(validateOnly ? 3 : 0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReplacementAndIncrementalChanges_ShareDescribedTopicOverrides(bool detailed)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        var resource = ConfigResource.Topic("orders");
        await AlterAsync(admin, Changes(resource,
            new() { Name = "retention.ms", Value = "1000" },
            new() { Name = "cleanup.policy", Value = "delete" }), detailed, validateOnly: false);
        var incremental = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [resource] = [ConfigAlter.Set("retention.ms", "2000"), ConfigAlter.Set("segment.ms", "5000")]
        };

        if (detailed)
            await Assert.That((await admin.IncrementalAlterConfigsDetailedAsync(incremental))[resource].IsSuccess).IsTrue();
        else
            await admin.IncrementalAlterConfigsAsync(incremental);

        var configs = (await admin.DescribeConfigsAsync([resource]))[resource];
        await Assert.That(configs.Count).IsEqualTo(3);
        await Assert.That(configs.Single(entry => entry.Name == "retention.ms").Value).IsEqualTo("2000");
        await Assert.That(configs.Single(entry => entry.Name == "segment.ms").Value).IsEqualTo("5000");
        await Assert.That(configs.Single(entry => entry.Name == "cleanup.policy").Value).IsEqualTo("delete");

        await AlterAsync(admin, Changes(resource, new ConfigEntry() { Name = "retention.ms", Value = "3000" }),
            detailed, validateOnly: false);

        await Assert.That((await admin.DescribeConfigsAsync([resource]))[resource].Single().Value).IsEqualTo("3000");
    }

    [Test]
    public async Task DescribeConfigs_MissingTopic_ThrowsWithoutCreatingTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        var exception = await Assert.That(async () => await admin.DescribeConfigsAsync([ConfigResource.Topic("missing")]))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task AlterConfigs_MissingTopic_ReportsErrorAndProcessesSiblings(bool detailed, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("before");
        cluster.CreateTopic("after");
        await using var admin = new InMemoryAdminClient(cluster);
        var before = ConfigResource.Topic("before");
        var missing = ConfigResource.Topic("missing");
        var after = ConfigResource.Topic("after");
        var changes = Changes(before, new ConfigEntry() { Name = "retention.ms", Value = "1000" });
        changes.Add(missing, []);
        changes.Add(after, [new() { Name = "retention.ms", Value = "2000" }]);

        if (detailed)
        {
            var results = await admin.AlterConfigsDetailedAsync(changes, new() { ValidateOnly = validateOnly });
            await Assert.That(results[before].IsSuccess).IsTrue();
            await Assert.That(results[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            await Assert.That(results[after].IsSuccess).IsTrue();
        }
        else
        {
            var exception = await Assert.That(async () => await admin.AlterConfigsAsync(changes, new() { ValidateOnly = validateOnly }))
                .Throws<KafkaException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            await Assert.That(exception.Message).Contains("missing");
        }

        var configs = await admin.DescribeConfigsAsync([before, after]);
        await Assert.That(configs[before].Count).IsEqualTo(validateOnly ? 0 : 1);
        await Assert.That(configs[after].Count).IsEqualTo(validateOnly ? 0 : 1);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["before", "after"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SimpleConfigs_ResourceFault_PreventsReadOrMutation(bool alter)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", configs: new Dictionary<string, string> { ["retention.ms"] = "1000" });
        await using var admin = new InMemoryAdminClient(cluster);
        var resource = ConfigResource.Topic("orders");
        var failure = new KafkaException(ErrorCode.TopicAuthorizationFailed, "denied");
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: resource.Name), failure);

        var exception = await Assert.That(async () => await InvokeSimpleAsync(admin, resource, alter))
            .Throws<KafkaException>();

        await Assert.That(exception).IsSameReferenceAs(failure);
        await Assert.That((await admin.DescribeConfigsAsync([resource]))[resource].Single().Value).IsEqualTo("1000");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SimpleConfigs_CancellationAtResourceBarrier_PreventsReadOrMutation(bool alter)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", configs: new Dictionary<string, string> { ["retention.ms"] = "1000" });
        await using var admin = new InMemoryAdminClient(cluster);
        var resource = ConfigResource.Topic("orders");
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: resource.Name));
        using var cancellation = new CancellationTokenSource();
        var pending = InvokeSimpleAsync(admin, resource, alter, cancellation.Token);
        try
        {
            await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
        }
        finally
        {
            barrier.Release();
        }

        await Assert.That((await admin.DescribeConfigsAsync([resource]))[resource].Single().Value).IsEqualTo("1000");
    }

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> Changes(ConfigResource resource, params ConfigEntry[] entries) =>
        new() { [resource] = entries };

    private static async Task AlterAsync(InMemoryAdminClient admin,
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> changes, bool detailed, bool validateOnly)
    {
        if (detailed)
        {
            var results = await admin.AlterConfigsDetailedAsync(changes, new() { ValidateOnly = validateOnly });
            await Assert.That(results.Values.All(result => result.IsSuccess)).IsTrue();
        }
        else
        {
            await admin.AlterConfigsAsync(changes, new() { ValidateOnly = validateOnly });
        }
    }

    private static async Task InvokeSimpleAsync(InMemoryAdminClient admin, ConfigResource resource, bool alter,
        CancellationToken cancellationToken = default)
    {
        if (alter)
            await admin.AlterConfigsAsync(Changes(resource, new ConfigEntry() { Name = "retention.ms", Value = "2000" }), cancellationToken: cancellationToken);
        else
            await admin.DescribeConfigsAsync([resource], cancellationToken: cancellationToken);
    }
}
