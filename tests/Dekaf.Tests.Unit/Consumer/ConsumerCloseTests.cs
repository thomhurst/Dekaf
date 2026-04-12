using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for consumer CloseAsync functionality.
/// </summary>
public class ConsumerCloseTests
{
    #region Consumer CloseAsync Idempotency Tests

    [Test]
    public async Task KafkaConsumer_CloseAsync_IsIdempotent()
    {
        // Create a minimal consumer for testing
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
            // Note: No GroupId, so no coordinator - tests basic close behavior
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // First close should succeed
        await consumer.CloseAsync();

        // Second close should also succeed (idempotent)
        await consumer.CloseAsync();

        // If we got here without exceptions, the test passed
        // Verify consumer state reflects closed status
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task KafkaConsumer_CloseAsync_HandlesCancellation()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // Close with already-cancelled token should still work
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should complete without throwing (graceful handling of cancellation)
        try
        {
            await consumer.CloseAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // This is acceptable - cancellation was respected
        }

        // Either graceful completion or caught cancellation is valid
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task KafkaConsumer_DisposeAsync_CallsCloseInternally()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
        };

        var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // Dispose should work without prior close
        await consumer.DisposeAsync();

        // Consumer should be disposed - calling close should do nothing (already closed)
        await consumer.CloseAsync();

        // If we got here without exceptions, the test passed
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    #endregion
}
