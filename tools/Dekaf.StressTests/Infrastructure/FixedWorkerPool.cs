using System.Globalization;
using System.Text.Json;

namespace Dekaf.StressTests.Infrastructure;

/// <summary>Establishes the explicitly requested worker pool before workload warmup.</summary>
internal static class FixedWorkerPool
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    internal static int Initialize(string outputPath)
    {
        var configured = Environment.GetEnvironmentVariable("DEKAF_STRESS_FIXED_WORKER_THREADS");
        if (string.IsNullOrEmpty(configured))
            return 0;
        if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var requested)
            || requested is < 0 or > 1024)
            throw new ArgumentException("DEKAF_STRESS_FIXED_WORKER_THREADS must be an integer from 0 to 1024.");
        if (requested == 0)
            return 0;
        if (Thread.CurrentThread.IsThreadPoolThread)
            throw new InvalidOperationException("The fixed worker pool must be initialized before asynchronous startup.");

        ThreadPool.GetMinThreads(out var minimum, out _);
        ThreadPool.GetMaxThreads(out var maximum, out _);
        if (minimum != requested || maximum != requested)
            throw new InvalidOperationException($"Fixed worker limits differ: requested={requested}, min={minimum}, max={maximum}.");
        if (Environment.GetEnvironmentVariable("DOTNET_ThreadPool_ThreadTimeoutMs") != "-1")
            throw new InvalidOperationException("Fixed workers require DOTNET_ThreadPool_ThreadTimeoutMs=-1 to prevent idle retirement.");

        var startup = new StartupState(requested);
        var queued = 0;
        var callbacksFinished = false;
        try
        {
            for (; queued < requested; queued++)
            {
                if (!ThreadPool.UnsafeQueueUserWorkItem(static state =>
                {
                    try
                    {
                        state.Ready.Signal();
                        state.Release.Wait();
                    }
                    finally
                    {
                        state.Finished.Signal();
                    }
                }, startup, preferLocal: false))
                    throw new InvalidOperationException("Could not queue fixed-worker startup.");
            }
            if (!startup.Ready.Wait(StartupTimeout))
                throw new TimeoutException("Could not start every configured worker before workload warmup.");
        }
        finally
        {
            if (queued < requested)
                startup.Finished.Signal(requested - queued);
            startup.Release.Set();
            // Observe every queued callback before disposing its synchronization state.
            // On timeout the callbacks retain the state until process failure/cleanup.
            callbacksFinished = startup.Finished.Wait(StartupTimeout);
            if (callbacksFinished)
                startup.Dispose();
        }
        if (!callbacksFinished)
            throw new TimeoutException("Fixed-worker startup callbacks did not finish.");

        VerifyWorkerCount(requested);
        var snapshot = new
        {
            requestedWorkers = requested,
            minimumWorkers = minimum,
            maximumWorkers = maximum,
            liveWorkers = ThreadPool.ThreadCount,
            idleTimeoutMilliseconds = -1,
            processId = Environment.ProcessId,
            capturedAtUtc = DateTimeOffset.UtcNow
        };
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, $"fixed-worker-pool-{Environment.ProcessId}.json"),
            JsonSerializer.Serialize(snapshot));
        Console.WriteLine($"Fixed worker pool ready: requested={requested}, min={minimum}, max={maximum}, live={snapshot.liveWorkers}, idle_timeout_ms=-1");
        return requested;
    }

    internal static void VerifyWorkerCount(int requested)
    {
        if (ThreadPool.ThreadCount != requested)
            throw new InvalidOperationException($"Fixed worker count differs: requested={requested}, live={ThreadPool.ThreadCount}.");
    }

    private sealed class StartupState(int count) : IDisposable
    {
        internal readonly CountdownEvent Ready = new(count);
        internal readonly ManualResetEventSlim Release = new(false);
        internal readonly CountdownEvent Finished = new(count);

        public void Dispose()
        {
            Ready.Dispose();
            Release.Dispose();
            Finished.Dispose();
        }
    }
}
