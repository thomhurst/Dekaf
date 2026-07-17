using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Hosting;

public class AddConsumerServiceTests
{
    [Test]
    public async Task AddConsumerService_RegistersConsumerAndHostedService()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group"));
        });

        await Assert.That(services.Any(d =>
            d.ServiceType == typeof(IKafkaConsumer<string, string>) &&
            d.Lifetime == ServiceLifetime.Singleton)).IsTrue();
        await Assert.That(services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TestService))).IsTrue();
    }

    [Test]
    public async Task AddConsumerService_WithDeadLetterQueue_RegistersOptionsWithInheritedServers()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq.WithMaxFailures(3));
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredKeyedService<DeadLetterOptions>(
            typeof(IKafkaConsumer<string, string>));

        await Assert.That(options.MaxFailures).IsEqualTo(3);
        await Assert.That(options.BootstrapServers).IsEqualTo("localhost:9092");
    }

    [Test]
    public async Task AddConsumerService_OptionsOverload_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            });
        });

        await Assert.That(services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TestService))).IsTrue();
    }

    [Test]
    public async Task AddConsumerService_WithDeadLetterQueue_ServiceReceivesOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<DlqTestService, string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq.WithTopicSuffix(".dead"));
        });

        var provider = services.BuildServiceProvider();
        var service = provider.GetServices<IHostedService>().OfType<DlqTestService>().Single();

        await Assert.That(service.ConfiguredDeadLetterOptions).IsNotNull();
        await Assert.That(service.ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".dead");
    }

    [Test]
    public async Task AddConsumerService_WithDeadLetterQueue_ServiceNotForwardingOptions_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq.WithMaxFailures(3));
        });

        var provider = services.BuildServiceProvider();

        InvalidOperationException? caught = null;
        try
        {
            _ = provider.GetServices<IHostedService>().ToList();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("DeadLetterOptions");
    }

    [Test]
    public async Task AddConsumerService_ForwardingServiceWithMissingDependency_SurfacesOriginalError()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<DlqWithDependencyService, string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group"),
                dlq => dlq.WithMaxFailures(3));
        });

        var provider = services.BuildServiceProvider();

        InvalidOperationException? caught = null;
        try
        {
            _ = provider.GetServices<IHostedService>().ToList();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("IMissingDependency");
        await Assert.That(caught.Message).DoesNotContain("DeadLetterOptions");
    }

    [Test]
    public async Task AddConsumerService_ConfigurationOverload_RegistersConsumerAndHostedService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapServers"] = "localhost:9092",
                ["GroupId"] = "cfg-group"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(configuration);
        });

        await Assert.That(services.Any(d =>
            d.ServiceType == typeof(IKafkaConsumer<string, string>) &&
            d.Lifetime == ServiceLifetime.Singleton)).IsTrue();
        await Assert.That(services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TestService))).IsTrue();
    }

    [Test]
    public async Task AddConsumerService_InitializationServiceStartsBeforeConsumerService()
    {
        var services = new ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group"));
        });

        var hostedDescriptors = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        var initializerIndex = hostedDescriptors.FindIndex(d =>
            d.ImplementationType?.Name == "DekafInitializationService");
        var consumerServiceIndex = hostedDescriptors.FindIndex(d =>
            d.ImplementationType == typeof(TestService));

        await Assert.That(initializerIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(consumerServiceIndex).IsGreaterThan(initializerIndex);
    }

    [Test]
    public async Task AddConsumerService_SameServiceTypeSameKeyTwice_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();
        InvalidOperationException? caught = null;

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("first"));

            try
            {
                // Mixed shape (second registration adds a DLQ) is the hazardous case: without
                // the guard it would silently start two competing instances.
                builder.AddConsumerService<TestService, string, string>(
                    c => c
                        .WithBootstrapServers("localhost:9092")
                        .WithGroupId("second"),
                    dlq => dlq.WithMaxFailures(2));
            }
            catch (InvalidOperationException ex)
            {
                caught = ex;
            }
        });

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("already registered");

        // The guard runs before any container mutation, so the rejected call must not have
        // registered a second consumer whose config would silently win last-registration.
        await Assert.That(services.Count(d =>
            d.ServiceType == typeof(IKafkaConsumer<string, string>))).IsEqualTo(1);
    }

    [Test]
    public async Task AddConsumerService_SameServiceTypeUnderTwoKeys_BothServicesRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<TestService, string, string>("orders", c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("orders"));

            builder.AddConsumerService<TestService, string, string>("payments", c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("payments"));
        });

        var provider = services.BuildServiceProvider();
        var instances = provider.GetServices<IHostedService>().OfType<TestService>().ToList();

        await Assert.That(instances).Count().IsEqualTo(2);
        await Assert.That(ReferenceEquals(instances[0], instances[1])).IsFalse();
    }

    [Test]
    public async Task AddConsumerService_TwoTypePairsWithDeadLetterQueues_OptionsDoNotCollide()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<DlqTestService, string, string>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("strings"),
                dlq => dlq.WithTopicSuffix(".strings-dlq"));

            builder.AddConsumerService<DlqBytesService, string, byte[]>(
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("bytes"),
                dlq => dlq.WithTopicSuffix(".bytes-dlq"));
        });

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        var stringService = hostedServices.OfType<DlqTestService>().Single();
        var bytesService = hostedServices.OfType<DlqBytesService>().Single();

        await Assert.That(stringService.ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".strings-dlq");
        await Assert.That(bytesService.ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".bytes-dlq");
    }

    [Test]
    public async Task AddConsumerService_KeyedSameTypePair_EachServiceGetsOwnConsumerAndOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDekaf(builder =>
        {
            builder.AddConsumerService<DlqTestService, string, string>(
                "orders",
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("orders"),
                dlq => dlq.WithTopicSuffix(".orders-dlq"));

            builder.AddConsumerService<SecondDlqTestService, string, string>(
                "payments",
                c => c
                    .WithBootstrapServers("localhost:9092")
                    .WithGroupId("payments"),
                dlq => dlq.WithTopicSuffix(".payments-dlq"));
        });

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        var ordersService = hostedServices.OfType<DlqTestService>().Single();
        var paymentsService = hostedServices.OfType<SecondDlqTestService>().Single();

        await Assert.That(ordersService.ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".orders-dlq");
        await Assert.That(paymentsService.ConfiguredDeadLetterOptions!.TopicSuffix).IsEqualTo(".payments-dlq");
    }

    private sealed class TestService : KafkaConsumerService<string, string>
    {
        public TestService(IKafkaConsumer<string, string> consumer, ILogger<TestService> logger)
            : base(consumer, logger)
        {
        }

        protected override IEnumerable<string> Topics => ["test-topic"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class DlqTestService : KafkaConsumerService<string, string>
    {
        public DlqTestService(
            IKafkaConsumer<string, string> consumer,
            ILogger<DlqTestService> logger,
            DeadLetterOptions deadLetterOptions)
            : base(consumer, logger, deadLetterOptions)
        {
        }

        protected override IEnumerable<string> Topics => ["test-topic"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class SecondDlqTestService : KafkaConsumerService<string, string>
    {
        public SecondDlqTestService(
            IKafkaConsumer<string, string> consumer,
            ILogger<SecondDlqTestService> logger,
            DeadLetterOptions deadLetterOptions)
            : base(consumer, logger, deadLetterOptions)
        {
        }

        protected override IEnumerable<string> Topics => ["other-topic"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private interface IMissingDependency;

    private sealed class DlqWithDependencyService : KafkaConsumerService<string, string>
    {
        public DlqWithDependencyService(
            IKafkaConsumer<string, string> consumer,
            ILogger<DlqWithDependencyService> logger,
            DeadLetterOptions deadLetterOptions,
            IMissingDependency dependency)
            : base(consumer, logger, deadLetterOptions)
        {
            _ = dependency;
        }

        protected override IEnumerable<string> Topics => ["test-topic"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class DlqBytesService : KafkaConsumerService<string, byte[]>
    {
        public DlqBytesService(
            IKafkaConsumer<string, byte[]> consumer,
            ILogger<DlqBytesService> logger,
            DeadLetterOptions deadLetterOptions)
            : base(consumer, logger, deadLetterOptions)
        {
        }

        protected override IEnumerable<string> Topics => ["bytes-topic"];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, byte[]> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
