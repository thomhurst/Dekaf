using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryServiceCollectionTests
{
    private static readonly string[] ClientKeys = ["orders", "invoices"];

    [Test]
    public async Task AddDekafInMemory_FallbackClientsAreSingletonsAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await AssertSingletonClients(provider, scope.ServiceProvider, serviceKey: null);
    }

    [Test]
    public async Task AddDekafInMemory_ReplacesRealKeyedClientsWithSeparateSingletons()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            foreach (var key in ClientKeys)
            {
                builder.AddProducer<string, string>(key, p => p.WithBootstrapServers("unused:9092"));
                builder.AddConsumer<string, string>(key, c => c.WithBootstrapServers("unused:9092").WithGroupId(key));
                builder.AddShareConsumer<string, string>(key, c => c.WithBootstrapServers("unused:9092").WithGroupId(key));
                builder.AddAdminClient(key, a => a.WithBootstrapServers("unused:9092"));
            }
        });
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await AssertSingletonClients(provider, scope.ServiceProvider, "orders");
        await AssertSingletonClients(provider, scope.ServiceProvider, "invoices");
        await Assert.That(provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("orders"))
            .IsNotSameReferenceAs(provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("invoices"));
        await Assert.That(provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders"))
            .IsNotSameReferenceAs(provider.GetRequiredService<IKafkaProducer<string, string>>());
        await Assert.That(provider.GetServices<IInitializableKafkaClient>()).IsEmpty();
    }

    [Test]
    public async Task AddDekafInMemory_ClosedUnkeyedClientsAreSingletonsAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaProducer<string, string>>(_ => throw new InvalidOperationException("real producer"));
        services.AddSingleton<IKafkaConsumer<string, string>>(_ => throw new InvalidOperationException("real consumer"));
        services.AddSingleton<IKafkaShareConsumer<string, string>>(_ => throw new InvalidOperationException("real share consumer"));
        services.AddSingleton<IAdminClient>(_ => throw new InvalidOperationException("real admin"));
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await AssertSingletonClients(provider, scope.ServiceProvider, serviceKey: null);
    }

    [Test]
    [Arguments(ServiceLifetime.Singleton, false)]
    [Arguments(ServiceLifetime.Singleton, true)]
    [Arguments(ServiceLifetime.Scoped, false)]
    [Arguments(ServiceLifetime.Scoped, true)]
    [Arguments(ServiceLifetime.Transient, false)]
    [Arguments(ServiceLifetime.Transient, true)]
    public async Task AddDekafInMemory_PreservesCustomRegistrationLifetime(ServiceLifetime lifetime, bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        var type = typeof(IKafkaConsumer<string, string>);
        services.Add(keyed
            ? ServiceDescriptor.DescribeKeyed(type, "orders", (_, _) => throw new InvalidOperationException("real consumer"), lifetime)
            : ServiceDescriptor.Describe(type, _ => throw new InvalidOperationException("real consumer"), lifetime));
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        object? key = keyed ? "orders" : null;
        var first = firstScope.ServiceProvider.GetRequiredKeyedService<IKafkaConsumer<string, string>>(key);
        var repeated = firstScope.ServiceProvider.GetRequiredKeyedService<IKafkaConsumer<string, string>>(key);
        var second = secondScope.ServiceProvider.GetRequiredKeyedService<IKafkaConsumer<string, string>>(key);

        await Assert.That(first).IsTypeOf<InMemoryConsumer<string, string>>();
        await Assert.That(ReferenceEquals(first, repeated)).IsEqualTo(lifetime != ServiceLifetime.Transient);
        await Assert.That(ReferenceEquals(first, second)).IsEqualTo(lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddDekafInMemory_ReplacesOpenGenericKeyedClients()
    {
        var key = new object();
        var services = new ServiceCollection();
        services.AddKeyedSingleton(typeof(IKafkaProducer<,>), key, typeof(InMemoryProducer<,>));
        services.AddKeyedSingleton(typeof(IKafkaConsumer<,>), key, typeof(InMemoryConsumer<,>));
        services.AddKeyedSingleton(typeof(IKafkaShareConsumer<,>), key, typeof(InMemoryShareConsumer<,>));
        services.AddKeyedSingleton<IAdminClient>(key, (_, _) => throw new InvalidOperationException("real admin"));
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await AssertSingletonClients(provider, scope.ServiceProvider, key);
        await Assert.That(provider.GetRequiredKeyedService<IKafkaProducer<int, string>>(key))
            .IsTypeOf<InMemoryProducer<int, string>>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AddDekafInMemory_HostedConsumerUsesApplicationSingleton(bool keyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDekaf(dekaf =>
        {
            if (keyed)
            {
                dekaf.AddConsumerService<OrdersWorker, string, string>("orders", c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
            }
            else
            {
                dekaf.AddConsumerService<OrdersWorker, string, string>(c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
            }
        });
        builder.Services.AddSingleton(new InMemoryConsumerOptions
        {
            GroupId = "orders",
            AutoOffsetReset = AutoOffsetReset.Earliest
        });
        builder.Services.AddDekafInMemory();
        using var host = builder.Build();
        var consumer = keyed
            ? host.Services.GetRequiredKeyedService<IKafkaConsumer<string, string>>("orders")
            : host.Services.GetRequiredService<IKafkaConsumer<string, string>>();
        var producer = host.Services.GetRequiredService<IKafkaProducer<string, string>>();
        await producer.ProduceAsync("orders", "key", "value");

        await host.StartAsync();
        try
        {
            var worker = host.Services.GetServices<IHostedService>().OfType<OrdersWorker>().Single();
            var record = await worker.Processed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(worker.Client).IsSameReferenceAs(consumer);
            await Assert.That(record.Value).IsEqualTo("value");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Test]
    public async Task AddDekafInMemory_ClientEnumerationPreservesRegistrationCount()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p => p.WithBootstrapServers("unused:9092"));
            builder.AddProducer<int, string>(p => p.WithBootstrapServers("unused:9092"));
            builder.AddConsumer<string, string>(c => c.WithBootstrapServers("unused:9092").WithGroupId("orders"));
            builder.AddShareConsumer<string, string>(c => c.WithBootstrapServers("unused:9092").WithGroupId("orders"));
            builder.AddAdminClient(a => a.WithBootstrapServers("unused:9092"));
        });
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetServices<IKafkaProducer<string, string>>().Single())
            .IsSameReferenceAs(provider.GetRequiredService<IKafkaProducer<string, string>>());
        await Assert.That(provider.GetServices<IKafkaProducer<int, string>>().Single())
            .IsSameReferenceAs(provider.GetRequiredService<IKafkaProducer<int, string>>());
        await Assert.That(provider.GetServices<IKafkaConsumer<string, string>>().Single())
            .IsSameReferenceAs(provider.GetRequiredService<IKafkaConsumer<string, string>>());
        await Assert.That(provider.GetServices<IKafkaShareConsumer<string, string>>().Single())
            .IsSameReferenceAs(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());
        await Assert.That(provider.GetServices<IAdminClient>().Single())
            .IsSameReferenceAs(provider.GetRequiredService<IAdminClient>());
    }

    [Test]
    public async Task AddDekafInMemory_ExistingOpenGenericClientsDoNotGainFallbacks()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IKafkaProducer<,>), typeof(InMemoryProducer<,>));
        services.AddScoped(typeof(IKafkaConsumer<,>), typeof(InMemoryConsumer<,>));
        services.AddScoped(typeof(IKafkaShareConsumer<,>), typeof(InMemoryShareConsumer<,>));
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var scoped = scope.ServiceProvider;
        await Assert.That(scoped.GetServices<IKafkaProducer<int, string>>().Single())
            .IsSameReferenceAs(scoped.GetRequiredService<IKafkaProducer<int, string>>());
        await Assert.That(scoped.GetServices<IKafkaConsumer<int, string>>().Single())
            .IsSameReferenceAs(scoped.GetRequiredService<IKafkaConsumer<int, string>>());
        await Assert.That(scoped.GetServices<IKafkaShareConsumer<int, string>>().Single())
            .IsSameReferenceAs(scoped.GetRequiredService<IKafkaShareConsumer<int, string>>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AddDekafInMemory_HostedShareConsumerAliasesPreserveWorkerIdentity(bool keyed)
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            if (keyed)
            {
                builder.AddShareConsumerService<ShareOrdersWorker, string, string>("orders", c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
                builder.AddShareConsumerService<OtherShareOrdersWorker, string, string>("orders", c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
            }
            else
            {
                builder.AddShareConsumerService<ShareOrdersWorker, string, string>(c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
                builder.AddShareConsumerService<OtherShareOrdersWorker, string, string>(c => c
                    .WithBootstrapServers("unused:9092").WithGroupId("orders"));
            }
        });
        services.AddDekafInMemory();

        await using var provider = services.BuildServiceProvider();
        object? key = keyed ? "orders" : null;
        var workers = provider.GetServices<IHostedService>().OfType<ShareOrdersWorker>().ToArray();
        var consumers = provider.GetKeyedServices<IKafkaShareConsumer<string, string>>(key).ToArray();
        await Assert.That(workers.Length).IsEqualTo(2);
        await Assert.That(consumers.Length).IsEqualTo(2);
        for (var i = 0; i < workers.Length; i++)
        {
            await Assert.That(consumers[i]).IsTypeOf<InMemoryShareConsumer<string, string>>();
            await Assert.That(consumers[i]).IsSameReferenceAs(workers[i].Client);
            var privateKey = new KafkaShareConsumerServiceKey(workers[i].GetType(), key);
            await Assert.That(consumers[i]).IsSameReferenceAs(
                provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(privateKey));
        }
        await Assert.That(consumers[0]).IsNotSameReferenceAs(consumers[1]);
        await Assert.That(provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(key))
            .IsSameReferenceAs(consumers[1]);
    }

    private class ShareOrdersWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance)
    {
        public IKafkaShareConsumer<string, string> Client { get; } = consumer;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class OtherShareOrdersWorker(IKafkaShareConsumer<string, string> consumer) : ShareOrdersWorker(consumer);

    private static async Task AssertSingletonClients(IServiceProvider provider, IServiceProvider scoped, object? serviceKey)
    {
        var producer = provider.GetRequiredKeyedService<IKafkaProducer<string, string>>(serviceKey);
        var consumer = provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>(serviceKey);
        var shareConsumer = provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(serviceKey);
        var admin = provider.GetRequiredKeyedService<IAdminClient>(serviceKey);

        await Assert.That(producer).IsTypeOf<InMemoryProducer<string, string>>();
        await Assert.That(consumer).IsTypeOf<InMemoryConsumer<string, string>>();
        await Assert.That(shareConsumer).IsTypeOf<InMemoryShareConsumer<string, string>>();
        await Assert.That(admin).IsTypeOf<InMemoryAdminClient>();
        await Assert.That(producer).IsSameReferenceAs(scoped.GetRequiredKeyedService<IKafkaProducer<string, string>>(serviceKey));
        await Assert.That(consumer).IsSameReferenceAs(scoped.GetRequiredKeyedService<IKafkaConsumer<string, string>>(serviceKey));
        await Assert.That(shareConsumer).IsSameReferenceAs(scoped.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>(serviceKey));
        await Assert.That(admin).IsSameReferenceAs(scoped.GetRequiredKeyedService<IAdminClient>(serviceKey));
    }

    private sealed class OrdersWorker(IKafkaConsumer<string, string> consumer)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaConsumerServiceOptions { DrainOnShutdown = false })
    {
        public IKafkaConsumer<string, string> Client { get; } = consumer;
        public TaskCompletionSource<ConsumeResult<string, string>> Processed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override IEnumerable<string> Topics => ["orders"];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            Processed.TrySetResult(result);
            return ValueTask.CompletedTask;
        }
    }
}
