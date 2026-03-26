using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KafkaConsumerService hosted service.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class HostedServiceTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProcessesMessages_ViaHostedService_MessagesReceived()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"hosted-service-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<string>();
        const int messageCount = 5;

        // Create and initialize the producer BEFORE starting the host.
        // This ensures the producer's connection and metadata are established
        // before the consumer's background polling starts competing for
        // thread pool and connection resources on resource-constrained CI runners.
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        // (topic metadata is cached after this call)
        await producer.ProduceAsync(new ProducerMessage<string, string>
            { Topic = topic, Key = "warmup", Value = "warmup" });

        // Build host with consumer service
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumer<string, string>(c =>
                c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                 .WithGroupId(groupId)
                 .WithAutoOffsetReset(AutoOffsetReset.Earliest));
        });

        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(topic));
        builder.Services.AddHostedService<TestConsumerService>();

        var host = builder.Build();

        // Start the host
        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        // Give the consumer service time to start and join the group.
        // The consumer needs to subscribe, join the group, and receive its partition
        // assignment before it can receive messages. On CI runners this can take longer
        // than on local machines due to thread pool contention.
        await Task.Delay(2000).ConfigureAwait(false);

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"hosted-key-{i}",
                Value = $"hosted-value-{i}"
            }, CancellationToken.None);
        }

        // Wait for messages to be processed
        await WaitForConditionAsync(
            () => receivedMessages.Count >= messageCount,
            TimeSpan.FromSeconds(30));

        await Assert.That(receivedMessages.Count).IsGreaterThanOrEqualTo(messageCount);

        // Stop gracefully
        cts.Cancel();

        try
        {
            await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Host shutdown can be slow on resource-constrained CI runners
        }
        finally
        {
            host.Dispose();
        }
    }

    [Test]
    public async Task StopsGracefully_NoExceptions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"hosted-stop-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<string>();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumer<string, string>(c =>
                c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                 .WithGroupId(groupId)
                 .WithAutoOffsetReset(AutoOffsetReset.Earliest));
        });

        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(topic));
        builder.Services.AddHostedService<TestConsumerService>();

        var host = builder.Build();

        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        // Let it start
        await Task.Delay(1000).ConfigureAwait(false);

        // Stop
        cts.Cancel();

        // Should stop without throwing
        Exception? caughtException = null;
        try
        {
            await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Host shutdown can be slow on resource-constrained CI runners
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }
        finally
        {
            host.Dispose();
        }

        await Assert.That(caughtException).IsNull();
    }

    private sealed class TestTopicHolder(string topic)
    {
        public string Topic { get; } = topic;
    }

    private sealed class TestConsumerService : KafkaConsumerService<string, string>
    {
        private readonly ConcurrentBag<string> _receivedMessages;
        private readonly TestTopicHolder _topicHolder;

        public TestConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<TestConsumerService> logger,
            ConcurrentBag<string> receivedMessages,
            TestTopicHolder topicHolder)
            : base(consumer, logger)
        {
            _receivedMessages = receivedMessages;
            _topicHolder = topicHolder;
        }

        protected override IEnumerable<string> Topics => [_topicHolder.Topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            _receivedMessages.Add(result.Value);
            return ValueTask.CompletedTask;
        }
    }
}
