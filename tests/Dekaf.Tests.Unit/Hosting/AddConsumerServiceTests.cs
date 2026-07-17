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
}
