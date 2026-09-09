using System.Buffers;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks;

[MemoryDiagnoser]
public class PendingRequestRecoveryBenchmark
{
    private PendingRequestPool _pool = null!;
    private Reservation _reservation = null!;
    private byte[] _frame = null!;
    private long _operations;
    private long _failures;

    [Params(false, true)]
    public bool FailReset { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pool = new PendingRequestPool(1);
        _reservation = new Reservation(FailReset);
        var bytes = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(bytes);
        writer.WriteInt32(42);
        writer.WriteInt8(1);
        _frame = bytes.WrittenSpan.ToArray();
        var timer = Stopwatch.StartNew();
        var next = 1d;
        using var process = Process.GetCurrentProcess();
        do
        {
            CompleteResetAndRecover();
            if (!Program.Smoke && timer.Elapsed.TotalSeconds >= next)
            {
                Verify();
                process.Refresh();
                Console.WriteLine($"WARM seconds={timer.Elapsed.TotalSeconds:F6} completed={_operations} failures={_failures} jit={System.Runtime.JitInfo.GetCompiledMethodCount()} threads={ThreadPool.ThreadCount} cpuMs={process.TotalProcessorTime.TotalMilliseconds:F3} gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} heap={GC.GetTotalMemory(false)} rss={process.WorkingSet64}");
                next++;
            }
        } while (!Program.Smoke && timer.Elapsed.TotalSeconds < 20);
        Verify();
        Console.WriteLine($"WARM completed seconds={timer.Elapsed.TotalSeconds:F6} calls={_operations} failures={_failures}");
    }

    // One operation completes a response, returns it through reservation cleanup,
    // then completes and returns a second request to verify continued pool use.
    [Benchmark]
    public void CompleteResetAndRecover()
    {
        var request = _pool.Rent();
        Complete(request, _reservation);
        Exception? observed = null;
        try { _pool.Return(request); }
        catch (InvalidOperationException error) { observed = error; }
        if (!ReferenceEquals(observed, FailReset ? _reservation.Failure : null))
            throw new InvalidOperationException("Reset changed the expected exception contract.");
        if (observed is not null) _failures++;
        var recovered = _pool.Rent();
        if (ReferenceEquals(recovered, request) == FailReset)
            throw new InvalidOperationException("Reset failure retained the request, or successful reset lost it.");
        Complete(recovered, null);
        _pool.Return(recovered);
        _operations++;
    }

    private void Complete(PooledPendingRequest request, IResponseMemoryReservation? reservation)
    {
        request.Initialize(0, CancellationToken.None);
        var response = new PooledResponseBuffer(_frame, _frame.Length, isPooled: false);
        if (!request.TryComplete(request.Version, response, reservation))
            throw new InvalidOperationException("Request completion failed.");
        var completion = request.AsValueTask();
        if (!completion.IsCompletedSuccessfully)
            throw new InvalidOperationException("The completed response was not available synchronously.");
        using var result = completion.GetAwaiter().GetResult();
        if (result.Data.Span[0] != 1)
            throw new InvalidOperationException("Response payload changed.");
    }

    [GlobalCleanup]
    public void Verify()
    {
        if (_reservation.DisposeCalls != _operations || _failures != (FailReset ? _operations : 0))
            throw new InvalidOperationException("Reservation cleanup or failure count changed.");
        // Main's known counter defect is the subject of the fix. Both revisions
        // perform identical successful completions and recovery operations.
        var expected = Program.Baseline ? unchecked(1 - (int)_failures) : 1;
        if (_pool.ApproximateCount != expected)
            throw new InvalidOperationException($"Pool count {_pool.ApproximateCount}, expected {expected}.");
    }

    private sealed class Reservation(bool fail) : IResponseMemoryReservation
    {
        public InvalidOperationException Failure { get; } = new("Injected reservation cleanup failure.");
        public long DisposeCalls { get; private set; }
        public void Dispose()
        {
            DisposeCalls++;
            if (fail) throw Failure;
        }
    }
}
