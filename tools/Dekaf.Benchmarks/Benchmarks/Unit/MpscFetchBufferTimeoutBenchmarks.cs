using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 2, iterationCount: 3)]
[IterationTime(5000)]
public class MpscFetchBufferTimeoutBenchmarks
{
    private readonly MpscFetchBuffer _buffer = new(4);
    private CancellationTokenSource? _cancellationSource;
    private CancellationToken _cancellationToken;

    public enum CancellationMode
    {
        None,
        Stable,
        ResetBetweenWaits,
    }

    [ParamsAllValues]
    public CancellationMode Mode { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        if (Mode is not CancellationMode.None)
        {
            _cancellationSource = new CancellationTokenSource();
            _cancellationToken = _cancellationSource.Token;
        }

        await _buffer.WaitToReadAsync(timeoutMs: 1, _cancellationToken).ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask<bool> TimeoutAsync()
    {
        if (Mode is CancellationMode.ResetBetweenWaits)
        {
            if (!_cancellationSource!.TryReset())
                throw new InvalidOperationException("Cancellation source could not be reset.");
            _cancellationToken = _cancellationSource.Token;
        }

        return _buffer.WaitToReadAsync(timeoutMs: 1, _cancellationToken);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
        _cancellationSource?.Dispose();
    }
}

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 15)]
[IterationTime(1000)]
public class MpscFetchBufferSignalBenchmarks
{
    private readonly MpscFetchBuffer _buffer = new(4);
    private readonly PendingFetchData _item = PendingFetchData.Create(
        "benchmark-topic",
        partitionIndex: 0,
        Array.Empty<RecordBatch>());
    private readonly Action _continuation;
    private ValueTask<bool> _pendingWait;
    private Exception? _completionException;
    private bool _signaled;
    private int _completionVersion;

    public MpscFetchBufferSignalBenchmarks()
    {
        _continuation = CompletePendingWait;
    }

    [GlobalSetup]
    public void Setup() => SignalPendingWait();

    [Benchmark]
    public bool SignalPendingWait()
    {
        var expectedVersion = Volatile.Read(ref _completionVersion) + 1;
        _completionException = null;
        _pendingWait = _buffer.WaitToReadAsync(Timeout.Infinite, CancellationToken.None);
        if (_pendingWait.IsCompleted)
            throw new InvalidOperationException("Read wait did not park.");

        _pendingWait.GetAwaiter().UnsafeOnCompleted(_continuation);

        if (!_buffer.TryWrite(_item))
            throw new InvalidOperationException("Benchmark item could not be written.");

        var deadline = Stopwatch.GetTimestamp() + 5 * Stopwatch.Frequency;
        var spin = new SpinWait();
        while (Volatile.Read(ref _completionVersion) != expectedVersion)
        {
            if (Stopwatch.GetTimestamp() >= deadline)
                throw new InvalidOperationException("Read wait did not complete within five seconds.");
            spin.SpinOnce();
        }

        if (_completionException is not null)
            throw new InvalidOperationException("Read wait failed.", _completionException);

        if (!_buffer.TryRead(out var item) || !ReferenceEquals(item, _item))
            throw new InvalidOperationException("Benchmark item could not be read.");

        return _signaled;
    }

    private void CompletePendingWait()
    {
        try
        {
            _signaled = _pendingWait.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _completionException = exception;
        }
        finally
        {
            Interlocked.Increment(ref _completionVersion);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
        _item.Dispose();
    }
}
