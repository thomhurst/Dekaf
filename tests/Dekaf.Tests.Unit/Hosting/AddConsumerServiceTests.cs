using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
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
        var options = provider.GetRequiredService<DeadLetterOptions>();

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

        await Assert.That(() => provider.GetServices<IHostedService>().ToList())
            .Throws<InvalidOperationException>();
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
