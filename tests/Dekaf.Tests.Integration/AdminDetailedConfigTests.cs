using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public class AdminDetailedConfigTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MixedResources_PreserveValidationAndAppliedConfig(bool incremental)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var invalid = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var good = ConfigResource.Topic(topic);
        var bad = ConfigResource.Topic(invalid);
        var before = (await admin.DescribeConfigsAsync([good]))[good].Single(static entry => entry.Name == "retention.ms").Value;
        var value = before == "86400000" ? "172800000" : "86400000";
        var changes = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [good] = [ConfigAlter.Set("retention.ms", value)],
            [bad] = [ConfigAlter.Set("not_a_real_topic_config", "1")]
        };
        async ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> Apply(bool validateOnly) => incremental
            ? await admin.IncrementalAlterConfigsDetailedAsync(changes, new() { ValidateOnly = validateOnly })
            : await admin.AlterConfigsDetailedAsync(changes.ToDictionary(static pair => pair.Key,
                static pair => (IReadOnlyList<ConfigEntry>)pair.Value.Select(static entry => new ConfigEntry { Name = entry.Name, Value = entry.Value }).ToArray()),
                new() { ValidateOnly = validateOnly });
        var validated = await Apply(true);
        await Assert.That(validated[good].IsSuccess).IsTrue();
        await Assert.That(validated[bad].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await Assert.That(validated[bad].ErrorMessage).IsNotNull();
        await Assert.That((await admin.DescribeConfigsAsync([good]))[good].Single(static entry => entry.Name == "retention.ms").Value).IsEqualTo(before);
        var applied = await Apply(false);
        await Assert.That(applied[good].IsSuccess).IsTrue();
        await Assert.That(applied[bad].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await WaitForConditionAsync(async () => (await admin.DescribeConfigsAsync([good]))[good],
            entries => entries.Any(entry => entry.Name == "retention.ms" && entry.Value == value), description: "detailed config visible in metadata");
    }
}
