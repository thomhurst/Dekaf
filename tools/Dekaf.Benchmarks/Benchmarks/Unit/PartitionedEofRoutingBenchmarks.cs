using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class PartitionedEofRoutingBenchmarks
{
    private Func<ConsumeBatch<string, string>, CancellationToken, ValueTask> _route = null!;
    private PendingFetchData _pending = null!;
    private ConsumeBatch<string, string> _batch = null!;

    [GlobalSetup]
    public void Setup()
    {
        var runtime = new PartitionedConsumerRuntime<string, string>(null!, static (_, _) => default,
            new PartitionedProcessingOptions { CommitPolicy = PartitionCommitPolicy.UserManaged }, null);
        var partition = new TopicPartition("eof", 0);
        var lane = new PartitionLane<string, string>(partition, 1,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        // Match the real routing path; reflection and fixture construction stay outside measurement.
        var runtimeType = runtime.GetType();
        var lanes = (Dictionary<TopicPartition, PartitionLane<string, string>>)
            runtimeType.GetField("_lanes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        lanes.Add(partition, lane);
        _route = runtimeType.GetMethod("RouteBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ConsumeBatch<string, string>, CancellationToken, ValueTask>>(runtime);
        _pending = PendingFetchData.CreatePartitionEof("eof", 0, 42);
        _batch = new ConsumeBatch<string, string>(_pending, Serializers.String, Serializers.String);
    }

    // One caught-up partition notification. EOF batches contain no records and can be reused.
    [Benchmark]
    public void RouteEofBatch() => _route(_batch, default).GetAwaiter().GetResult();

    [GlobalCleanup]
    public void Cleanup() => _pending.Dispose();
}
