using System.Diagnostics;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Tests.Integration;
using Microsoft.Extensions.Logging;
using TUnit.Core;
using TUnit.Core.Helpers;

[assembly: Timeout(300_000)] // 5 minutes per test — prevents indefinite hangs

namespace Dekaf.Tests.Integration;

/// <summary>
/// Global test setup that runs once before all tests in the session.
/// Ensures compression codecs are registered before any test runs.
/// The codec libraries also self-register via [ModuleInitializer],
/// but this provides a reliable fallback for test execution.
/// </summary>
internal sealed class GlobalTestSetup
{
    [Before(TestSession)]
    public static void RegisterCompressionCodecs()
    {
        // Ensure the thread pool has enough threads for timer callbacks (CancelAfter, etc.)
        // to fire promptly on CI runners with limited CPUs. Without this, thread pool
        // starvation can delay CancellationTokenSource timers by hundreds of seconds,
        // causing tests to hang until the orphan sweep (360s) fires.
        ThreadPool.SetMinThreads(32, 32);

        CompressionCodecRegistry.Default.AddLz4();
        CompressionCodecRegistry.Default.AddSnappy();
        CompressionCodecRegistry.Default.AddZstd();

#if DEBUG
        // Enable Debug.WriteLine output on Linux (where no default trace listener writes to console).
        // This makes [BatchTrack] diagnostic output from BrokerSender visible in CI logs.
        if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any())
            Trace.Listeners.Add(new ConsoleTraceListener());
#endif
    }

    /// <summary>
    /// Creates a TUnit-backed logger factory for the current test. Each call creates a new
    /// factory bound to <c>TestContext.Current</c>, so producer/consumer debug logs appear
    /// in that test's output in CI reports.
    /// </summary>
    public static ILoggerFactory GetLoggerFactory() => LoggerFactoryFixture.Create();
}
