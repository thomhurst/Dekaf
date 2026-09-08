using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dekaf;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

internal sealed class ShutdownProbe
{
    private const int Records = 128;
    private readonly int _batchSize;
    private readonly int _keys;
    private readonly PartitionProcessor<int, int> _processor;
    private readonly long[] _latencies = new long[Records];
    private readonly long[] _lastByKey = new long[2];
    private readonly Action<PartitionLane<int, int>, Exception> _failed;
    private TaskCompletionSource _started = null!;
    private TaskCompletionSource _release = null!;
    private Exception? _failure;
    private int _handled;
    private int _pending;
    private int _largestBatch;
    private long _enqueuedAt;

    private ShutdownProbe(int batchSize, int keys)
    {
        _batchSize = batchSize;
        _keys = keys;
        _failed = (_, error) => _failure = error;
        var options = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key,
            MaxBufferedRecordsPerPartition = Records,
            MaxConcurrentHandlersPerPartition = 2,
            MaxHandlerBatchSize = batchSize
        };
        var factory = typeof(PartitionedConsumerExtensions).GetMethod(
            "CreateBatchProcessor", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int), typeof(int));
        PartitionBatchProcessor<int, int> handler = (_, records, _) => Handle(records);
        _processor = (PartitionProcessor<int, int>)factory.Invoke(null, [handler, options])!;
    }

    private async ValueTask Handle(IReadOnlyList<ConsumeResult<int, int>> records)
    {
        Interlocked.Increment(ref _pending);
        _started.TrySetResult();
        try
        {
            await _release.Task.ConfigureAwait(false);
            var key = records[0].Key;
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record.Key != key || record.Offset <= _lastByKey[key])
                    throw new InvalidOperationException("Shutdown changed key order.");
                _lastByKey[key] = record.Offset;
                if (Interlocked.Exchange(ref _latencies[record.Offset],
                        Math.Max(1, Stopwatch.GetTimestamp() - _enqueuedAt)) != 0)
                    throw new InvalidOperationException("Shutdown processed a duplicate.");
                Interlocked.Increment(ref _handled);
            }
            InterlockedMax(ref _largestBatch, records.Count);
        }
        finally { Interlocked.Decrement(ref _pending); }
    }

    private async ValueTask<long> StopLoaded()
    {
        _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _failure = null;
        _handled = _pending = _largestBatch = 0;
        Array.Clear(_latencies);
        Array.Fill(_lastByKey, -1);
        var lane = new PartitionLane<int, int>(new TopicPartition("shutdown", 0), Records,
            static (_, _) => default, static _ => { }, _failed);
        _enqueuedAt = Stopwatch.GetTimestamp();
        for (var index = 0; index < Records; index++)
        {
            if (!lane.TryEnqueue(new ConsumeResult<int, int>("shutdown", 0, index,
                    (index / 32) % _keys, 0, null, 0, TimestampType.CreateTime, 7)))
                throw new InvalidOperationException("Shutdown queue fill failed.");
        }
        lane.Start(_processor);
        try
        {
            await _started.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            var queued = Stopwatch.GetTimestamp();
            // Wait for all accepted records to reach the coordinator's pending
            // queues. A signal from the first handler alone races batch formation.
            while (Volatile.Read(ref BufferedCount(lane)) != 0)
            {
                if (Stopwatch.GetElapsedTime(queued).TotalSeconds > 10)
                    throw new InvalidOperationException("Dispatcher did not queue the bounded workload.");
                await Task.Yield();
            }
            if (_handled != 0 || Volatile.Read(ref _pending) == 0)
                throw new InvalidOperationException("Shutdown did not start under load.");
            var stopStart = Stopwatch.GetTimestamp();
            var stopping = lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10));
            if (stopping.IsCompleted)
                throw new InvalidOperationException("Shutdown completed with blocked handlers.");
            _release.TrySetResult();
            var failure = await stopping.ConfigureAwait(false);
            var stopTicks = Stopwatch.GetTimestamp() - stopStart;
            if (failure is not null || _failure is not null || _handled != Records || _pending != 0
                || !lane.IsCompleted || _latencies.Any(static value => value <= 0)
                || lane.GetCommitOffset() != new TopicPartitionOffset("shutdown", 0, Records, 7)
                || _largestBatch != _batchSize)
                throw new InvalidOperationException($"Shutdown validation: handled={_handled}, pending={_pending}, " +
                    $"completed={lane.IsCompleted}, missing={_latencies.Count(static value => value <= 0)}, " +
                    $"frontier={lane.GetCommitOffset()}, batch={_largestBatch}/{_batchSize}.", failure ?? _failure);
            return stopTicks;
        }
        finally
        {
            _release.TrySetResult();
            await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
    }

    internal static async Task<int> Run(string[] args)
    {
        var batchSize = int.Parse(args[1]);
        var keys = int.Parse(args[2]);
        var folder = args[3];
        var durations = new[] { double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) };
        Directory.CreateDirectory(folder);
        using var compilations = new CompilationLog(Path.Combine(folder, "compilations.json"));
        compilations.Phase("initialize");
        var probe = new ShutdownProbe(batchSize, keys);
        var captures = durations.Select(static seconds => new Capture(seconds)).ToArray();
        using var process = Process.GetCurrentProcess();
        var origin = Stopwatch.GetTimestamp();
        long completed = 0;
        // One continuous loop crosses the warmup boundary; reporting happens afterwards.
        var phase = 0;
        var current = captures[phase];
        compilations.Phase("warmup");
        current.Start = Snapshot.Take(process, origin, completed);
        var nextSecond = 1d;
        long stopSum = 0, messageSum = 0, intervalStops = 0, stopMax = 0, messageMax = 0;
        while (true)
        {
            var stop = await probe.StopLoaded().ConfigureAwait(false);
            current.Stops.Add(stop);
            stopSum += stop;
            intervalStops++;
            stopMax = Math.Max(stopMax, stop);
            foreach (var ticks in probe._latencies)
            {
                current.Messages.Add(ticks);
                messageSum += ticks;
                messageMax = Math.Max(messageMax, ticks);
            }
            completed += Records;
            var elapsed = Stopwatch.GetElapsedTime(origin).TotalSeconds - current.Start.Seconds;
            if (elapsed < nextSecond && elapsed < current.Duration) continue;
            var snapshot = Snapshot.Take(process, origin, completed) with
            {
                StopMeanTicks = stopSum / (double)intervalStops, StopMaxTicks = stopMax,
                MessageMeanTicks = messageSum / (double)(intervalStops * Records), MessageMaxTicks = messageMax
            };
            current.Series.Add(snapshot);
            stopSum = messageSum = intervalStops = stopMax = messageMax = 0;
            nextSecond = Math.Floor(elapsed) + 1;
            if (elapsed < current.Duration) continue;
            current.End = snapshot;
            if (++phase == captures.Length) break;
            current = captures[phase];
            compilations.Phase("measured");
            current.Start = Snapshot.Take(process, origin, completed);
            nextSecond = 1;
        }
        compilations.Phase("finalize");
        for (var index = 0; index < captures.Length; index++)
        {
            var capture = captures[index];
            File.WriteAllText(Path.Combine(folder, index == 0 ? "warmup.json" : "measured.json"),
                JsonSerializer.Serialize(new { BatchSize = batchSize, Keys = keys, RecordsPerStop = Records,
                    StopwatchFrequency = Stopwatch.Frequency, capture.Start, capture.End, capture.Series,
                    StopTicks = capture.Stops.Values(), MessageTicks = capture.Messages.Values(),
                    PendingAfterStop = 0, Failures = 0,
                    Scope = "Dispatcher process including construction, synchronization and histogram accounting. " +
                        "Message latency spans queue fill to handler completion; stop latency spans Drain request through completion. " +
                        "Every stop verifies all 128 records and the automatic commit frontier. No broker in this focused lifecycle probe." }));
        }
        return 0;
    }

    private sealed class Capture(double duration)
    {
        internal readonly double Duration = duration;
        internal Snapshot Start = null!;
        internal Snapshot End = null!;
        internal readonly List<Snapshot> Series = new((int)Math.Ceiling(duration) + 1);
        internal readonly Histogram Stops = new();
        internal readonly Histogram Messages = new();
    }

    private sealed class Histogram
    {
        private readonly long[] _dense = new long[Math.Min(Stopwatch.Frequency / 1000, 1_000_000) + 1];
        private readonly Dictionary<long, long> _overflow = new();
        internal void Add(long value)
        {
            if ((ulong)value < (ulong)_dense.Length) _dense[value]++;
            else { _overflow.TryGetValue(value, out var count); _overflow[value] = count + 1; }
        }
        internal IEnumerable<object> Values()
        {
            for (var ticks = 0; ticks < _dense.Length; ticks++)
                if (_dense[ticks] != 0) yield return new { Ticks = (long)ticks, Count = _dense[ticks] };
            foreach (var item in _overflow.OrderBy(static pair => pair.Key))
                yield return new { Ticks = item.Key, Count = item.Value };
        }
    }

    private sealed record Snapshot(double Seconds, long Completed, long CpuTicks, long AllocatedBytes,
        long JitMethods, double JitMs, int Threads, long PendingWork, int Gen0, int Gen1, int Gen2,
        long HeapBytes, long RssBytes, double StopMeanTicks = 0, long StopMaxTicks = 0,
        double MessageMeanTicks = 0, long MessageMaxTicks = 0)
    {
        internal static Snapshot Take(Process process, long origin, long completed)
        {
            process.Refresh();
            return new(Stopwatch.GetElapsedTime(origin).TotalSeconds, completed, process.TotalProcessorTime.Ticks,
                GC.GetTotalAllocatedBytes(true), System.Runtime.JitInfo.GetCompiledMethodCount(),
                System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, ThreadPool.ThreadCount,
                ThreadPool.PendingWorkItemCount, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                GC.GetTotalMemory(false), process.WorkingSet64);
        }
    }

    private static void InterlockedMax(ref int location, int value)
    {
        var current = Volatile.Read(ref location);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current) return;
            current = observed;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bufferedCount")]
    private static extern ref int BufferedCount(PartitionLane<int, int> lane);
}
