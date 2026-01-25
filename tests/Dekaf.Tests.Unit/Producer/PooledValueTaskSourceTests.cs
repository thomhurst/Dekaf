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

        RecordMetadata? receivedMetadata = null;
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

        await Assert.That(receivedMetadata).IsNotNull();
        await Assert.That(receivedMetadata!.Topic).IsEqualTo("test");
        await Assert.That(receivedMetadata.Offset).IsEqualTo(42);
        await Assert.That(receivedException).IsNull();
    }

    [Test]
    public async Task DeliveryHandler_InvokedOnException()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var source = pool.Rent();

        RecordMetadata? receivedMetadata = null;
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

        await Assert.That(receivedMetadata).IsNull();
        await Assert.That(receivedException).IsNotNull();
        await Assert.That(receivedException).IsSameReferenceAs(expectedException);
    }

    [Test]
    public async Task DeliveryHandler_ClearedAfterInvocation()
    {
        // Note: Delivery handler is only invoked for RecordMetadata type
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
}
