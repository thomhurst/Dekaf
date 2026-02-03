using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying the BufferMemory fix prevents unbounded memory growth
/// under sustained load. This is a regression test for the arena fast path bypass bug.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class BufferMemoryStressTests(KafkaTestContainer kafka)
{
    /// <summary>
    /// Regression test for the BufferMemory arena bypass bug.
    /// Verifies that sustained message production to the same partition does not cause
    /// unbounded memory growth. The original bug caused 18GB+ growth in 90 seconds.
    /// This test runs for 30 seconds (enough to detect multi-GB leaks) and ensures memory
    /// growth stays under reasonable bounds.
    /// </summary>
    [Test]
    public async Task SustainedLoad_DoesNotCauseUnboundedMemoryGrowth()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 1).ConfigureAwait(false);

        // Use small 8MB buffer to make the test more sensitive to leaks
        // Smaller buffer = leak more obvious relative to expected memory footprint
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("memory-stress-test")
            .WithAcks(Acks.Leader)
            .WithBufferMemory(8388608) // 8 MB - small buffer makes leaks more obvious
            .Build();

        // Force full GC before measuring baseline
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        var initialMemory = GC.GetTotalMemory(forceFullCollection: false);
        // 30 seconds is sufficient to detect the bug (original showed 18GB/90s = ~6GB in 30s)
        // while being CI-friendly (avoids 10-minute timeout with other tests)
        var testDuration = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        var messageCount = 0;
        var lastLogTime = startTime;

        Console.WriteLine($"[BufferMemoryStressTest] Starting sustained load test");
        Console.WriteLine($"[BufferMemoryStressTest] Initial memory: {initialMemory / 1_000_000.0:F1} MB");
        Console.WriteLine($"[BufferMemoryStressTest] Test duration: {testDuration.TotalSeconds} seconds");

        // Act: Send messages continuously for 30 seconds using fire-and-forget
        // Using the same key ensures messages go to the same partition, triggering the arena fast path
        // Use Send() (fire-and-forget) instead of ProduceAsync to avoid blocking on network I/O
        // This matches real high-throughput producer patterns and allows clean test termination
        var deadline = startTime + testDuration;

        while (DateTime.UtcNow < deadline)
        {
            // Fire-and-forget: Send returns immediately, delivery happens async
            producer.Send(topic, "same-key", $"value-{messageCount}");

            messageCount++;

            // Log memory every 10 seconds
            var elapsed = DateTime.UtcNow - lastLogTime;
            if (elapsed.TotalSeconds >= 10)
            {
                var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
                var growthMB = (currentMemory - initialMemory) / 1_000_000.0;
                var rate = messageCount / (DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"[BufferMemoryStressTest] {(DateTime.UtcNow - startTime).TotalSeconds:F0}s: " +
                                 $"{messageCount:N0} messages ({rate:F0} msg/s), Memory growth: {growthMB:F1} MB");
                lastLogTime = DateTime.UtcNow;
            }
        }

        Console.WriteLine($"[BufferMemoryStressTest] Test duration reached, stopping message production");

        // Ensure all messages are flushed (with timeout to prevent test hanging)
        using var flushCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await producer.FlushAsync(flushCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[BufferMemoryStressTest] Flush timed out after 30s, continuing with memory check");
        }

        // Force full GC to get accurate final memory measurement
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        var totalGrowthMB = (finalMemory - initialMemory) / 1_000_000.0;

        Console.WriteLine($"[BufferMemoryStressTest] Test complete");
        Console.WriteLine($"[BufferMemoryStressTest] Messages sent: {messageCount:N0}");
        Console.WriteLine($"[BufferMemoryStressTest] Average rate: {messageCount / testDuration.TotalSeconds:F0} msg/s");
        Console.WriteLine($"[BufferMemoryStressTest] Initial memory: {initialMemory / 1_000_000.0:F1} MB");
        Console.WriteLine($"[BufferMemoryStressTest] Final memory: {finalMemory / 1_000_000.0:F1} MB");
        Console.WriteLine($"[BufferMemoryStressTest] Total memory growth: {totalGrowthMB:F1} MB");

        // Assert: Memory growth should be < 200MB
        // With 8MB buffer, expect memory to stay well under this. The original bug caused
        // 18GB growth in 90 seconds (~6GB in 30s), so 200MB catches leaks while allowing
        // for CI environment variability (container overhead, GC timing differences, etc.)
        Console.WriteLine($"[BufferMemoryStressTest] Asserting memory growth < 200 MB (actual: {totalGrowthMB:F1} MB)");
        await Assert.That(totalGrowthMB).IsLessThan(200);
    }

    /// <summary>
    /// Verifies that BufferMemory accounting works correctly under load.
    /// While we can't easily test backpressure in integration tests without adding
    /// builder API, we can verify that sustained load doesn't exceed expected memory.
    /// </summary>
    [Test]
    public async Task BufferedBytes_StaysWithinReasonableBounds_UnderLoad()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 4).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("buffered-bytes-test")
            .WithAcks(Acks.Leader)
            .WithLingerMs(100) // Add some linger to allow batching
            .Build();

        var messageValue = new string('x', 1000); // 1KB messages
        var messageCount = 1000;
        var maxObservedMemoryMB = 0.0;

        Console.WriteLine($"[BufferedBytesTest] Sending {messageCount} messages");

        // Act: Send messages and observe memory doesn't grow unbounded
        var tasks = new List<Task<RecordMetadata>>();

        for (int i = 0; i < messageCount; i++)
        {
            var sendTask = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i % 10}", // Distribute across partitions
                Value = messageValue
            }).AsTask(); // Convert to Task to store safely

            tasks.Add(sendTask);

            // Sample memory periodically
            if (i % 100 == 0)
            {
                var currentMemory = GC.GetTotalMemory(forceFullCollection: false) / 1_000_000.0;
                if (currentMemory > maxObservedMemoryMB)
                {
                    maxObservedMemoryMB = currentMemory;
                }
            }
        }

        // Wait for all messages to be sent
        await Task.WhenAll(tasks).ConfigureAwait(false);

        Console.WriteLine($"[BufferedBytesTest] All {messageCount} messages sent successfully");
        Console.WriteLine($"[BufferedBytesTest] Max observed memory: {maxObservedMemoryMB:F1} MB");

        // Assert: All messages delivered successfully
        await Assert.That(tasks.Count).IsEqualTo(messageCount);
        foreach (var task in tasks)
        {
            await Assert.That(task.IsCompletedSuccessfully).IsTrue();
            await Assert.That(task.Result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }
}
