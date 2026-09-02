using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the awaited-produce completion source lifecycle: <see cref="ValueTaskSourcePool{T}.Rent"/>
/// on the caller thread and the automatic return from GetResult after completion. The cross-thread
/// case completes and returns each source on a dedicated thread as soon as it is rented, matching
/// production where completions run on other threads while callers keep renting. No broker.
/// </summary>
[MemoryDiagnoser]
public class ValueTaskSourcePoolBenchmarks
{
    private const int SourcesPerInvoke = 1024;
    private const int PoolSize = 4096;

    private readonly ValueTaskSourcePool<int> _pool = new(PoolSize);
    private readonly PooledValueTaskSource<int>[] _ring = new PooledValueTaskSource<int>[SourcesPerInvoke];
    private Thread? _returner;
    private long _published;
    private long _consumed;
    private volatile bool _stop;

    [GlobalSetup(Target = nameof(RentReturnCrossThread))]
    public void StartReturner()
    {
        _returner = new Thread(ReturnLoop) { IsBackground = true, Name = "vts-pool-returner" };
        _returner.Start();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stop = true;
        _returner?.Join();
        _pool.DisposeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Rent and return on one thread: the uncontended cost of the pair.
    /// </summary>
    [Benchmark(OperationsPerInvoke = SourcesPerInvoke)]
    public void RentReturnSameThread()
    {
        for (var i = 0; i < SourcesPerInvoke; i++)
            _ring[i] = _pool.Rent();

        for (var i = 0; i < SourcesPerInvoke; i++)
            CompleteAndReturn(_ring[i], i);
    }

    /// <summary>
    /// Rent on the benchmark thread while a second thread completes and returns each source as it
    /// is published: the production shape, where any shared per-operation counter bounces between
    /// cores on every message.
    /// </summary>
    [Benchmark(OperationsPerInvoke = SourcesPerInvoke)]
    public void RentReturnCrossThread()
    {
        var published = _published;
        for (var i = 0; i < SourcesPerInvoke; i++)
        {
            _ring[i] = _pool.Rent();
            Volatile.Write(ref _published, ++published);
        }

        var spinner = new SpinWait();
        while (Volatile.Read(ref _consumed) != published)
            spinner.SpinOnce(sleep1Threshold: -1);
    }

    private void ReturnLoop()
    {
        var spinner = new SpinWait();
        var consumed = 0L;
        while (!_stop)
        {
            if (consumed == Volatile.Read(ref _published))
            {
                spinner.SpinOnce(sleep1Threshold: -1);
                continue;
            }

            spinner.Reset();
            CompleteAndReturn(_ring[consumed % SourcesPerInvoke], (int)consumed);
            Volatile.Write(ref _consumed, ++consumed);
        }
    }

    private static void CompleteAndReturn(PooledValueTaskSource<int> source, int result)
    {
        source.SetResult(result);
        // GetResult on the completed source resets it and returns it to the pool, exactly as the
        // awaiting caller's continuation does in production.
        if (source.Task.GetAwaiter().GetResult() != result)
            throw new InvalidOperationException("Unexpected completion result.");
    }
}
