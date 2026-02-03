using System.Threading.Channels;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Unit tests for producer cancellation semantics.
/// These tests verify cancellation behavior in controlled conditions without timing-based race conditions.
/// </summary>
public class ProducerCancellationTests
{
    [Test]
    public async Task RecordAccumulator_FlushAsync_PreCancelledToken_ThrowsImmediately()
    {
        // Arrange
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 1_000_000,
            BatchSize = 16384,
            LingerMs = 100,
            DeliveryTimeoutMs = 30_000
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            // Act & Assert - Should throw immediately without waiting
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await accumulator.FlushAsync(cts.Token);
            });
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task RecordAccumulator_FlushAsync_CompletesQuicklyDueToCompletionLoop()
    {
        // Arrange - With the new completion loop architecture, delivery tasks complete
        // immediately when batches become ready, making fire-and-forget truly fast
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 1_000_000,
            BatchSize = 16384,
            LingerMs = 10000, // Long linger, but FlushAsync expires batches immediately
            DeliveryTimeoutMs = 30_000
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Add a message to create a batch
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);
            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            // Act - FlushAsync completes quickly because completion loop completes delivery immediately
            using var cts = new CancellationTokenSource(100); // Generous timeout
            var startTime = Environment.TickCount64;
            await accumulator.FlushAsync(cts.Token);
            var elapsed = Environment.TickCount64 - startTime;

            // Assert - Should complete in well under 100ms (completion loop is synchronous)
            await Assert.That(elapsed).IsLessThan(100);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ChannelWriter_WriteAsync_PreCancelledToken_ThrowsImmediately()
    {
        // Arrange - Test that channel write respects pre-cancelled tokens
        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act & Assert - WriteAsync should throw immediately
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await channel.Writer.WriteAsync(42, cts.Token);
        });
    }

    [Test]
    public async Task ChannelWriter_WriteAsync_CancelledDuringWrite_ThrowsOperationCancelled()
    {
        // Arrange - Create bounded channel that can block
        var channel = Channel.CreateBounded<int>(1);

        // Fill the channel so next write blocks
        await channel.Writer.WriteAsync(1);

        using var cts = new CancellationTokenSource();

        // Act - Start write that will block (channel is full)
        var writeTask = channel.Writer.WriteAsync(2, cts.Token);

        // Cancel during the blocked write
        cts.Cancel();

        // Assert - Should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await writeTask;
        });
    }

    [Test]
    public async Task CancellationToken_ThrowIfCancellationRequested_PreCancelled_Throws()
    {
        // Arrange - Test that pre-queue checks work correctly
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw immediately
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await Task.Run(() => cts.Token.ThrowIfCancellationRequested());
        });
    }

    [Test]
    public async Task Task_WaitAsync_CancelledDuringWait_ThrowsOperationCancelled()
    {
        // Arrange - Test that Task.WaitAsync respects cancellation (used in FlushAsync)
        var tcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource();

        // Act - Start waiting with cancellation
        var waitTask = tcs.Task.WaitAsync(cts.Token);

        // Cancel during wait
        await Task.Delay(10);
        cts.Cancel();

        // Assert - Should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await waitTask;
        });
    }

    [Test]
    public async Task RecordAccumulator_MultipleFlushAsync_WithPreCancelledTokens_AllThrow()
    {
        // Arrange - Test that multiple concurrent flushes all respect pre-cancelled tokens
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 1_000_000,
            BatchSize = 16384,
            LingerMs = 10000,
            DeliveryTimeoutMs = 30_000
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Add messages to create batches
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);
            for (int i = 0; i < 10; i++)
            {
                accumulator.TryAppendFireAndForget(
                    "test-topic", i % 3, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null);
            }

            // Act - Start multiple flushes with pre-cancelled tokens
            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            using var cts3 = new CancellationTokenSource();

            cts1.Cancel();
            cts2.Cancel();
            cts3.Cancel();

            // Assert - All should throw OperationCanceledException immediately
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await accumulator.FlushAsync(cts1.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await accumulator.FlushAsync(cts2.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await accumulator.FlushAsync(cts3.Token));
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }
}
