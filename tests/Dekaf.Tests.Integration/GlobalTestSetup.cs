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
[assembly: ParallelLimiter<IntegrationTestParallelLimit>]

namespace Dekaf.Tests.Integration;

/// <summary>
/// Global test setup that runs once before all tests in the session.
/// Ensures compression codecs are registered before any test runs.
/// The codec libraries also self-register via [ModuleInitializer],
/// but this provides a reliable fallback for test execution.
/// </summary>
internal sealed class GlobalTestSetup
{
    private static LoggerFactoryFixture? _loggerFactoryFixture;

    [Before(TestSession)]
    public static void RegisterCompressionCodecs()
    {
        // Ensure the thread pool has enough threads for timer callbacks (CancelAfter, etc.)
        // to fire promptly on CI runners with limited CPUs. Without this, thread pool
        // starvation can delay CancellationTokenSource timers by hundreds of seconds,
        // causing tests to hang until the orphan sweep (360s) fires.
        ThreadPool.SetMinThreads(32, 32);

        // Enable Dekaf's internal logging (metadata resolution, prefetch errors, send loop
        // diagnostics) so CI failures include the relevant debug output. Without this,
        // errors in the prefetch loop and metadata manager are silently discarded.
        _loggerFactoryFixture = new LoggerFactoryFixture();

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

    public static ILoggerFactory GetLoggerFactory() => _loggerFactoryFixture?.LoggerFactory
        ?? throw new InvalidOperationException("Logger factory not initialized");

    [After(TestSession)]
    public static void CleanupLoggerFactory()
    {
        _loggerFactoryFixture?.Dispose();
        _loggerFactoryFixture = null;
    }
}
