using Amazon.DynamoDBv2;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Dekaf.Tests.Unit.Outbox;

/// <summary>
/// The outbox registers inside <c>AddDekaf</c>. Each builder extension must register exactly
/// what its <see cref="IServiceCollection"/> counterpart registers, so the two styles cannot
/// drift apart.
/// </summary>
public sealed class DekafBuilderOutboxExtensionsTests
{
    private static readonly DynamoDbOutboxOptions DynamoDbOptions = new() { TableName = "outbox" };

    [Test]
    public async Task OutboxCalls_ChainWithTheRestOfTheBuilder()
    {
        var services = new ServiceCollection();
        DekafBuilder? first = null;
        DekafBuilder? last = null;

        services.AddDekaf(dekaf =>
        {
            first = dekaf;
            last = dekaf
                .AddProducer<string, string>(producer => producer.WithBootstrapServers("localhost:9092"))
                .AddDynamoDbOutboxStore(DynamoDbOptions)
                .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092"))
                .AddOutboxNotificationTransport<Transport>()
                .AddConsumer<string, string>(consumer => consumer
                    .WithBootstrapServers("localhost:9092").WithGroupId("orders"));
        });

        await Assert.That(last).IsSameReferenceAs(first);
    }

    [Test]
    public async Task AddOutboxRelay_RegistersWhatTheServiceCollectionCallRegisters()
    {
        var viaBuilder = new ServiceCollection();
        viaBuilder.AddDekaf(dekaf => dekaf
            .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092"))
            .AddOutboxNotificationTransport<Transport>());
        var direct = new ServiceCollection();
        direct.AddDekafOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092"));
        direct.AddDekafOutboxNotificationTransport<Transport>();

        await Assert.That(Describe(viaBuilder)).IsEqualTo(Describe(direct));
    }

    [Test]
    public async Task AddOutboxRelay_WithACustomPublisher_UsesItAndTheGivenOptions()
    {
        var publisher = Substitute.For<IOutboxPublisher>();
        var options = new OutboxRelayOptions { BucketCount = 32 };
        var services = new ServiceCollection();
        services.AddSingleton(publisher);
        services.AddDekaf(dekaf => dekaf.AddOutboxRelay(options));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IOutboxPublisher>()).IsSameReferenceAs(publisher);
        await Assert.That(provider.GetRequiredService<OutboxRelayOptions>()).IsSameReferenceAs(options);
    }

    [Test]
    public async Task Relay_StartsAfterDekafHasInitializedItsClients()
    {
        var services = new ServiceCollection();
        services.AddDekaf(dekaf => dekaf
            .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));

        // Hosted services start in registration order.
        var hosted = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType?.Name)
            .ToArray();

        await Assert.That(string.Join(',', hosted)).IsEqualTo("DekafInitializationService,OutboxRelayService");
    }

    [Test]
    public async Task AddDynamoDbOutboxStore_ResolvesTheStoreAndTheWriter_WithTheRelaysNotifier()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAmazonDynamoDB>());
        services.AddDekaf(dekaf => dekaf
            .AddDynamoDbOutboxStore(DynamoDbOptions)
            .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IOutboxStore>()).IsTypeOf<DynamoDbOutboxStore>();
        await Assert.That(provider.GetRequiredService<IDynamoDbOutboxWriter>()).IsTypeOf<DynamoDbOutboxWriter>();
        await Assert.That(provider.GetRequiredService<IOutboxNotifier>()).IsNotNull();
    }

    [Test]
    public async Task AddDynamoDbOutboxStore_PassesTheClientFactoryOn()
    {
        var outboxClient = Substitute.For<IAmazonDynamoDB>();
        var services = new ServiceCollection();
        services.AddDekaf(dekaf => dekaf.AddDynamoDbOutboxStore(DynamoDbOptions, _ => outboxClient));
        await using var provider = services.BuildServiceProvider();

        // No IAmazonDynamoDB is registered: only the factory can have supplied the client.
        await Assert.That(provider.GetRequiredService<IOutboxStore>()).IsTypeOf<DynamoDbOutboxStore>();
    }

    [Test]
    public async Task AddEntityFrameworkCoreOutboxStore_RegistersWhatTheServiceCollectionCallsRegister()
    {
        var viaBuilder = new ServiceCollection();
        viaBuilder.AddDekaf(dekaf => dekaf
            .AddEntityFrameworkCoreOutboxStore<Context>((_, options) => options.UseInMemoryDatabase("outbox"))
            .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));
        var direct = new ServiceCollection();
        direct.AddDekafEntityFrameworkCoreOutboxStore<Context>((_, options) => options.UseInMemoryDatabase("outbox"));
        direct.AddDekafOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092"));

        await Assert.That(Describe(viaBuilder)).IsEqualTo(Describe(direct));

        var existingFactory = new ServiceCollection();
        existingFactory.AddDekaf(dekaf => dekaf.AddEntityFrameworkCoreOutboxStore<Context>());
        await Assert.That(existingFactory.Single(descriptor => descriptor.ServiceType == typeof(IOutboxStore))
            .ImplementationType).IsEqualTo(typeof(EfCoreOutboxStore<Context>));
        await Assert.That(existingFactory.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<Context>)))
            .IsFalse();
    }

    [Test]
    public async Task NullBuilder_IsRejected()
    {
        DekafBuilder builder = null!;

        await Assert.That(() => builder.AddOutboxRelay()).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddOutboxRelay(_ => { })).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddOutboxNotificationTransport<Transport>()).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddDynamoDbOutboxStore(DynamoDbOptions)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddEntityFrameworkCoreOutboxStore<Context>()).Throws<ArgumentNullException>();
    }

    /// <summary>The registrations, without the initialization service that only AddDekaf adds.</summary>
    private static string Describe(IServiceCollection services) => string.Join('\n', services
        .Where(descriptor => descriptor.ImplementationType?.Name != "DekafInitializationService")
        .Select(descriptor =>
            $"{descriptor.Lifetime} {descriptor.ServiceType} <- {descriptor.ImplementationType?.ToString() ?? "factory"}"));

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    private sealed class Transport : IOutboxNotificationTransport
    {
        public ValueTask PublishAsync(ReadOnlyMemory<int> buckets, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public Task ListenAsync(Action<int> notifyCommitted, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
