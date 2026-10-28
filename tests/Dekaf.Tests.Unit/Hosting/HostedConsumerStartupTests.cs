using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using static Dekaf.Tests.Unit.Hosting.KafkaShareConsumerServiceTests;

namespace Dekaf.Tests.Unit.Hosting;

/// <summary>
/// Starts a real host with <c>AddDekaf</c>, so the eager client initializer runs before the hosted
/// consumer services. A transient startup failure must reach the service's restart loop instead of
/// faulting <see cref="IHost.StartAsync"/> (#3736).
/// </summary>
public class HostedConsumerStartupTests
{
    [Test]
    public async Task AddConsumerService_TransientInitializationFailure_HostStartsAndServiceRetries()
    {
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = CreateConsumer();
        consumer.InitializeAsync(Arg.Any<CancellationToken>()).Returns(
            ValueTask.FromException(new KafkaTimeoutException("Broker unreachable")),
            ValueTask.CompletedTask);
        consumer.When(c => c.Subscribe(Arg.Any<string[]>())).Do(_ => subscribed.TrySetResult());

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDekaf(dekaf => dekaf.AddConsumerService<FastRestartConsumerService, string, string>(c => c
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("orders")));
        builder.Services.RemoveAll<IKafkaConsumer<string, string>>();
        builder.Services.AddSingleton(consumer);
        using var host = builder.Build();

        await host.StartAsync();
        await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await host.StopAsync();

        // One failed attempt and one successful attempt, both from the service's restart loop.
        await consumer.Received(2).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddShareConsumerService_TransientInitializationFailure_HostStartsAndServiceRetries()
    {
        var consumer = new TestConsumer(Record(0))
        {
            InitializeFailures = new Queue<Exception>([new KafkaTimeoutException("Broker unreachable")])
        };

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDekaf(dekaf => dekaf.AddShareConsumerService<FastRestartShareWorker, string, string>(c => c
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("orders")));
        foreach (var descriptor in builder.Services.Where(d => d.IsKeyedService &&
                     d.ServiceType == typeof(IKafkaShareConsumer<string, string>) &&
                     d.ServiceKey is KafkaShareConsumerServiceKey).ToArray())
        {
            builder.Services.Remove(descriptor);
            builder.Services.AddKeyedSingleton<IKafkaShareConsumer<string, string>>(descriptor.ServiceKey, consumer);
        }
        using var host = builder.Build();

        await host.StartAsync();
        var worker = host.Services.GetServices<IHostedService>().OfType<FastRestartShareWorker>().Single();
        await worker.Processed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await host.StopAsync();

        // One failed attempt and one successful attempt, both from the service's restart loop.
        await Assert.That(string.Join(',', consumer.Events.Take(3))).IsEqualTo("initialize,initialize,subscribe");
    }

    [Test]
    public async Task AddConsumer_WithoutHostedService_StillInitializesEagerlyAtStartup()
    {
        var consumer = CreateConsumer();
        consumer.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new KafkaTimeoutException("Broker unreachable")));

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDekaf(dekaf => dekaf.AddConsumer<string, string>(c => c
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("orders")));
        builder.Services.RemoveAll<IKafkaConsumer<string, string>>();
        builder.Services.AddSingleton(consumer);
        using var host = builder.Build();

        await Assert.That(async () => await host.StartAsync()).Throws<KafkaTimeoutException>();
        await consumer.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    private static IKafkaConsumer<string, string> CreateConsumer()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>, IConsumerOffsetStoreTimingConfiguration>();
        var configuration = (IConsumerOffsetStoreTimingConfiguration)consumer;
        configuration.OffsetCommitMode.Returns(OffsetCommitMode.Auto);
        configuration.EnableAutoOffsetStore.Returns(true);
        configuration.HasConsumerGroup.Returns(true);
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));
        return consumer;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> WaitForCancellation(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: the host is stopping.
        }

        yield break;
    }

    private sealed class FastRestartConsumerService(IKafkaConsumer<string, string> consumer)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance, serviceOptions: new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = false,
            PollRetryBackoff = TimeSpan.FromMilliseconds(1),
            MaxPollRetryBackoff = TimeSpan.FromMilliseconds(1)
        })
    {
        protected override IEnumerable<string> Topics => ["orders"];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class FastRestartShareWorker(IKafkaShareConsumer<string, string> consumer)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance, serviceOptions: new KafkaShareConsumerServiceOptions
        {
            PollRetryBackoff = TimeSpan.FromMilliseconds(1),
            MaxPollRetryBackoff = TimeSpan.FromMilliseconds(1)
        })
    {
        public TaskCompletionSource Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override IEnumerable<string> Topics => ["orders"];

        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
        {
            Processed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
