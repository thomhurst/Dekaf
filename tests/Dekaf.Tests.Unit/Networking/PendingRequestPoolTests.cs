using System.Buffers;
using Dekaf.Networking;
using Dekaf.Protocol;

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
    public async Task Pool_RespectsCustomMaxPoolSize()
    {
        var pool = new PendingRequestPool(maxPoolSize: 2);

        var r1 = pool.Rent();
        var r2 = pool.Rent();
        var r3 = pool.Rent();

        // Complete all requests before returning
        r1.Initialize(0, CancellationToken.None);
        var d1 = new byte[] { 0, 0, 0, 1, 10 };
        r1.TryComplete(new PooledResponseBuffer(d1, d1.Length, isPooled: false));
        await r1.AsValueTask().ConfigureAwait(false);

        r2.Initialize(0, CancellationToken.None);
        var d2 = new byte[] { 0, 0, 0, 1, 10 };
        r2.TryComplete(new PooledResponseBuffer(d2, d2.Length, isPooled: false));
        await r2.AsValueTask().ConfigureAwait(false);

        r3.Initialize(0, CancellationToken.None);
        var d3 = new byte[] { 0, 0, 0, 1, 10 };
        r3.TryComplete(new PooledResponseBuffer(d3, d3.Length, isPooled: false));
        await r3.AsValueTask().ConfigureAwait(false);

        pool.Return(r1);
        pool.Return(r2);
        pool.Return(r3); // Should be discarded (pool full at 2)

        await Assert.That(pool.ApproximateCount).IsEqualTo(2);
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

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Return_WithReservation_BalancesCountAndPreservesResetContract(bool failReset)
    {
        var pool = new PendingRequestPool(maxPoolSize: 1);
        var request = pool.Rent();
        var reservation = new TestReservation(failReset);
        await CompleteWithReservationAsync(request, reservation);

        Exception? observed = null;
        try
        {
            pool.Return(request);
        }
        catch (InvalidOperationException exception)
        {
            observed = exception;
        }

        await Assert.That(reservation.DisposeCalls).IsEqualTo(1);
        await Assert.That(observed).IsSameReferenceAs(failReset ? reservation.Failure : null);
        await Assert.That(pool.ApproximateCount).IsEqualTo(failReset ? 0 : 1);
        var replacement = pool.Rent();
        await Assert.That(ReferenceEquals(replacement, request)).IsEqualTo(!failReset);
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
        pool.Return(replacement);
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
    }

    [Test]
    public async Task Return_Null_DoesNotChangeCount()
    {
        var pool = new PendingRequestPool(maxPoolSize: 1);
        var request = pool.Rent();
        pool.Return(request);

        await Assert.That(() => pool.Return(null!)).Throws<ArgumentNullException>();

        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
        await Assert.That(pool.Rent()).IsSameReferenceAs(request);
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentReturns_WithResetFailures_CountOnlyRetainedRequests()
    {
        const int capacity = 8;
        const int requestCount = 32;
        var pool = new PendingRequestPool(capacity);
        var requests = new PooledPendingRequest[requestCount];
        var reservations = new TestReservation[requestCount];
        for (var i = 0; i < requestCount; i++)
        {
            requests[i] = pool.Rent();
            reservations[i] = new TestReservation(failReset: i % 2 == 0);
            await CompleteWithReservationAsync(requests[i], reservations[i]);
        }

        var errors = new Exception?[requestCount];
        Parallel.For(0, requestCount, i =>
        {
            try
            {
                pool.Return(requests[i]);
            }
            catch (InvalidOperationException exception)
            {
                errors[i] = exception;
            }
        });

        for (var i = 0; i < requestCount; i++)
        {
            await Assert.That(reservations[i].DisposeCalls).IsEqualTo(1);
            await Assert.That(errors[i]).IsSameReferenceAs(i % 2 == 0 ? reservations[i].Failure : null);
        }
        await Assert.That(pool.ApproximateCount).IsEqualTo(capacity);

        var retained = new PooledPendingRequest[capacity];
        for (var i = 0; i < capacity; i++)
        {
            retained[i] = pool.Rent();
            var originalIndex = Array.IndexOf(requests, retained[i]);
            await Assert.That(originalIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(originalIndex % 2).IsEqualTo(1);
        }
        await Assert.That(new HashSet<PooledPendingRequest>(retained).Count).IsEqualTo(capacity);
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
        foreach (var request in retained)
        {
            pool.Return(request);
        }
        await Assert.That(pool.ApproximateCount).IsEqualTo(capacity);
    }

    private static async Task CompleteWithReservationAsync(
        PooledPendingRequest request, IResponseMemoryReservation reservation)
    {
        request.Initialize(0, CancellationToken.None);
        var bytes = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(bytes);
        writer.WriteInt32(42);
        writer.WriteInt8(1);
        var response = new PooledResponseBuffer(bytes.WrittenSpan.ToArray(), bytes.WrittenCount, isPooled: false);
        await Assert.That(request.TryComplete(request.Version, response, reservation)).IsTrue();
        using var result = await request.AsValueTask();
        await Assert.That(result.Data.Span[0]).IsEqualTo((byte)1);
    }

    private sealed class TestReservation(bool failReset) : IResponseMemoryReservation
    {
        public InvalidOperationException Failure { get; } = new("Injected reservation cleanup failure.");
        public int DisposeCalls { get; private set; }

        public void Dispose()
        {
            DisposeCalls++;
            if (failReset)
            {
                throw Failure;
            }
        }
    }
}
