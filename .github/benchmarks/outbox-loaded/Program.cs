using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Outbox;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 7)
            throw new ArgumentException("bootstrap output topic legacy|renewal off|on warmupSeconds measuredSeconds");
        var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output))
            throw new IOException("Output already exists");
        Directory.CreateDirectory(output);
        if (args[3] is not ("legacy" or "renewal") || args[4] is not ("off" or "on"))
            throw new ArgumentException("Invalid store/listener mode");
        int warmup = int.Parse(args[5]), measured = int.Parse(args[6]);
        if (warmup < 1 || measured < 1)
            throw new ArgumentOutOfRangeException(nameof(args));
        using var compilations = new CompilationLog(Path.Combine(output, "compilations.json"));
        compilations.Phase("initialize");
        Store store = args[3] == "renewal"
            ? new RenewingStore(args[2], warmup, measured, compilations)
            : new Store(args[2], warmup, measured, compilations);
        using var metrics = new MetricsObserver(args[4] == "on");
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20 + warmup + measured + 120));
        var admin = Kafka.CreateAdminClient().WithBootstrapServers(args[0]).Build();
        await using (admin)
            await admin.CreateTopicsAsync([new NewTopic { Name = args[2], NumPartitions = 3, ReplicationFactor = 1 }],
                cancellationToken: lifetime.Token);
        var producer = await Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithBootstrapServers(args[0]).WithClientId("outbox-loaded")
            .WithAcks(Acks.All).WithIdempotence(true).WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(128 * 1024).WithBufferMemory(64UL * 1024 * 1024)
            .WithConnectionsPerBroker(1).WithoutAdaptiveConnections().BuildAsync(lifetime.Token);
        var publisher = new DekafOutboxPublisher(producer);
        var options = new OutboxRelayOptions
        {
            RelayId = "outbox-loaded", BucketCount = 1, BatchSize = Store.BatchCount + 1,
            MaxPublishDuration = TimeSpan.FromSeconds(30), LeaseDuration = TimeSpan.FromSeconds(90),
            LeaseRenewInterval = TimeSpan.FromSeconds(30), PollInterval = TimeSpan.FromMilliseconds(10)
        };
        // The baseline has no telemetry options. Publication inputs are identical.
        typeof(OutboxRelayOptions).GetProperty("MetricsName")?.SetValue(options, "loaded");
        typeof(OutboxRelayOptions).GetProperty("MetricsCollectionInterval")?.SetValue(options, TimeSpan.FromSeconds(1));
        using var relay = new OutboxRelayService(store, publisher, options, new FailureLogger(store));
        using var sampling = new ManualResetEventSlim();
        var sampler = new Thread(() => store.Sample(sampling, metrics)) { IsBackground = true, Name = "outbox-observer" };
        sampler.Start();
        Exception? failure = null;
        double shutdownSeconds = 0;
        try
        {
            await relay.StartAsync(lifetime.Token);
            var completed = await Task.WhenAny(store.Finished.Task, relay.ExecuteTask!).WaitAsync(lifetime.Token);
            if (completed != store.Finished.Task)
            {
                await completed;
                throw new InvalidOperationException("Relay exited before all phases finished");
            }
            await store.Finished.Task;
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            compilations.Phase("finalize");
            var stopped = Stopwatch.StartNew();
            try
            {
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await relay.StopAsync(shutdown.Token);
                await publisher.DisposeAsync().AsTask().WaitAsync(shutdown.Token);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
            shutdownSeconds = stopped.Elapsed.TotalSeconds;
            sampling.Set();
            if (!sampler.Join(TimeSpan.FromSeconds(5)))
                failure ??= new TimeoutException("Runtime sampler did not stop");
            store.Write(output);
            await File.WriteAllTextAsync(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new
            {
                store.TotalCompleted, store.Pending, store.MetricQueries, metrics.Acknowledged,
                metrics.Failures, metrics.GaugeSamples, ShutdownSeconds = shutdownSeconds,
                Error = failure?.ToString() ?? store.Failure?.ToString()
            }));
            var assembly = typeof(OutboxRelayService).Assembly;
            await File.WriteAllTextAsync(Path.Combine(output, "identity.json"), JsonSerializer.Serialize(new
            {
                Args = args, assembly.FullName, assembly.Location, Runtime = Environment.Version.ToString(),
                Environment.ProcessorCount, GCSettings.IsServerGC, Stopwatch.Frequency,
                ProductSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
            }));
        }
        if (failure is not null || store.Failure is not null)
            throw new InvalidOperationException("Loaded outbox failed", failure ?? store.Failure);
        if (store.Pending != 0 || metrics.Failures != 0)
            throw new InvalidOperationException("Failed or leftover work");
#if CANDIDATE
        if (args[4] == "on" && (metrics.Acknowledged != store.TotalCompleted || store.MetricQueries == 0 || metrics.GaugeSamples == 0))
            throw new InvalidOperationException("Missing or incorrect telemetry coverage");
#endif
    }
}

internal class Store : IOutboxStore
#if CANDIDATE
    , IOutboxMetricsStore
#endif
{
    public const int BatchCount = 500;
    private static readonly int[] Buckets = [0];
    private readonly OutboxMessage[] _rows;
    private readonly Phase[] _phases;
    private readonly CompilationLog _compilations;
    private readonly List<Snapshot> _samples;
    private int _phase;
    private long _started;
    public long TotalCompleted;
    public int Pending;
    public int MetricQueries = 0;
    public Exception? Failure;
    public readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Store(string topic, int warmup, int measured, CompilationLog compilations)
    {
        _compilations = compilations;
        _phases = [new("primer", 20), new("warmup", warmup), new("measured", measured)];
        _samples = new(20 + warmup + measured + 140);
        _rows = new OutboxMessage[BatchCount];
        for (var index = 0; index < _rows.Length; index++)
            _rows[index] = new OutboxMessage
            {
                Id = index + 1, Bucket = 0, MessageId = Guid.NewGuid(), Topic = topic,
                Partition = index % 3, Value = new byte[1000], CreatedAtUtc = DateTimeOffset.UtcNow
            };
    }

    public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
        CancellationToken cancellationToken = default) => new(Buckets);
    public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default) => new(_phase == _phases.Length ? Array.Empty<int>() : Buckets);

    public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (_phase == _phases.Length)
            return new(Array.Empty<OutboxMessage>());
        if (maxCount <= BatchCount || bucket != 0 || Pending != 0)
            throw new InvalidOperationException("Invalid batch fetch");
        var phase = _phases[_phase];
        if (phase.Start.Timestamp == 0)
        {
            _compilations.Phase(phase.Name);
            phase.Start = Snapshot.Capture(TotalCompleted, Pending);
        }
        Volatile.Write(ref Pending, BatchCount);
        _started = Stopwatch.GetTimestamp();
        return new(_rows);
    }

    public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (bucket != 0 || messages.Count != BatchCount || Pending != BatchCount)
            throw new InvalidOperationException("Partial or duplicated batch deletion");
        for (var index = 0; index < messages.Count; index++)
            if (!ReferenceEquals(messages[index], _rows[index]))
                throw new InvalidOperationException("Changed deletion order or row identity");
        Volatile.Write(ref Pending, 0);
        Interlocked.Add(ref TotalCompleted, BatchCount);
        long ended = Stopwatch.GetTimestamp();
        var phase = _phases[_phase];
        if (phase.Count == phase.Cycles.Length)
            throw new InvalidOperationException("Raw cycle capacity exceeded");
        phase.Cycles[phase.Count++] = new(_started, ended);
        if (Stopwatch.GetElapsedTime(phase.Start.Timestamp, ended).TotalSeconds >= phase.Seconds)
        {
            phase.End = Snapshot.Capture(TotalCompleted, Pending);
            _phase++;
            if (_phase == _phases.Length)
                Finished.TrySetResult();
        }
        return default;
    }

#if CANDIDATE
    private readonly OutboxPendingMetrics _full = new(BatchCount, DateTimeOffset.UnixEpoch);
    private readonly OutboxPendingMetrics _empty = new(0, null);
    public ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref MetricQueries);
        return new(Volatile.Read(ref Pending) == 0 ? _empty : _full);
    }
#endif

    internal void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref Failure, error, null);
        Finished.TrySetException(error);
    }

    internal void Sample(ManualResetEventSlim stop, MetricsObserver metrics)
    {
        try
        {
            do
            {
                _samples.Add(Snapshot.Capture(Volatile.Read(ref TotalCompleted), Volatile.Read(ref Pending)));
                metrics.Poll();
            } while (!stop.Wait(TimeSpan.FromSeconds(1)));
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    internal void Write(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "series.json"), JsonSerializer.Serialize(_samples));
        foreach (var phase in _phases)
        {
            using (var stream = new BinaryWriter(File.Create(Path.Combine(directory, phase.Name + "-cycles.bin"))))
                for (var index = 0; index < phase.Count; index++)
                {
                    stream.Write(phase.Cycles[index].Start);
                    stream.Write(phase.Cycles[index].End);
                }
            var sorted = new long[phase.Count];
            for (var index = 0; index < sorted.Length; index++)
                sorted[index] = phase.Cycles[index].End - phase.Cycles[index].Start;
            Array.Sort(sorted);
            double ns(long ticks) => ticks * (1e9 / Stopwatch.Frequency);
            double percentile(double fraction) => sorted.Length == 0 ? 0 : ns(sorted[(int)Math.Ceiling(sorted.Length * fraction) - 1]);
            var duration = (phase.End.Timestamp - phase.Start.Timestamp) / (double)Stopwatch.Frequency;
            long completed = (long)phase.Count * BatchCount;
            File.WriteAllText(Path.Combine(directory, phase.Name + ".json"), JsonSerializer.Serialize(new
            {
                phase.Name, RequestedSeconds = phase.Seconds, Seconds = duration, Completed = completed,
                Cycles = phase.Count, BatchCount, StopwatchFrequency = Stopwatch.Frequency,
                MessagesPerSecond = duration > 0 ? completed / duration : 0,
                CpuNsPerMessage = completed > 0 ? (phase.End.CpuTicks - phase.Start.CpuTicks) * 100.0 / completed : 0,
                AllocatedBytesPerMessage = completed > 0 ? (phase.End.Allocated - phase.Start.Allocated) / (double)completed : 0,
                P50Ns = percentile(.50), P99Ns = percentile(.99), MaxNs = percentile(1), phase.Start, phase.End
            }));
        }
    }
}

internal sealed class RenewingStore(string topic, int warmup, int measured, CompilationLog compilations)
    : Store(topic, warmup, measured, compilations), IOutboxLeaseRenewalStore
{
    public ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default) => new(true);
}

internal sealed class Phase(string name, int seconds)
{
    public readonly string Name = name;
    public readonly int Seconds = seconds;
    // 2.5 million completed rows/s ceiling; exceeding it invalidates, never drops samples.
    public readonly Cycle[] Cycles = new Cycle[checked((seconds + 1) * 5000)];
    public int Count;
    public Snapshot Start;
    public Snapshot End;
}

internal readonly record struct Cycle(long Start, long End);
internal readonly record struct Snapshot(long Timestamp, long CpuTicks, long Allocated, long Completed,
    int Pending, int Gen0, int Gen1, int Gen2, long HeapBytes, long RssBytes, int Threads,
    long PendingWork, long JitMethods, double JitMs)
{
    public static Snapshot Capture(long completed, int pending)
    {
        using var process = Process.GetCurrentProcess();
        return new(Stopwatch.GetTimestamp(), process.TotalProcessorTime.Ticks, GC.GetTotalAllocatedBytes(true),
            completed, pending, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetGCMemoryInfo().HeapSizeBytes, process.WorkingSet64, ThreadPool.ThreadCount,
            ThreadPool.PendingWorkItemCount, JitInfo.GetCompiledMethodCount(), JitInfo.GetCompilationTime().TotalMilliseconds);
    }
}

internal sealed class MetricsObserver : IDisposable
{
    private readonly MeterListener? _listener;
    public long Acknowledged;
    public long Failures;
    public long GaugeSamples;
    public MetricsObserver(bool enabled)
    {
        if (!enabled)
            return;
        _listener = new MeterListener();
        _listener.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Dekaf.Outbox")
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "dekaf.outbox.publish.acknowledged")
                Interlocked.Add(ref Acknowledged, value);
            else if (instrument.Name == "dekaf.outbox.publish.failures")
                Interlocked.Add(ref Failures, value);
            else if (instrument.Name == "dekaf.outbox.pending.messages")
                Interlocked.Increment(ref GaugeSamples);
        });
        _listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
        _listener.Start();
    }
    public void Poll() => _listener?.RecordObservableInstruments();
    public void Dispose() => _listener?.Dispose();
}

internal sealed class FailureLogger(Store store) : ILogger<OutboxRelayService>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
            store.Fail(exception ?? new InvalidOperationException(formatter(state, exception)));
    }
}
