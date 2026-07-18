using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class ProduceAllCompletionTests
{
    private static RecordMetadata Metadata(int offset) => new()
    {
        Topic = "test-topic",
        Partition = 0,
        Offset = offset,
        Timestamp = DateTimeOffset.UnixEpoch
    };

    [Test]
    public async Task AllSynchronousCompletions_ReturnsResultsInOrder()
    {
        var completion = ProduceAllCompletion.Rent(3);

        for (var i = 0; i < 3; i++)
        {
            completion.Register(i, new ValueTask<RecordMetadata>(Metadata(i)));
        }

        var results = await completion.WaitAsync();

        await Assert.That(results.Length).IsEqualTo(3);
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(results[i].Offset).IsEqualTo(i);
        }
    }

    [Test]
    public async Task AsynchronousCompletions_OutOfOrder_ResultsInInputOrder()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = ProduceAllCompletion.Rent(3);

        var sources = new PooledValueTaskSource<RecordMetadata>[3];
        for (var i = 0; i < 3; i++)
        {
            sources[i] = pool.Rent();
            completion.Register(i, sources[i].Task);
        }

        var aggregate = completion.WaitAsync();

        // Complete in reverse order
        sources[2].SetResult(Metadata(2));
        sources[1].SetResult(Metadata(1));
        sources[0].SetResult(Metadata(0));

        var results = await aggregate;

        await Assert.That(results.Length).IsEqualTo(3);
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(results[i].Offset).IsEqualTo(i);
        }
    }

    [Test]
    public async Task MixedSyncAndAsyncCompletions_ResultsInInputOrder()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = ProduceAllCompletion.Rent(4);

        var pendingA = pool.Rent();
        var pendingB = pool.Rent();

        completion.Register(0, new ValueTask<RecordMetadata>(Metadata(0)));
        completion.Register(1, pendingA.Task);
        completion.Register(2, new ValueTask<RecordMetadata>(Metadata(2)));
        completion.Register(3, pendingB.Task);

        var aggregate = completion.WaitAsync();

        pendingB.SetResult(Metadata(3));
        pendingA.SetResult(Metadata(1));

        var results = await aggregate;

        for (var i = 0; i < 4; i++)
        {
            await Assert.That(results[i].Offset).IsEqualTo(i);
        }
    }

    [Test]
    public async Task Failure_SmallestIndexWins()
    {
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = ProduceAllCompletion.Rent(3);

        var sources = new PooledValueTaskSource<RecordMetadata>[3];
        for (var i = 0; i < 3; i++)
        {
            sources[i] = pool.Rent();
            completion.Register(i, sources[i].Task);
        }

        var aggregate = completion.WaitAsync();

        // Later-index failure completes first; earlier index must still win.
        sources[2].SetException(new InvalidOperationException("index-2"));
        sources[1].SetException(new InvalidOperationException("index-1"));
        sources[0].SetResult(Metadata(0));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await aggregate);
        await Assert.That(thrown!.Message).IsEqualTo("index-1");
    }

    [Test]
    public async Task RecordFailure_SynchronousThrow_SurfacesFromAggregate()
    {
        var completion = ProduceAllCompletion.Rent(2);

        completion.Register(0, new ValueTask<RecordMetadata>(Metadata(0)));
        completion.RecordFailure(1, new ObjectDisposedException("producer"));
        completion.AbortRegistration(1);

        var thrown = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await completion.WaitAsync());
        await Assert.That(thrown).IsNotNull();
    }

    [Test]
    public async Task AbortRegistration_MidLoop_WaitsForInFlightThenFaults()
    {
        // Simulates a sync produce throw at index 1 of 4: index 0 is still in flight,
        // indices 1-3 never register. The aggregate must wait for index 0, then fault.
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var inFlight = pool.Rent();

        var completion = ProduceAllCompletion.Rent(4);
        completion.Register(0, inFlight.Task);
        completion.RecordFailure(1, new InvalidOperationException("sync-throw"));
        completion.AbortRegistration(3);

        var aggregate = completion.WaitAsync();
        await Assert.That(aggregate.IsCompleted).IsFalse();

        inFlight.SetResult(Metadata(0));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await aggregate);
        await Assert.That(thrown!.Message).IsEqualTo("sync-throw");
    }

    [Test]
    public async Task PooledReuse_SecondUseAfterFirstAwait_Works()
    {
        var first = ProduceAllCompletion.Rent(1);
        first.Register(0, new ValueTask<RecordMetadata>(Metadata(7)));
        var firstResults = await first.WaitAsync();
        await Assert.That(firstResults[0].Offset).IsEqualTo(7);

        // The pool should hand back the same reset instance; either way it must behave freshly.
        var second = ProduceAllCompletion.Rent(2);
        second.Register(0, new ValueTask<RecordMetadata>(Metadata(10)));
        second.Register(1, new ValueTask<RecordMetadata>(Metadata(11)));
        var secondResults = await second.WaitAsync();

        await Assert.That(secondResults.Length).IsEqualTo(2);
        await Assert.That(secondResults[0].Offset).IsEqualTo(10);
        await Assert.That(secondResults[1].Offset).IsEqualTo(11);
    }

    [Test]
    public async Task ManyConcurrentCompletions_AllHarvested()
    {
        const int count = 1000;
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = ProduceAllCompletion.Rent(count);

        var sources = new PooledValueTaskSource<RecordMetadata>[count];
        for (var i = 0; i < count; i++)
        {
            sources[i] = pool.Rent();
            completion.Register(i, sources[i].Task);
        }

        var aggregate = completion.WaitAsync();

        Parallel.For(0, count, i => sources[i].SetResult(Metadata(i)));

        var results = await aggregate;

        await Assert.That(results.Length).IsEqualTo(count);
        for (var i = 0; i < count; i++)
        {
            await Assert.That(results[i].Offset).IsEqualTo(i);
        }
    }

    [Test]
    public async Task InlineContinuations_CompleteOnSettingThread()
    {
        // ProduceAllAsync rents per-message sources with RunContinuationsAsynchronously = false;
        // the harvest continuation must complete the aggregate synchronously on the acking thread.
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var source = pool.Rent();
        source.SetRunContinuationsAsynchronously(false);

        var completion = ProduceAllCompletion.Rent(1);
        completion.Register(0, source.Task);
        var aggregate = completion.WaitAsync();

        source.SetResult(Metadata(1));

        // No other thread involved: the aggregate must already be completed.
        await Assert.That(aggregate.IsCompleted).IsTrue();
        var results = await aggregate;
        await Assert.That(results[0].Offset).IsEqualTo(1);
    }
}
