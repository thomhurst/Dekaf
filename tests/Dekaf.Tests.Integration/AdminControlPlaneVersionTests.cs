using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public sealed class AdminControlPlaneVersionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    // ListConfigResources v1 (typed resource filtering) only exists from Kafka 4.1;
    // 4.0 brokers expose ListClientMetricsResources (API key 74) at v0 only.
    [Test]
    [SupportsKafka(410)]
    public async Task ListConfigResourcesAsync_V1Broker_ReturnsTypedBrokerResources()
    {
        await using var admin = KafkaContainer.CreateAdminClient();

        var resources = await admin.ListConfigResourcesAsync(new ListConfigResourcesOptions
        {
            ResourceTypes = [ConfigResourceType.Broker]
        });

        await Assert.That(resources).IsNotEmpty();
        await Assert.That(resources.All(static resource => resource.Type == ConfigResourceType.Broker)).IsTrue();

        var clientMetricsResources = await admin.ListClientMetricsResourcesAsync();
        await Assert.That(clientMetricsResources).IsNotNull();
    }

    [Test]
    public async Task DescribeConsumerGroupsAsync_V6MissingGroup_SurfacesGroupIdNotFound()
    {
        await using var admin = KafkaContainer.CreateAdminClient();
        var missingGroupId = $"missing-group-{Guid.NewGuid():N}";

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DescribeConsumerGroupsAsync([missingGroupId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(exception.GroupId).IsEqualTo(missingGroupId);
    }
}
