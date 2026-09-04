using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Surplus native returns above the 256-buffer retention ceiling. Keep the retained set
/// full and allocate the incoming native pointer directly, excluding the separate
/// NativeResponseBuffer wrapper pool's own 256-object ceiling from this measurement.
/// </summary>
[MemoryDiagnoser]
public class ResponseBufferOverflowBenchmarks
{
    private const int RetainedBuffers = 256;
    private const int FrameBytes = 128 * 1024;
    private ResponseBufferPool _pool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ResponseBufferPool(ResponseBufferPool.DefaultMaxArrayLength,
            managedArraysPerBucket: 256, maxRetainedNativeBuffers: 256);
        for (var i = 0; i < RetainedBuffers; i++)
            _pool.ReturnNative(Marshal.AllocHGlobal(FrameBytes), FrameBytes);
    }

    [Benchmark(Baseline = true)]
    public void AllocateAndFree() => Marshal.FreeHGlobal(Marshal.AllocHGlobal(FrameBytes));

    [Benchmark]
    public void SurplusReturn() => _pool.ReturnNative(Marshal.AllocHGlobal(FrameBytes), FrameBytes);

    [GlobalCleanup]
    public void Cleanup() => _pool.TrimNativeBuffers();
}

/// <summary>
/// Measures <see cref="ResponseBufferPool"/> rent/return when a wave of live response frames
/// (prefetch depth x brokers x connections) is released by the application and immediately
/// refilled by the receive loops. A pool shallower than the wave frees the surplus on return
/// and re-allocates it on rent: for native frames that is <c>AllocHGlobal</c>/<c>FreeHGlobal</c>
/// plus first-touch page faults (visible as time), for managed frames a fresh array (visible
/// in <c>Allocated</c>). See the <see cref="ResponseBufferPool"/> remarks for the sizing rule.
/// </summary>
[MemoryDiagnoser]
public class ResponseBufferPoolBenchmarks
{
    private const int LiveResponses = 16;
    private const int CeilingWaveResponses = 256;
    private const int FetchFrameBytes = 1024 * 1024;
    private const int CaughtUpFrameBytes = 256 * 1024;
    private const int CeilingWaveFrameBytes = 128 * 1024;
    private const int ProduceFrameBytes = 8 * 1024;
    private const int PageBytes = 4096;

    // 4 = ResponseBufferPool.Default; 20 = PoolSizing.ForConsumerResponseBuffers(1 broker, depth 3, 4 connections);
    // 256 = PoolSizing.MaxResponseBuffers, the retention ceiling every native bucket allocates slots for.
    [Params(4, 20, 256)]
    public int Depth { get; set; }

    private ResponseBufferPool _pool = null!;
    private ResponseBufferPool _grownPool = null!;
    private ResponseBufferPool _ceilingWavePool = null!;
    private NativeResponseBuffer[] _nativeLive = null!;
    private NativeResponseBuffer[] _grownLive = null!;
    private NativeResponseBuffer[] _ceilingWaveLive = null!;
    private byte[][] _managedLive = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Direct construction keeps benchmark pools out of the process-wide shared-pool cache
        // and disables the once-per-second memory-pressure sample so only pool mechanics are timed.
        _pool = CreatePool();
        _nativeLive = new NativeResponseBuffer[LiveResponses];
        _managedLive = new byte[LiveResponses][];
        for (var i = 0; i < LiveResponses; i++)
        {
            _nativeLive[i] = _pool.RentNative(FetchFrameBytes);
            _managedLive[i] = _pool.Pool.Rent(ProduceFrameBytes);
        }

        // A consumer that was caught up (256 KB frames) and then fell behind (1 MB frames):
        // the caught-up frames sit dormant in their own capacity bucket while the live
        // working set has moved to the larger one.
        _grownPool = CreatePool();
        _grownLive = new NativeResponseBuffer[LiveResponses];
        for (var i = 0; i < LiveResponses; i++)
            _grownLive[i] = _grownPool.RentNative(CaughtUpFrameBytes);
        for (var i = 0; i < LiveResponses; i++)
            _grownLive[i].Return();
        for (var i = 0; i < LiveResponses; i++)
            _grownLive[i] = _grownPool.RentNative(FetchFrameBytes);

        // A wave the size of the retention ceiling, so a covering pool's bucket sweeps its
        // whole slot range every invocation.
        _ceilingWavePool = CreatePool();
        _ceilingWaveLive = new NativeResponseBuffer[CeilingWaveResponses];
        for (var i = 0; i < CeilingWaveResponses; i++)
            _ceilingWaveLive[i] = _ceilingWavePool.RentNative(CeilingWaveFrameBytes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (var i = 0; i < LiveResponses; i++)
        {
            _nativeLive[i].Return();
            _grownLive[i].Return();
            _pool.Pool.Return(_managedLive[i]);
        }

        for (var i = 0; i < CeilingWaveResponses; i++)
            _ceilingWaveLive[i].Return();

        _pool.TrimNativeBuffers();
        _grownPool.TrimNativeBuffers();
        _ceilingWavePool.TrimNativeBuffers();
    }

    /// <summary>
    /// Page-touch cost alone on frames that stay live, so the Ratio column of the other
    /// native rows isolates what the pool adds.
    /// </summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = LiveResponses)]
    public void TouchLiveFrames_1MB()
    {
        for (var i = 0; i < LiveResponses; i++)
            TouchPages(_nativeLive[i].GetSpan());
    }

    /// <summary>Fetch-sized frames through the native path.</summary>
    [Benchmark(OperationsPerInvoke = LiveResponses)]
    public void NativeFetchFrames_1MB() => ReturnAndRefillWave(_pool, _nativeLive, FetchFrameBytes);

    /// <summary>
    /// Same wave on a pool whose allowance is partly held by dormant frames of the previous,
    /// smaller capacity: shows whether the retained set follows the live frame size.
    /// </summary>
    [Benchmark(OperationsPerInvoke = LiveResponses)]
    public void NativeFetchFrames_1MB_AfterFrameSizeGrowth() => ReturnAndRefillWave(_grownPool, _grownLive, FetchFrameBytes);

    /// <summary>
    /// A wave the size of the retention ceiling (256 frames) released and refilled. On a
    /// covering pool the bucket's occupancy sweeps its entire slot range twice per invocation,
    /// which is where a slot scan degrades to O(depth) per frame; the per-frame time here
    /// should match the 16-frame rows at the same depth.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CeilingWaveResponses)]
    public void NativeCeilingWave_128KB() => ReturnAndRefillWave(_ceilingWavePool, _ceilingWaveLive, CeilingWaveFrameBytes);

    /// <summary>Produce-response-sized frames through the managed array pool.</summary>
    [Benchmark(OperationsPerInvoke = LiveResponses)]
    public void ManagedProduceFrames_8KB()
    {
        for (var i = 0; i < LiveResponses; i++)
            _pool.Pool.Return(_managedLive[i]);

        for (var i = 0; i < LiveResponses; i++)
        {
            var array = _pool.Pool.Rent(ProduceFrameBytes);
            array[0] = 1;
            _managedLive[i] = array;
        }
    }

    private ResponseBufferPool CreatePool() => new(
        ResponseBufferPool.DefaultMaxArrayLength,
        managedArraysPerBucket: Depth,
        maxRetainedNativeBuffers: Depth);

    /// <summary>
    /// Returns every live frame, then re-rents the wave. Each rented frame's pages are
    /// touched once, as a socket receive would, so a freshly allocated buffer pays its
    /// page faults here.
    /// </summary>
    private static void ReturnAndRefillWave(ResponseBufferPool pool, NativeResponseBuffer[] live, int frameBytes)
    {
        for (var i = 0; i < live.Length; i++)
            live[i].Return();

        for (var i = 0; i < live.Length; i++)
        {
            var buffer = pool.RentNative(frameBytes);
            TouchPages(buffer.GetSpan());
            live[i] = buffer;
        }
    }

    private static void TouchPages(Span<byte> span)
    {
        for (var offset = 0; offset < span.Length; offset += PageBytes)
            span[offset] = 1;
    }
}
