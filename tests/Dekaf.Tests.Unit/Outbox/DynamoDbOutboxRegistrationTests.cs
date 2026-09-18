using Amazon.DynamoDBv2;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class DynamoDbOutboxRegistrationTests
{
    private static readonly DynamoDbOutboxOptions Options = new() { TableName = "outbox" };

    [Test]
    public async Task Registration_ResolvesStoreAndWriter_FromTheContainersClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAmazonDynamoDB>());
        services.AddDekafDynamoDbOutboxStore(Options);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IOutboxStore>();

        await Assert.That(store).IsTypeOf<DynamoDbOutboxStore>();
        // The relay detects these capabilities on the registered store.
        await Assert.That(store is IOutboxLeaseRenewalStore and IOutboxLeaseOwnershipStore and IOutboxMetricsStore).IsTrue();
        await Assert.That(provider.GetRequiredService<IDynamoDbOutboxWriter>()).IsTypeOf<DynamoDbOutboxWriter>();
    }

    [Test]
    public async Task ClientFactory_ReplacesTheContainersClient_AndRunsOncePerContainer()
    {
        var created = 0;
        var services = new ServiceCollection();
        services.AddDekafDynamoDbOutboxStore(Options, _ =>
        {
            created++;
            return Substitute.For<IAmazonDynamoDB>();
        });
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDynamoDbOutboxWriter>();
        provider.GetRequiredService<IOutboxStore>();

        // A factory that builds a client must not leave the store and the writer with one each.
        await Assert.That(created).IsEqualTo(1);
    }

    [Test]
    public async Task MissingClient_FailsWhenTheStoreIsResolved_NamingTheClient()
    {
        var services = new ServiceCollection();
        services.AddDekafDynamoDbOutboxStore(Options);
        await using var provider = services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredService<IOutboxStore>()).Throws<InvalidOperationException>()
            .WithMessageContaining(nameof(IAmazonDynamoDB));
    }

    [Test]
    public async Task ExistingStoreRegistration_IsPreserved()
    {
        var existing = Substitute.For<IOutboxStore>();
        var services = new ServiceCollection();
        services.AddSingleton(existing);
        services.AddSingleton(Substitute.For<IAmazonDynamoDB>());
        services.AddDekafDynamoDbOutboxStore(Options);
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IOutboxStore>()).IsSameReferenceAs(existing);
    }

    [Test]
    public async Task InvalidOptions_FailAtRegistration()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddDekafDynamoDbOutboxStore(new DynamoDbOutboxOptions { TableName = " " }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task WriterInAProcessWithARelay_WakesThatRelay()
    {
        var notifier = Substitute.For<IOutboxBucketNotifier>();
        var services = new ServiceCollection();
        services.AddSingleton<IOutboxNotifier>(notifier);
        services.AddSingleton(Substitute.For<IAmazonDynamoDB>());
        services.AddDekafDynamoDbOutboxStore(Options);
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDynamoDbOutboxWriter>().NotifyCommitted(new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            Bucket = 6,
            Topic = "orders",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        notifier.Received(1).NotifyCommitted(6);
    }
}
