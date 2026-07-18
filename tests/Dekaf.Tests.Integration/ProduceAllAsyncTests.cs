using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
public class ProduceAllAsyncTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceAllAsync_MultipleMessages_AllDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var messages = Enumerable.Range(0, 10).Select(i => new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = $"key-{i}",
            Value = $"value-{i}"
        });

        var results = await producer.ProduceAllAsync(messages);

        await Assert.That(results.Length).IsEqualTo(10);

        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task ProduceAllAsync_ToSingleTopic_WithKeyValuePairs_AllDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var messages = Enumerable.Range(0, 5)
            .Select(i => ((string?)$"key-{i}", $"value-{i}"))
            .ToList();

        var results = await producer.ProduceAllAsync(topic, messages);

        await Assert.That(results.Length).IsEqualTo(5);

        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    [NotInParallel("DekafInstrumentation")]
    public async Task ProduceAllAsync_WithTracingAndMetricsEnabled_AllDelivered()
    {
        // With tracing/metrics active, per-message awaits go through AwaitWithActivity /
        // AwaitWithMetrics, which must upgrade ProduceAllAsync's inline-continuation request to
        // asynchronous continuations (instrumentation code must not run on the broker ack
        // thread). This covers those wrapper paths end-to-end under ProduceAllAsync.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        meterListener.Start();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var messages = Enumerable.Range(0, 100)
            .Select(i => ((string?)$"key-{i}", $"value-{i}"))
            .ToList();

        var results = await producer.ProduceAllAsync(topic, messages)
            .WaitAsync(TimeSpan.FromSeconds(60));

        await Assert.That(results.Length).IsEqualTo(100);
        foreach (var result in results)
        {
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task ProduceAllAsync_SyncThrowMidLoop_InFlightStillDeliveredAndExceptionSurfaces()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // The interceptor cancels the token while message "value-3" is being registered, so a
        // later iteration's entry cancellation check throws synchronously mid-loop — exercising
        // ProduceAllAsync's catch block (RecordFailure + AbortRegistration) with real in-flight
        // registrations. Post-append cancellation stops the wait, not delivery.
        using var cts = new CancellationTokenSource();
        var interceptor = new CancelTokenOnValueInterceptor(cts, "value-3");

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .AddInterceptor(interceptor)
            .BuildAsync())
        {
            var messages = Enumerable.Range(0, 6)
                .Select(i => ((string?)$"key-{i}", $"value-{i}"))
                .ToList();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await producer.ProduceAllAsync(topic, messages, cts.Token)
                    .WaitAsync(TimeSpan.FromSeconds(30)));

            // Messages appended before the throw are still delivered in the background.
            await producer.FlushAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        string[] required = ["value-0", "value-1", "value-2"];
        var consumed = new List<string>();
        using var consumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var draining = false;

        try
        {
            await foreach (var result in consumer.ConsumeAsync(consumeCts.Token))
            {
                consumed.Add(result.Value!);

                // Once everything registered before the throw has arrived, keep draining briefly
                // to prove nothing after the aborted registration loop was produced.
                if (!draining && required.All(consumed.Contains))
                {
                    draining = true;
                    consumeCts.CancelAfter(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Drain window elapsed.
        }

        foreach (var value in required)
        {
            await Assert.That(consumed).Contains(value);
        }

        // "value-3" raced the cancellation and may or may not have been appended; everything
        // after the synchronous throw must never have been produced.
        await Assert.That(consumed).DoesNotContain("value-4");
        await Assert.That(consumed).DoesNotContain("value-5");
    }

    [Test]
    public async Task ProduceAllAsync_EmptyList_ReturnsEmptyResults()
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var results = await producer.ProduceAllAsync(Array.Empty<ProducerMessage<string, string>>());

        await Assert.That(results.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceAllAsync_VerifyAllMessagesConsumed()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var messages = Enumerable.Range(0, 5).Select(i => new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = $"key-{i}",
            Value = $"value-{i}"
        });

        await producer.ProduceAllAsync(messages);

        // Consume all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(result.Value!);
            if (consumed.Count >= 5)
                break;
        }

        await Assert.That(consumed.Count).IsEqualTo(5);
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(consumed).Contains($"value-{i}");
        }
    }

    private sealed class CancelTokenOnValueInterceptor(CancellationTokenSource cts, string triggerValue)
        : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            if (message.Value == triggerValue)
                cts.Cancel();
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
        {
        }
    }
}
