using System.Buffers;
using System.Threading.Tasks.Sources;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class PooledPendingRequestTests
{
    [Test]
    public async Task TryComplete_WithValidBuffer_CompletesWithSlicedResult()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        // Create a test buffer with correlation ID (4 bytes) + payload
        var testData = new byte[] { 0, 0, 0, 1, 10, 20, 30, 40 }; // correlation ID = 1, payload = [10, 20, 30, 40]
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);

        var completed = request.TryComplete(buffer);

        await Assert.That(completed).IsTrue();

        var result = await request.AsValueTask().ConfigureAwait(false);
        // Result should be sliced to skip the 4-byte correlation ID
        await Assert.That(result.Length).IsEqualTo(4);
        await Assert.That(result.Data.Span[0]).IsEqualTo((byte)10);

        pool.Return(request);
    }

    [Test]
    public async Task TryComplete_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        // First completion
        var testData1 = new byte[] { 0, 0, 0, 1, 10, 20 };
        var buffer1 = new PooledResponseBuffer(testData1, testData1.Length, isPooled: false);
        var completed1 = request.TryComplete(buffer1);

        // Second completion should fail
        var testData2 = new byte[] { 0, 0, 0, 2, 30, 40 };
        var buffer2 = new PooledResponseBuffer(testData2, testData2.Length, isPooled: false);
        var completed2 = request.TryComplete(buffer2);

        await Assert.That(completed1).IsTrue();
        await Assert.That(completed2).IsFalse();

        pool.Return(request);
    }

    [Test]
    public async Task TrySetException_CompletesWithException()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var expectedException = new InvalidOperationException("Test error");
        var result = request.TrySetException(expectedException);

        await Assert.That(result).IsTrue();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await request.AsValueTask().ConfigureAwait(false);
        });

        await Assert.That(thrown!.Message).IsEqualTo("Test error");

        pool.Return(request);
    }

    [Test]
    public async Task TrySetException_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        // Complete with result first
        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request.TryComplete(buffer);

        // Exception should fail
        var result = request.TrySetException(new InvalidOperationException("Test"));

        await Assert.That(result).IsFalse();

        pool.Return(request);
    }

    [Test]
    public async Task TrySetCanceled_CompletesWithCancellation()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var result = request.TrySetCanceled();

        await Assert.That(result).IsTrue();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await request.AsValueTask().ConfigureAwait(false);
        });

        pool.Return(request);
    }

    [Test]
    public async Task TrySetCanceled_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        // Complete with result first
        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request.TryComplete(buffer);

        // Cancellation should fail
        var result = request.TrySetCanceled();

        await Assert.That(result).IsFalse();

        pool.Return(request);
    }

    [Test]
    public async Task CancellationToken_CancelsRequest()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        var cts = new CancellationTokenSource();

        request.Initialize(responseHeaderVersion: 0, cts.Token);

        // Cancel before completion
        cts.Cancel();

        // Small delay to allow cancellation registration to fire
        await Task.Delay(10).ConfigureAwait(false);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await request.AsValueTask().ConfigureAwait(false);
        });

        pool.Return(request);
    }

    [Test]
    public async Task Pool_ReusesInstances()
    {
        var pool = new PendingRequestPool();

        // Rent and return first instance
        var request1 = pool.Rent();
        request1.Initialize(responseHeaderVersion: 0, CancellationToken.None);
        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request1.TryComplete(buffer);
        await request1.AsValueTask().ConfigureAwait(false);
        pool.Return(request1);

        // Rent again - should get same instance
        var request2 = pool.Rent();

        await Assert.That(request1).IsSameReferenceAs(request2);

        // Should be usable again
        request2.Initialize(responseHeaderVersion: 0, CancellationToken.None);
        var testData2 = new byte[] { 0, 0, 0, 2, 20 };
        var buffer2 = new PooledResponseBuffer(testData2, testData2.Length, isPooled: false);
        request2.TryComplete(buffer2);
        var result = await request2.AsValueTask().ConfigureAwait(false);

        await Assert.That(result.Data.Span[0]).IsEqualTo((byte)20);

        pool.Return(request2);
    }

    [Test]
    public async Task FlexibleHeader_SkipsTaggedFields()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        // Header version >= 1 means flexible header with tagged fields
        request.Initialize(responseHeaderVersion: 1, CancellationToken.None);

        // Create buffer with:
        // - 4 bytes correlation ID
        // - 1 byte tag count (0 = no tags)
        // - payload
        var testData = new byte[] { 0, 0, 0, 1, 0, 99 }; // correlation ID = 1, tag count = 0, payload = [99]
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);

        request.TryComplete(buffer);

        var result = await request.AsValueTask().ConfigureAwait(false);
        // Should skip correlation ID (4) + tag count (1) = 5 bytes
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result.Data.Span[0]).IsEqualTo((byte)99);

        pool.Return(request);
    }

    [Test]
    public async Task ConcurrentTryComplete_OnlyOneSucceeds()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var successCount = 0;
        var barrier = new Barrier(10);
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            var value = (byte)i;
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                var testData = new byte[] { 0, 0, 0, 1, value };
                var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
                if (request.TryComplete(buffer))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(successCount).IsEqualTo(1);

        pool.Return(request);
    }

    [Test]
    public async Task ConcurrentMixedCompletion_OnlyOneSucceeds()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var successCount = 0;
        var barrier = new Barrier(4);
        var tasks = new List<Task>();

        // 2 threads try TryComplete
        for (int i = 0; i < 2; i++)
        {
            var value = (byte)i;
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                var testData = new byte[] { 0, 0, 0, 1, value };
                var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
                if (request.TryComplete(buffer))
                    Interlocked.Increment(ref successCount);
            }));
        }

        // 2 threads try TrySetException
        for (int i = 0; i < 2; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (request.TrySetException(new InvalidOperationException("test")))
                    Interlocked.Increment(ref successCount);
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(successCount).IsEqualTo(1);

        pool.Return(request);
    }

    [Test]
    public async Task GetStatus_ReturnsPending_BeforeCompletion()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var valueTask = request.AsValueTask();
        var status = ((IValueTaskSource<PooledResponseBuffer>)request).GetStatus(request.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Pending);

        // Complete the request to avoid hanging test
        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request.TryComplete(buffer);

        pool.Return(request);
    }

    [Test]
    public async Task GetStatus_ReturnsSucceeded_AfterComplete()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var valueTask = request.AsValueTask();

        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request.TryComplete(buffer);

        var status = ((IValueTaskSource<PooledResponseBuffer>)request).GetStatus(request.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Succeeded);

        pool.Return(request);
    }

    [Test]
    public async Task GetStatus_ReturnsFaulted_AfterSetException()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var valueTask = request.AsValueTask();

        request.TrySetException(new InvalidOperationException("test"));

        var status = ((IValueTaskSource<PooledResponseBuffer>)request).GetStatus(request.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Faulted);

        pool.Return(request);
    }

    [Test]
    public async Task GetStatus_ReturnsCanceled_AfterSetCanceled()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var valueTask = request.AsValueTask();

        request.TrySetCanceled();

        var status = ((IValueTaskSource<PooledResponseBuffer>)request).GetStatus(request.Version);

        await Assert.That(status).IsEqualTo(ValueTaskSourceStatus.Canceled);

        pool.Return(request);
    }

    [Test]
    public async Task RegisterCancellation_CancelsRequest()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var cts = new CancellationTokenSource();
        request.RegisterCancellation(cts.Token);

        // Cancel via registered token
        cts.Cancel();

        // Small delay to allow cancellation registration to fire
        await Task.Delay(10).ConfigureAwait(false);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await request.AsValueTask().ConfigureAwait(false);
        });

        pool.Return(request);
    }

    [Test]
    public async Task RegisterCancellation_WakesUpAwaitingTask()
    {
        // This test verifies that cancellation properly wakes up a task that's already awaiting
        // (more closely matches the integration test scenario where await happens before timeout)
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var cts = new CancellationTokenSource();
        request.RegisterCancellation(cts.Token);

        // Start awaiting BEFORE cancellation
        var awaiterStarted = new TaskCompletionSource<bool>();
        var task = Task.Run(async () =>
        {
            awaiterStarted.SetResult(true);
            await request.AsValueTask().ConfigureAwait(false);
        });

        // Wait for the task to actually start awaiting
        await awaiterStarted.Task.ConfigureAwait(false);
        await Task.Delay(50).ConfigureAwait(false); // Extra delay to ensure continuation is registered

        // Cancel while awaiting - this should wake up the task
        cts.Cancel();

        // Task should now throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);

        pool.Return(request);
    }

    [Test]
    public async Task CancelAfter_WakesUpAwaitingTask()
    {
        // This test simulates the exact pattern used in KafkaConnection.SendAsync:
        // CancelAfter schedules a timer, then we await, and timeout fires later
        var pool = new PendingRequestPool();
        var request = pool.Rent();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Short timeout for test
        request.RegisterCancellation(cts.Token);

        // Await - should be woken up when the timer fires
        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await request.AsValueTask().ConfigureAwait(false);
        });

        await Assert.That(thrown).IsNotNull();

        pool.Return(request);
    }
}
