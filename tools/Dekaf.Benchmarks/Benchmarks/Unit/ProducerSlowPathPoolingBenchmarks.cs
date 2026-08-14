using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the suspended FireAsync append tails used when BufferMemory backpressure makes
/// accumulator append incomplete.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProducerSlowPathPoolingBenchmarks
{
    private const string Topic = "producer-slow-path-pooling";

    private static readonly Action<RecordMetadata, Exception?> DeliveryHandler = static (_, _) => { };

    private readonly DeferredBoolSource _source = new();
    private FinishDelegate _finish = null!;
    private FinishWithCallbackDelegate _finishWithCallback = null!;

    private delegate ValueTask FinishDelegate(ValueTask<bool> appendResult, Activity? activity, string topic);
    private delegate ValueTask FinishWithCallbackDelegate(
        ValueTask<bool> appendResult,
        Action<RecordMetadata, Exception?> deliveryHandler);

    [GlobalSetup]
    public void Setup()
    {
        var producerType = typeof(KafkaProducer<string, string>);
        var producer = (KafkaProducer<string, string>)RuntimeHelpers.GetUninitializedObject(producerType);

        _finish = producerType
            .GetMethod("FinishProduceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<FinishDelegate>(producer);
        _finishWithCallback = producerType
            .GetMethod("FinishProduceAsyncWithCallback", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<FinishWithCallbackDelegate>(producer);
    }

    [Benchmark]
    public ValueTask FinishProduceAsync() =>
        _finish(_source.QueueResult(), activity: null, Topic);

    [Benchmark]
    public ValueTask FinishProduceAsyncWithCallback() =>
        _finishWithCallback(_source.QueueResult(), DeliveryHandler);

    private sealed class DeferredBoolSource : IValueTaskSource<bool>, IThreadPoolWorkItem
    {
        private ManualResetValueTaskSourceCore<bool> _core;

        public ValueTask<bool> QueueResult()
        {
            _core.Reset();
            ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
            return new ValueTask<bool>(this, _core.Version);
        }

        void IThreadPoolWorkItem.Execute() => _core.SetResult(true);

        public bool GetResult(short token) => _core.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);
    }
}
