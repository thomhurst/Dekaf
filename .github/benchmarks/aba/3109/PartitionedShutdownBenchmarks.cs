using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks;

[MemoryDiagnoser]
public class PartitionedShutdownBenchmarks
{
    private const int RecordCount = 1024;
    private PartitionedConsumerRuntime<string, string> _runtime = null!;
    private PartitionLane<string, string> _lane = null!;
    private Func<ValueTask> _stop = null!;
    private Action _startDeadline = null!;
    private TaskCompletionSource _release = null!;
    private Task<bool> _backpressure = null!;
    private int _processed;

    [IterationSetup]
    public void Setup()
    {
        _processed = 0;
        _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runtime = new(null!, static (_, _) => default, new PartitionedProcessingOptions
        {
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(10)
        }, null);
        var runtimeType = _runtime.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _stop = runtimeType.GetMethod("StopAllBoundedAsync", flags)!.CreateDelegate<Func<ValueTask>>(_runtime);
        // Main creates its deadline inside StopAllBoundedAsync; the candidate shares
        // a deadline started by StopHandlerCommits. Both are inside the measured stop.
        _startDeadline = runtimeType.GetMethod("StopHandlerCommits", flags)?.CreateDelegate<Action>(_runtime)
            ?? (static () => { });
        var partition = new TopicPartition("shutdown", 0);
        _lane = new(partition, RecordCount, static (_, _) => default, static _ => { }, static (_, error) => throw error);
        var lanes = (Dictionary<TopicPartition, PartitionLane<string, string>>)runtimeType.GetField("_lanes", flags)!.GetValue(_runtime)!;
        lanes.Add(partition, _lane);
        for (var index = 0; index < RecordCount; index++)
        {
            var record = new ConsumeResult<string, string>("shutdown", 0, index,
                default, true, default, true, null, 0, TimestampType.NotAvailable, null, null, null);
            if (!_lane.TryEnqueue(record)) throw new InvalidOperationException("Queue fill failed.");
        }
        _backpressure = _lane.WaitToWriteAsync(CancellationToken.None).AsTask();
        if (_backpressure.IsCompleted || !_lane.IsFull) throw new InvalidOperationException("Queue is not full.");
        _lane.Start(async (context, token) =>
        {
            started.TrySetResult();
            await _release.Task.WaitAsync(token);
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset != _processed++) throw new InvalidOperationException("Record order changed.");
                context.MarkProcessed(record);
            }
        });
        started.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }

    [Benchmark]
    public async ValueTask DrainFullQueue()
    {
        _startDeadline();
        var stopping = _stop();
        _release.TrySetResult();
        await stopping;
        if (await _backpressure) throw new InvalidOperationException("Stopped queue accepted a writer.");
    }

    [IterationCleanup]
    public void Cleanup()
    {
        if (_processed != RecordCount || _lane.GetCommitOffset()?.Offset != RecordCount || _lane.IsFull)
            throw new InvalidOperationException("Incomplete drain or checkpoint.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var name in new[] { "_shutdownCancellation", "_restartCancellation" })
            (_runtime.GetType().GetField(name, flags)?.GetValue(_runtime) as CancellationTokenSource)?.Dispose();
    }
}
