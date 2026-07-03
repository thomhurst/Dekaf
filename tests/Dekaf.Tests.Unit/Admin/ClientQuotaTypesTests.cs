using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Admin;

public sealed class ClientQuotaTypesTests
{
    [Test]
    public async Task ClientQuotaEntityType_HasCorrectValues()
    {
        var unknown = (int)ClientQuotaEntityType.Unknown;
        var user = (int)ClientQuotaEntityType.User;
        var clientId = (int)ClientQuotaEntityType.ClientId;
        var ip = (int)ClientQuotaEntityType.Ip;

        await Assert.That(unknown).IsEqualTo(0);
        await Assert.That(user).IsEqualTo(1);
        await Assert.That(clientId).IsEqualTo(2);
        await Assert.That(ip).IsEqualTo(3);
    }

    [Test]
    public async Task ClientQuotaMatchType_HasCorrectValues()
    {
        var exact = (sbyte)ClientQuotaMatchType.Exact;
        var defaultMatch = (sbyte)ClientQuotaMatchType.Default;
        var anySpecified = (sbyte)ClientQuotaMatchType.AnySpecified;

        await Assert.That(exact).IsEqualTo((sbyte)0);
        await Assert.That(defaultMatch).IsEqualTo((sbyte)1);
        await Assert.That(anySpecified).IsEqualTo((sbyte)2);
    }

    [Test]
    public async Task ClientQuotaFilter_All_MatchesAllEntities()
    {
        var filter = ClientQuotaFilter.All();

        await Assert.That(filter.Components).IsEmpty();
        await Assert.That(filter.Strict).IsFalse();
    }

    [Test]
    public async Task ClientQuotaFilterComponent_Factories_CreateExpectedComponents()
    {
        var exact = ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.User, "alice");
        var defaultUser = ClientQuotaFilterComponent.Default(ClientQuotaEntityType.User);
        var anyClientId = ClientQuotaFilterComponent.AnySpecified(ClientQuotaEntityType.ClientId);

        await Assert.That(exact.EntityType).IsEqualTo(ClientQuotaEntityType.User);
        await Assert.That(exact.MatchType).IsEqualTo(ClientQuotaMatchType.Exact);
        await Assert.That(exact.Match).IsEqualTo("alice");
        await Assert.That(defaultUser.MatchType).IsEqualTo(ClientQuotaMatchType.Default);
        await Assert.That(defaultUser.Match).IsNull();
        await Assert.That(anyClientId.MatchType).IsEqualTo(ClientQuotaMatchType.AnySpecified);
    }

    [Test]
    public async Task ClientQuotaEntityComponent_Factories_CreateExpectedComponents()
    {
        var user = ClientQuotaEntityComponent.User("alice");
        var clientId = ClientQuotaEntityComponent.ClientId(null);
        var ip = ClientQuotaEntityComponent.Ip("127.0.0.1");

        await Assert.That(user.EntityType).IsEqualTo(ClientQuotaEntityType.User);
        await Assert.That(user.Name).IsEqualTo("alice");
        await Assert.That(clientId.EntityType).IsEqualTo(ClientQuotaEntityType.ClientId);
        await Assert.That(clientId.Name).IsNull();
        await Assert.That(ip.EntityType).IsEqualTo(ClientQuotaEntityType.Ip);
    }

    [Test]
    public async Task ClientQuotaEntity_Equality_IsOrderInsensitive()
    {
        var first = ClientQuotaEntity.For(
            ClientQuotaEntityComponent.User("alice"),
            ClientQuotaEntityComponent.ClientId(null));
        var second = ClientQuotaEntity.For(
            ClientQuotaEntityComponent.ClientId(null),
            ClientQuotaEntityComponent.User("alice"));

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }

    [Test]
    public async Task ClientQuotaEntity_CanBeUsedAsDictionaryKey()
    {
        var dictionary = new Dictionary<ClientQuotaEntity, string>
        {
            [ClientQuotaEntity.For(
                ClientQuotaEntityComponent.User("alice"),
                ClientQuotaEntityComponent.ClientId("orders"))] = "value"
        };

        var key = ClientQuotaEntity.For(
            ClientQuotaEntityComponent.ClientId("orders"),
            ClientQuotaEntityComponent.User("alice"));

        await Assert.That(dictionary[key]).IsEqualTo("value");
    }

    [Test]
    public async Task ClientQuotaOperation_Factories_CreateExpectedOperations()
    {
        var set = ClientQuotaOperation.Set("consumer_byte_rate", 1024);
        var remove = ClientQuotaOperation.RemoveValue("producer_byte_rate");

        await Assert.That(set.Key).IsEqualTo("consumer_byte_rate");
        await Assert.That(set.Value).IsEqualTo(1024);
        await Assert.That(set.Remove).IsFalse();
        await Assert.That(remove.Key).IsEqualTo("producer_byte_rate");
        await Assert.That(remove.Remove).IsTrue();
    }

    [Test]
    public async Task ClientQuotaAlteration_Factories_CreateExpectedAlterations()
    {
        var entity = ClientQuotaEntity.ForUser("alice");
        var set = ClientQuotaAlteration.Set(entity, "consumer_byte_rate", 1024);
        var remove = ClientQuotaAlteration.Remove(entity, "producer_byte_rate");

        await Assert.That(set.Entity).IsEqualTo(entity);
        await Assert.That(set.Operations.Count).IsEqualTo(1);
        await Assert.That(set.Operations[0].Remove).IsFalse();
        await Assert.That(remove.Operations[0].Remove).IsTrue();
    }

    [Test]
    public async Task Options_DefaultValues_AreCorrect()
    {
        var describeOptions = new DescribeClientQuotasOptions();
        var alterOptions = new AlterClientQuotasOptions();

        await Assert.That(describeOptions.TimeoutMs).IsEqualTo(30000);
        await Assert.That(alterOptions.TimeoutMs).IsEqualTo(30000);
        await Assert.That(alterOptions.ValidateOnly).IsFalse();
    }
}
