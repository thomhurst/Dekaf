using System.Threading.Tasks.Sources;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class PooledValueTaskSourceTests
{
    [Test]
    public async Task SetResult_CompletesWithValue()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        source.SetResult(42);

        var result = await source.Task.ConfigureAwait(false);
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task SetException_ThrowsOnAwait()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var expected = new InvalidOperationException("Test error");
        source.SetException(expected);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await source.Task.ConfigureAwait(false);
        });

        await Assert.That(thrown!.Message).IsEqualTo("Test error");
    }

    [Test]
    public async Task TrySetCanceled_ThrowsOperationCanceledOnAwait()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        source.TrySetCanceled(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await source.Task.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task TrySetResult_ReturnsTrue_WhenNotCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var result = source.TrySetResult(42);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrySetResult_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();
        source.SetResult(1);

        var result = source.TrySetResult(2);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrySetException_ReturnsTrue_WhenNotCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var result = source.TrySetException(new InvalidOperationException("Test"));

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrySetException_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();
        source.SetResult(1);

        var result = source.TrySetException(new InvalidOperationException("Test"));

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrySetCanceled_ReturnsTrue_WhenNotCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var result = source.TrySetCanceled(CancellationToken.None);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrySetCanceled_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();
        source.SetResult(1);

        var result = source.TrySetCanceled(CancellationToken.None);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Source_ResetsAfterAwait_CanBeReused()
    {
        // Create a pool with max size 1 to ensure reuse
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        // First use
        var source1 = pool.Rent();
        source1.SetResult(1);
        var result1 = await source1.Task.ConfigureAwait(false);

        // After await, source should be returned to pool and reset
        // Rent again - should get same instance
        var source2 = pool.Rent();

        // Should be able to complete again with different value
        source2.SetResult(2);
        var result2 = await source2.Task.ConfigureAwait(false);

        await Assert.That(result1).IsEqualTo(1);
        await Assert.That(result2).IsEqualTo(2);
        await Assert.That(source1).IsSameReferenceAs(source2);
    }

    [Test]
    public async Task Version_ChangesAfterReset()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();
        var version1 = source.Version;

        source.SetResult(42);
        await source.Task.ConfigureAwait(false);

        // After await, the source is reset and returned to pool
        // Rent again to get the same instance
        var sameSource = pool.Rent();
        var version2 = sameSource.Version;

        await Assert.That(source).IsSameReferenceAs(sameSource);
        await Assert.That(version2).IsNotEqualTo(version1);
    }

    [Test]
    public async Task DeliveryHandler_InvokedOnSuccess()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var source = pool.Rent();

        RecordMetadata receivedMetadata = default;
        Exception? receivedException = null;

        source.SetDeliveryHandler((metadata, ex) =>
        {
            receivedMetadata = metadata;
            receivedException = ex;
        });

        var expectedMetadata = new RecordMetadata
        {
            Topic = "test",
            Partition = 0,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        };

        source.SetResult(expectedMetadata);
        await source.Task.ConfigureAwait(false);

        await Assert.That(receivedMetadata.Topic).IsEqualTo("test");
        await Assert.That(receivedMetadata.Offset).IsEqualTo(42);
        await Assert.That(receivedException).IsNull();
    }

    [Test]
    public async Task DeliveryHandler_InvokedOnException()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var source = pool.Rent();

        RecordMetadata receivedMetadata = default;
        Exception? receivedException = null;

        source.SetDeliveryHandler((metadata, ex) =>
        {
            receivedMetadata = metadata;
            receivedException = ex;
        });

        var expectedException = new InvalidOperationException("Test error");
        source.SetException(expectedException);

        try
        {
            await source.Task.ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // On exception, the handler receives default (empty struct) for the metadata
        await Assert.That(receivedMetadata.Topic).IsNull();
        await Assert.That(receivedMetadata.Offset).IsEqualTo(0);
        await Assert.That(receivedException).IsNotNull();
        await Assert.That(receivedException).IsSameReferenceAs(expectedException);
    }

    [Test]
    public async Task DeliveryHandler_ClearedAfterInvocation()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>(maxPoolSize: 1);
        var invocationCount = 0;

        var source = pool.Rent();
        source.SetDeliveryHandler((_, _) => invocationCount++);
        source.SetResult(new RecordMetadata
        {
            Topic = "test",
            Partition = 0,
            Offset = 1,
            Timestamp = DateTimeOffset.UtcNow
        });
        await source.Task.ConfigureAwait(false);

        // Handler should have been invoked once
        await Assert.That(invocationCount).IsEqualTo(1);

        // Get the same instance back
        var sameSource = pool.Rent();
        sameSource.SetResult(new RecordMetadata
        {
            Topic = "test",
            Partition = 0,
            Offset = 2,
            Timestamp = DateTimeOffset.UtcNow
        });
        await sameSource.Task.ConfigureAwait(false);

        // Handler should NOT have been invoked again (it was cleared)
        await Assert.That(invocationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Task_ReturnsValueTaskBoundToSource()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var task = source.Task;

        source.SetResult(42);
        var result = await task.ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task MultipleCompletionTypes_WorkCorrectly()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        // Success
        var source1 = pool.Rent();
        source1.SetResult(1);
        var result1 = await source1.Task.ConfigureAwait(false);
        await Assert.That(result1).IsEqualTo(1);

        // Exception (reusing same instance)
        var source2 = pool.Rent();
        source2.SetException(new InvalidOperationException("test"));
        try
        {
            await source2.Task.ConfigureAwait(false);
            Assert.Fail("Should have thrown");
        }
        catch (InvalidOperationException ex) when (ex.Message == "test")
        {
            // Expected
        }

        // Success again
        var source3 = pool.Rent();
        source3.SetResult(3);
        var result3 = await source3.Task.ConfigureAwait(false);
        await Assert.That(result3).IsEqualTo(3);
    }

    [Test]
    public async Task ConcurrentTrySetResult_OnlyOneSucceeds()
    {
        // Test that concurrent TrySetResult calls are safe and only one wins
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var successCount = 0;
        var barrier = new Barrier(10);
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var value = i;
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait(); // Ensure all threads start simultaneously
                if (source.TrySetResult(value))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Only one thread should succeed
        await Assert.That(successCount).IsEqualTo(1);

        // The task should complete with a value
        var result = await source.Task.ConfigureAwait(false);
        await Assert.That(result).IsGreaterThanOrEqualTo(0);
        await Assert.That(result).IsLessThan(10);
    }

    [Test]
    public async Task ConcurrentTrySetResult_AndTrySetException_OnlyOneSucceeds()
    {
        // Test mixed concurrent completion attempts
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        var successCount = 0;
        var barrier = new Barrier(4);
        var tasks = new List<Task>();

        // 2 threads try SetResult
        for (int i = 0; i < 2; i++)
        {
            var value = i;
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (source.TrySetResult(value))
                    Interlocked.Increment(ref successCount);
            }));
        }

        // 2 threads try SetException
        for (int i = 0; i < 2; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (source.TrySetException(new InvalidOperationException("test")))
                    Interlocked.Increment(ref successCount);
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Only one should succeed
        await Assert.That(successCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetStatus_ReturnsPending_BeforeCompletion()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        // Access via interface to test GetStatus
        var valueTaskSource = (IValueTaskSource<int>)source;
        var status = valueTaskSource.GetStatus(source.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Pending);
    }

    [Test]
    public async Task GetStatus_ReturnsSucceeded_AfterSetResult()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        source.SetResult(42);

        var valueTaskSource = (IValueTaskSource<int>)source;
        var status = valueTaskSource.GetStatus(source.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Succeeded);
    }

    [Test]
    public async Task GetStatus_ReturnsFaulted_AfterSetException()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        source.SetException(new InvalidOperationException("test"));

        var valueTaskSource = (IValueTaskSource<int>)source;
        var status = valueTaskSource.GetStatus(source.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Faulted);
    }

    [Test]
    public async Task GetStatus_ReturnsCanceled_AfterTrySetCanceled()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        source.TrySetCanceled(new CancellationToken(true));

        var valueTaskSource = (IValueTaskSource<int>)source;
        var status = valueTaskSource.GetStatus(source.Version);

        // ManualResetValueTaskSourceCore correctly detects OperationCanceledException as Canceled
        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Canceled);
    }

    [Test]
    public async Task DeliveryHandler_ExceptionInHandler_DoesNotPreventCompletion()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var source = pool.Rent();

        source.SetDeliveryHandler((_, _) => throw new InvalidOperationException("Handler error"));

        var metadata = new RecordMetadata
        {
            Topic = "test",
            Partition = 0,
            Offset = 1,
            Timestamp = DateTimeOffset.UtcNow
        };

        source.SetResult(metadata);

        // The handler exception should propagate (not swallowed)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await source.Task.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task DeliveryHandler_WorksWithNonRecordMetadataType()
    {
        // Test that generic delivery handler works with any type (not just RecordMetadata)
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        int? receivedValue = null;
        Exception? receivedException = null;

        source.SetDeliveryHandler((value, ex) =>
        {
            receivedValue = value;
            receivedException = ex;
        });

        source.SetResult(42);
        var result = await source.Task.ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(receivedValue).IsEqualTo(42);
        await Assert.That(receivedException).IsNull();
    }

    [Test]
    public async Task DeliveryHandler_WorksWithNonRecordMetadataType_OnException()
    {
        // Test that generic delivery handler works with any type on exception
        var pool = new ValueTaskSourcePool<string>();
        var source = pool.Rent();

        string? receivedValue = null;
        Exception? receivedException = null;

        source.SetDeliveryHandler((value, ex) =>
        {
            receivedValue = value;
            receivedException = ex;
        });

        var expectedException = new InvalidOperationException("Test error");
        source.SetException(expectedException);

        try
        {
            await source.Task.ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        await Assert.That(receivedValue).IsNull();
        await Assert.That(receivedException).IsSameReferenceAs(expectedException);
    }

    [Test]
    public async Task SourceWithoutPool_CompletesSuccessfully()
    {
        // Create a source directly without going through pool
        var source = new PooledValueTaskSource<int>();

        source.SetResult(42);
        var result = await source.Task.ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);
        // No pool to return to - should not throw
    }

    [Test]
    public async Task HasCompleted_ResetAfterAwait()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();

        // First completion
        await Assert.That(source.TrySetResult(1)).IsTrue();
        await source.Task.ConfigureAwait(false);

        // Get the same instance back
        var sameSource = pool.Rent();
        await Assert.That(source).IsSameReferenceAs(sameSource);

        // Should be able to complete again (hasCompleted was reset)
        await Assert.That(sameSource.TrySetResult(2)).IsTrue();
        var result = await sameSource.Task.ConfigureAwait(false);
        await Assert.That(result).IsEqualTo(2);
    }

    [Test]
    public async Task ObserveForFireAndForget_ReturnsToPool_WhenCompletedImmediately()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Complete before observing
        source.SetResult(42);
        source.ObserveForFireAndForget();

        // Give a tiny bit of time for the callback to execute
        await Task.Yield();

        // Source should have been returned to pool
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);

        // Should be able to rent the same instance again
        var sameSource = pool.Rent();
        await Assert.That(source).IsSameReferenceAs(sameSource);
    }

    [Test]
    public async Task ObserveForFireAndForget_ReturnsToPool_WhenCompletedAsynchronously()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Observe before completing (registers continuation)
        source.ObserveForFireAndForget();

        // Not yet in pool because not completed
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Complete after observing - should trigger continuation
        source.SetResult(42);

        // Give a tiny bit of time for the callback to execute
        await Task.Yield();
        await Task.Delay(1); // Small delay to ensure continuation runs

        // Source should have been returned to pool
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
    }

    [Test]
    public async Task ObserveForFireAndForget_HandlesException_WithoutThrowing()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();

        source.SetException(new InvalidOperationException("Test"));
        source.ObserveForFireAndForget();

        // Should not throw, should just return to pool
        await Task.Yield();

        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
    }

    [Test]
    public async Task ObserveForFireAndForget_InvokesDeliveryHandler()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();
        int? receivedValue = null;
        Exception? receivedException = null;

        source.SetDeliveryHandler((value, ex) =>
        {
            receivedValue = value;
            receivedException = ex;
        });

        source.SetResult(42);
        source.ObserveForFireAndForget();

        await Task.Yield();

        await Assert.That(receivedValue).IsEqualTo(42);
        await Assert.That(receivedException).IsNull();
    }

    [Test]
    public async Task ObserveForFireAndForget_InvokesDeliveryHandler_OnException()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);

        var source = pool.Rent();
        int? receivedValue = null;
        Exception? receivedException = null;

        source.SetDeliveryHandler((value, ex) =>
        {
            receivedValue = value;
            receivedException = ex;
        });

        var expectedException = new InvalidOperationException("Test");
        source.SetException(expectedException);
        source.ObserveForFireAndForget();

        await Task.Yield();

        await Assert.That(receivedValue).IsEqualTo(default(int));
        await Assert.That(receivedException).IsSameReferenceAs(expectedException);
    }
}
