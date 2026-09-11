using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Models an assignment arriving after a cohort of consumers begins waiting, plus
/// unchanged empty heartbeat assignments. The 1 ms broker wait keeps the old timer
/// path measurable; it is not a proposal to change the production 200 ms default.
/// One operation covers the full cohort, including assignment processing and delivery.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerIdleAssignmentBenchmarks
{
    private ShareConsumerPollBenchmarks[] _consumers = null!;
    private ValueTask<bool>[] _pending = null!;

    [Params(1, 64)]
    public int ConsumerCount { get; set; }

    [Params(false, true)]
    public bool Batch { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _consumers = new ShareConsumerPollBenchmarks[ConsumerCount];
        _pending = new ValueTask<bool>[ConsumerCount];
        for (var index = 0; index < _consumers.Length; index++)
        {
            var consumer = new ShareConsumerPollBenchmarks
            {
                RecordCount = 1, BatchCount = 1, IdleFetchMaxWaitMs = 1
            };
            _consumers[index] = consumer;
            await consumer.Setup();
            consumer.PrepareIdlePolling(Batch);
        }
        if (await AssignmentArrival() != ConsumerCount)
            throw new InvalidOperationException("Assignment arrival lost a consumer delivery.");
        foreach (var consumer in _consumers)
            consumer.PublishEmptyAssignment();
    }

    [Benchmark]
    public async ValueTask<int> AssignmentArrival()
    {
        for (var index = 0; index < _consumers.Length; index++)
            _pending[index] = _consumers[index].BeginIdleRound();
        foreach (var consumer in _consumers)
            consumer.PublishIdleAssignment();
        var delivered = 0;
        for (var index = 0; index < _pending.Length; index++)
        {
            if (await _pending[index])
                delivered++;
        }
        return delivered;
    }

    [Benchmark]
    public void UnchangedEmptyHeartbeat()
    {
        foreach (var consumer in _consumers)
            consumer.PublishEmptyAssignment();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        foreach (var consumer in _consumers)
            await consumer.Cleanup();
    }
}