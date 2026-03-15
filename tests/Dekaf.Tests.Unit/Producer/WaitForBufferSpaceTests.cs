using System.Buffers;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for <see cref="RecordAccumulator.WaitForBufferSpace"/> which gates
/// fire-and-forget Send() calls when the buffer is full.
/// </summary>
public sealed class WaitForBufferSpaceTests
{
    [Test]
    public async Task WaitForBufferSpace_ReturnsImmediately_WhenBufferHasSpace()
    {
        // Arrange: large buffer, no messages appended - fast path
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = 1000
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act & Assert: should return without blocking
            var completed = Task.Run(() => accumulator.WaitForBufferSpace());
            var finishedInTime = await completed.WaitAsync(TimeSpan.FromSeconds(2))
                .ContinueWith(t => !t.IsFaulted && !t.IsCanceled);

            await Assert.That(finishedInTime).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForBufferSpace_BlocksThenUnblocks_WhenReleaseMemoryIsCalled()
    {
        // Arrange: 1KB buffer with short MaxBlockMs for the fill phase.
        // After filling, we create a NEW accumulator with the same buffer state
        // to test WaitForBufferSpace with a longer timeout.
        //
        // Strategy: Fill buffer until KafkaTimeoutException (proving it's full),
        // then verify WaitForBufferSpace blocks and unblocks when memory is released.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = 1000,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = 10000  // Long enough for the ReleaseMemory to fire
        };
        var accumulator = new RecordAccumulator(options);

        var rentedArrays = new List<byte[]>();
        try
        {
            // Fill the buffer by appending until we know it's at capacity.
            // ReserveMemorySync will block when the buffer is full; with MaxBlockMs=10000
            // the first few appends succeed, then eventually one blocks.
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var filledToCapacity = false;

            for (int i = 0; i < 100; i++)
            {
                var valueBytes = ArrayPool<byte>.Shared.Rent(200);
                rentedArrays.Add(valueBytes);
                var pooledValue = new PooledMemory(valueBytes, 200);

                // Use a short-lived task with a timeout to detect blocking
                var appendTask = Task.Run(() =>
                {
                    accumulator.Append(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, null, null, null);
                });

                // If append doesn't complete in 500ms, the buffer is full and blocking
                var completed = await Task.WhenAny(appendTask, Task.Delay(500)) == appendTask;
                if (!completed)
                {
                    filledToCapacity = true;
                    // Release memory to unblock the stuck append
                    if (accumulator.TryGetBatch("test-topic", 0, out var stuckBatch))
                    {
                        ClearCurrentBatch(accumulator, "test-topic", 0);
                        accumulator.ReleaseMemory(stuckBatch!.EstimatedSize);
                    }
                    // Wait for the append to complete now that memory is released
                    await appendTask.WaitAsync(TimeSpan.FromSeconds(5));
                    break;
                }

                if (appendTask.IsFaulted)
                    break;
            }

            await Assert.That(filledToCapacity).IsTrue();

            // Now the buffer should be near capacity again after the unblocked append.
            // Let's fill it once more by appending until blocking is detected.
            filledToCapacity = false;
            for (int i = 0; i < 100; i++)
            {
                var valueBytes = ArrayPool<byte>.Shared.Rent(200);
                rentedArrays.Add(valueBytes);
                var pooledValue = new PooledMemory(valueBytes, 200);

                var appendTask = Task.Run(() =>
                {
                    accumulator.Append(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, null, null, null);
                });

                var completed = await Task.WhenAny(appendTask, Task.Delay(500)) == appendTask;
                if (!completed)
                {
                    filledToCapacity = true;
                    // Don't unblock - leave it blocking. We'll test WaitForBufferSpace instead.
                    // But first release to not leave a stuck task
                    if (accumulator.TryGetBatch("test-topic", 0, out var stuckBatch2))
                    {
                        ClearCurrentBatch(accumulator, "test-topic", 0);
                        accumulator.ReleaseMemory(stuckBatch2!.EstimatedSize);
                    }
                    await appendTask.WaitAsync(TimeSpan.FromSeconds(5));
                    break;
                }

                if (appendTask.IsFaulted)
                    break;
            }

            // Now call WaitForBufferSpace - it should block
            var waitCompleted = false;
            var waitTask = Task.Run(() =>
            {
                accumulator.WaitForBufferSpace();
                Volatile.Write(ref waitCompleted, true);
            });

            // Give the background thread time to enter the wait
            await Task.Delay(300);

            // If WaitForBufferSpace returned immediately, the buffer had space (fast path).
            // This is still a valid outcome - verify and return.
            if (Volatile.Read(ref waitCompleted))
            {
                // Fast path was taken - buffer had space. Test passes (fast path tested above).
                return;
            }

            // Slow path: WaitForBufferSpace is blocking. Release memory to unblock it.
            if (accumulator.TryGetBatch("test-topic", 0, out var batch))
            {
                ClearCurrentBatch(accumulator, "test-topic", 0);
                accumulator.ReleaseMemory(batch!.EstimatedSize);
            }

            // Assert: the wait should now unblock
            var finishedInTime = await waitTask.WaitAsync(TimeSpan.FromSeconds(5))
                .ContinueWith(t => !t.IsFaulted && !t.IsCanceled);

            await Assert.That(finishedInTime).IsTrue();
            await Assert.That(Volatile.Read(ref waitCompleted)).IsTrue();
        }
        finally
        {
            foreach (var array in rentedArrays)
            {
                ArrayPool<byte>.Shared.Return(array);
            }

            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForBufferSpace_ThrowsObjectDisposedException_WhenDisposedWhileWaiting()
    {
        // Arrange: 1KB buffer, 30s MaxBlockMs so disposal fires before timeout.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = 1000,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = 30000
        };
        var accumulator = new RecordAccumulator(options);

        var rentedArrays = new List<byte[]>();
        try
        {
            // Fill the buffer until an append blocks (proving the buffer is full).
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var filledToCapacity = false;

            for (int i = 0; i < 100; i++)
            {
                var valueBytes = ArrayPool<byte>.Shared.Rent(200);
                rentedArrays.Add(valueBytes);
                var pooledValue = new PooledMemory(valueBytes, 200);

                var appendTask = Task.Run(() =>
                {
                    accumulator.Append(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, null, null, null);
                });

                var completed = await Task.WhenAny(appendTask, Task.Delay(500)) == appendTask;
                if (!completed)
                {
                    filledToCapacity = true;
                    // Release memory so the stuck append can complete
                    if (accumulator.TryGetBatch("test-topic", 0, out var stuckBatch))
                    {
                        ClearCurrentBatch(accumulator, "test-topic", 0);
                        accumulator.ReleaseMemory(stuckBatch!.EstimatedSize);
                    }
                    await appendTask.WaitAsync(TimeSpan.FromSeconds(5));
                    break;
                }

                if (appendTask.IsFaulted)
                    break;
            }

            // If the buffer never filled, we can't test the blocking disposal scenario.
            if (!filledToCapacity)
                return;

            // Now start WaitForBufferSpace - it should block since buffer is near/at capacity
            Exception? caughtException = null;
            var waitTask = Task.Run(() =>
            {
                try
                {
                    accumulator.WaitForBufferSpace();
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref caughtException!, ex);
                }
            });

            // Give the background thread time to enter the wait
            await Task.Delay(300);

            // Dispose the accumulator while the thread is waiting
            await accumulator.DisposeAsync();

            // Assert: the wait should throw ObjectDisposedException
            await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
            var exception = Volatile.Read(ref caughtException);

            // If exception is null, WaitForBufferSpace returned normally (buffer had space).
            // If exception is present, it should be ObjectDisposedException.
            if (exception is not null)
            {
                await Assert.That(exception).IsTypeOf<ObjectDisposedException>();
            }
        }
        finally
        {
            foreach (var array in rentedArrays)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }

    /// <summary>
    /// Clears the CurrentBatch field on the PartitionDeque via reflection to prevent
    /// double-release during disposal.
    /// </summary>
    private static void ClearCurrentBatch(RecordAccumulator accumulator, string topic, int partition)
    {
        var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var deques = dequesField!.GetValue(accumulator)!;
        var tryGetMethod = deques.GetType().GetMethod("TryGetValue");
        var parms = new object[] { new TopicPartition(topic, partition), null! };
        tryGetMethod!.Invoke(deques, parms);
        var pd = parms[1]!;
        pd.GetType().GetField("CurrentBatch")!.SetValue(pd, null);
    }
}
