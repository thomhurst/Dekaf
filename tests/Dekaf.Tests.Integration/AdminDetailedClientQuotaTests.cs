using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public class AdminDetailedClientQuotaTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task QuotaAlterations_PreserveMixedOutcomesValidateOnlyAndDefaultComponents()
    {
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var user = $"detailed-quota-{Guid.NewGuid():N}";
        var entity = ClientQuotaEntity.For(ClientQuotaEntityComponent.User(user), ClientQuotaEntityComponent.ClientId(null));
        var rejected = ClientQuotaEntity.ForUser($"invalid-{Guid.NewGuid():N}");
        var filter = new ClientQuotaFilter { Components = [ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.User, user)], Strict = false };
        ClientQuotaAlteration[] alterations = [ClientQuotaAlteration.Set(entity, "consumer_byte_rate", 4096),
            ClientQuotaAlteration.Set(rejected, "not_a_quota_key", 1024)];
        try
        {
            var validation = await admin.AlterClientQuotasDetailedAsync(alterations, new() { ValidateOnly = true });
            await Assert.That(validation[entity].IsSuccess).IsTrue();
            await Assert.That(validation[rejected].ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
            await Assert.That(validation[rejected].ErrorMessage).IsNotNull();
            await Assert.That((await admin.DescribeClientQuotasAsync(filter)).Count).IsEqualTo(0);
            var applied = await admin.AlterClientQuotasDetailedAsync(alterations);
            await Assert.That(applied[entity].IsSuccess).IsTrue();
            await Assert.That(applied[rejected].ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
            await WaitForConditionAsync(async () => await admin.DescribeClientQuotasAsync(filter),
                values => values.TryGetValue(entity, out var quotas) && quotas.TryGetValue("consumer_byte_rate", out var value) && value == 4096,
                description: "detailed quota mutation visible in metadata");
            var removed = await admin.AlterClientQuotasDetailedAsync([ClientQuotaAlteration.Remove(entity, "consumer_byte_rate")]);
            await Assert.That(removed[entity].IsSuccess).IsTrue();
            await WaitForConditionAsync(async () => await admin.DescribeClientQuotasAsync(filter), values => values.Count == 0,
                description: "detailed quota removal visible in metadata");
        }
        finally
        {
            await admin.AlterClientQuotasDetailedAsync([ClientQuotaAlteration.Remove(entity, "consumer_byte_rate")]);
        }
    }
}
