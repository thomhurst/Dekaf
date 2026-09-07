using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one Drain shutdown with a full partition queue and a waiting writer.
/// Compare saved product builds; reported time and allocations cover the entire
/// shutdown, including existing offset tracking, not an individual message.
/// Single-invocation iterations are sensitive to scheduling noise and do not
/// establish broker throughput or message-tail latency acceptance.
/// </summary>
[MemoryDiagnoser]
public class PartitionedShutdownBenchmarks
{
    private const int RecordCount = 1024;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(10);
    private PartitionedConsumerRuntime<string, string> _runtime = null!;
    private PartitionLane<string, string> _lane = null!;
    private Func<ValueTask> _stop = null!;
    private Action _startDeadline = null!;
    private TaskCompletionSource _release = null!;
    private Task<bool> _backpressure = null!;
    private Exception? _processorFailure;
    private bool _writerAccepted;
    private int _processed;

    [IterationSetup]
    public void Setup()
    {
        _processed = 0;
        _processorFailure = null;
        _writerAccepted = true;
        _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // UserManaged shutdown never calls the consumer. Reflection binds the real
        // shutdown path in setup without adding benchmark-only product visibility.
        _runtime = new(null!, static (_, _) => default, new PartitionedProcessingOptions
        {
            CommitPolicy = PartitionCommitPolicy.UserManaged,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = SafetyTimeout
        }, null);
        var runtimeType = _runtime.GetType();
        _stop = runtimeType.GetMethod("StopAllBoundedAsync", PrivateInstance)!
            .CreateDelegate<Func<ValueTask>>(_runtime);
        _startDeadline = runtimeType.GetMethod("StopHandlerCommits", PrivateInstance)!
            .CreateDelegate<Action>(_runtime);
        var partition = new TopicPartition("shutdown", 0);
        _lane = new(partition, RecordCount, static (_, _) => default, static _ => { },
            (_, error) => _processorFailure = error);
        var lanes = (Dictionary<TopicPartition, PartitionLane<string, string>>)
            runtimeType.GetField("_lanes", PrivateInstance)!.GetValue(_runtime)!;
        lanes.Add(partition, _lane);
        for (var index = 0; index < RecordCount; index++)
        {
            var record = new ConsumeResult<string, string>("shutdown", 0, index,
                default, true, default, true, null, 0, TimestampType.NotAvailable, null, null, null);
            if (!_lane.TryEnqueue(record))
                throw new InvalidOperationException("Queue fill failed.");
        }

        _backpressure = _lane.WaitToWriteAsync(CancellationToken.None).AsTask();
        if (_backpressure.IsCompleted || !_lane.IsFull)
            throw new InvalidOperationException("Queue is not full with a waiting writer.");

        _lane.Start(async (context, token) =>
        {
            started.TrySetResult();
            await _release.Task.WaitAsync(token);
            await foreach (var record in context.Messages.WithCancellation(token))
            {
                if (record.Offset != _processed++)
                    throw new InvalidOperationException("Record order changed.");
                context.MarkProcessed(record);
            }
        });
        started.Task.WaitAsync(SafetyTimeout).GetAwaiter().GetResult();
    }

    [Benchmark]
    public async ValueTask DrainFullQueue()
    {
        _startDeadline();
        var stopping = _stop();
        _release.TrySetResult();
        await stopping;
        _writerAccepted = await _backpressure;
    }

    [IterationCleanup]
    public void Cleanup()
    {
        try
        {
            // Also release and observe the handler if the measured operation failed.
            _release.TrySetResult();
            var failure = _lane.StopAsync(PartitionStopPolicy.Cancel, SafetyTimeout)
                .AsTask().GetAwaiter().GetResult();
            _backpressure.GetAwaiter().GetResult();
            if (failure is not null || _processorFailure is not null)
                throw new InvalidOperationException("Partition processor failed.", failure ?? _processorFailure);
            if (_writerAccepted || _processed != RecordCount
                || _lane.GetCommitOffset()?.Offset != RecordCount || _lane.IsFull)
                throw new InvalidOperationException("Incomplete drain, checkpoint, or writer rejection.");
        }
        finally
        {
            foreach (var name in new[] { "_shutdownCancellation", "_restartCancellation" })
                ((CancellationTokenSource)_runtime.GetType().GetField(name, PrivateInstance)!
                    .GetValue(_runtime)!).Dispose();
            (_runtime.GetType().GetField("_capacitySignal", PrivateInstance)!
                .GetValue(_runtime) as IDisposable)?.Dispose();
        }
    }
}
