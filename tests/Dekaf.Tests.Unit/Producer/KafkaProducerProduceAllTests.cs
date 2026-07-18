using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for <c>KafkaProducer.ProduceAllAsync</c>'s synchronous-throw handling: a produce call
/// that throws before registering (here: producer never initialized) must be recorded via the
/// completion's abort path and surface from the aggregate await instead of hanging or leaking.
/// </summary>
public class KafkaProducerProduceAllTests
{
    [Test]
    public async Task ProduceAllAsync_KeyValueOverload_SyncThrow_FaultsAggregateWithoutHanging()
    {
        // Build() without InitializeAsync: the first ProduceAsync throws synchronously at entry,
        // driving ProduceAllAsync's catch block (RecordFailure + AbortRegistration at index 0).
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var messages = new (string? Key, string Value)[] { ("k0", "v0"), ("k1", "v1"), ("k2", "v2") };

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await producer.ProduceAllAsync("test-topic", messages)
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    public async Task ProduceAllAsync_MessageListOverload_SyncThrow_FaultsAggregateWithoutHanging()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var messages = Enumerable.Range(0, 3).Select(i => new ProducerMessage<string, string>
            {
                Topic = "test-topic",
                Key = $"k{i}",
                Value = $"v{i}"
            }).ToList();

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await producer.ProduceAllAsync(messages)
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }
}
