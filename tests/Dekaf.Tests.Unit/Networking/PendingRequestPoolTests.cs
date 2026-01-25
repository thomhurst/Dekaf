using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class PendingRequestPoolTests
{
    [Test]
    public async Task Rent_ReturnsNewInstance_WhenPoolEmpty()
    {
        var pool = new PendingRequestPool();

        var request = pool.Rent();

        await Assert.That(request).IsNotNull();
    }

    [Test]
    public async Task Return_AddsToPool_ForReuse()
    {
        var pool = new PendingRequestPool();
        var request1 = pool.Rent();
        request1.Initialize(0, CancellationToken.None);

        // Complete and return
        var testData = new byte[] { 0, 0, 0, 1, 10 };
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request1.TryComplete(buffer);
        await request1.AsValueTask().ConfigureAwait(false);
        pool.Return(request1);

        // Rent again should return same instance
        var request2 = pool.Rent();

        await Assert.That(request2).IsSameReferenceAs(request1);
    }

    [Test]
    public async Task ReturnedInstance_CanBeReinitializedAndUsed()
    {
        var pool = new PendingRequestPool();

        // First use
        var request = pool.Rent();
        request.Initialize(0, CancellationToken.None);
        var testData1 = new byte[] { 0, 0, 0, 1, 42 };
        var buffer1 = new PooledResponseBuffer(testData1, testData1.Length, isPooled: false);
        request.TryComplete(buffer1);
        var result1 = await request.AsValueTask().ConfigureAwait(false);
        pool.Return(request);

        // Second use
        var sameRequest = pool.Rent();
        sameRequest.Initialize(0, CancellationToken.None);
        var testData2 = new byte[] { 0, 0, 0, 2, 99 };
        var buffer2 = new PooledResponseBuffer(testData2, testData2.Length, isPooled: false);
        sameRequest.TryComplete(buffer2);
        var result2 = await sameRequest.AsValueTask().ConfigureAwait(false);

        await Assert.That(result1.Data.Span[0]).IsEqualTo((byte)42);
        await Assert.That(result2.Data.Span[0]).IsEqualTo((byte)99);

        pool.Return(sameRequest);
    }

    [Test]
    public async Task Pool_LimitsSize_To256()
    {
        var pool = new PendingRequestPool();

        // Rent many instances
        var instances = new List<PooledPendingRequest>();
        for (int i = 0; i < 300; i++)
        {
            instances.Add(pool.Rent());
        }

        // Return all
        foreach (var request in instances)
        {
            request.Initialize(0, CancellationToken.None);
            var testData = new byte[] { 0, 0, 0, 1, 10 };
            var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
            request.TryComplete(buffer);
            await request.AsValueTask().ConfigureAwait(false);
            pool.Return(request);
        }

        // Rent back and count reused
        var reused = new HashSet<PooledPendingRequest>();
        for (int i = 0; i < 300; i++)
        {
            var request = pool.Rent();
            if (instances.Contains(request))
            {
                reused.Add(request);
            }
            request.Initialize(0, CancellationToken.None);
            var testData = new byte[] { 0, 0, 0, 1, 10 };
            var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
            request.TryComplete(buffer);
            await request.AsValueTask().ConfigureAwait(false);
            pool.Return(request);
        }

        // Should have pooled at most 256
        await Assert.That(reused.Count).IsLessThanOrEqualTo(256);
    }

    [Test]
    public async Task ConcurrentRentReturn_IsThreadSafe()
    {
        var pool = new PendingRequestPool();
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < 20; j++)
                    {
                        var request = pool.Rent();
                        request.Initialize(0, CancellationToken.None);
                        var testData = new byte[] { 0, 0, 0, 1, (byte)(j % 256) };
                        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
                        request.TryComplete(buffer);
                        await request.AsValueTask().ConfigureAwait(false);
                        pool.Return(request);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Return_ResetsRequestState()
    {
        var pool = new PendingRequestPool();
        var request = pool.Rent();

        // Set up cancellation and complete
        var cts = new CancellationTokenSource();
        request.Initialize(1, cts.Token);

        var testData = new byte[] { 0, 0, 0, 1, 0, 42 }; // Header version 1 with tag count = 0
        var buffer = new PooledResponseBuffer(testData, testData.Length, isPooled: false);
        request.TryComplete(buffer);
        await request.AsValueTask().ConfigureAwait(false);

        // Return to pool
        pool.Return(request);

        // Get back and verify it's clean
        var sameRequest = pool.Rent();
        await Assert.That(sameRequest).IsSameReferenceAs(request);

        // Should be able to reinitialize with different settings
        sameRequest.Initialize(0, CancellationToken.None);
        var testData2 = new byte[] { 0, 0, 0, 2, 99 };
        var buffer2 = new PooledResponseBuffer(testData2, testData2.Length, isPooled: false);
        sameRequest.TryComplete(buffer2);
        var result = await sameRequest.AsValueTask().ConfigureAwait(false);

        await Assert.That(result.Data.Span[0]).IsEqualTo((byte)99);

        pool.Return(sameRequest);
    }
}
