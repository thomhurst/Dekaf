using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

var role = args[0];
var topic = args[1];
var folder = Path.GetFullPath(args[2]);
var mode = args[3];
var warmup = int.Parse(args[4]);
var seconds = int.Parse(args[5]);
var rate = int.Parse(args[6]);
Directory.CreateDirectory(folder);
if (role == "produce")
    await Load.Produce(topic, folder, mode, warmup, seconds, rate);
else if (role == "consume")
{
    using var compilations = new CompilationLog(Path.Combine(folder, "compilations.json"));
    compilations.Phase("initialize");
    await new Load().Consume(topic, folder, mode, warmup, seconds, rate, compilations);
    compilations.Phase("finalize");
}
else
    throw new ArgumentException("Unknown role.");

internal readonly record struct Payload(int Sequence, long Scheduled);

internal sealed class PayloadDeserializer : IDeserializer<Payload>
{
    public Payload Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        => new(BinaryPrimitives.ReadInt32LittleEndian(data.Span),
            BinaryPrimitives.ReadInt64LittleEndian(data.Span[8..]));
}

internal sealed class Load
{
    private const int Partitions = 4;
    private const int OfferBurst = 128;
    private readonly long[] _lastByKey = new long[1024];
    private readonly long[] _batchCounts = new long[17];
    private readonly long[] _measuredBatchCounts = new long[17];
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Process _accountingProcess = Process.GetCurrentProcess();
    private long[] _latencies = null!;
    private CompilationLog _compilations = null!;
    private Sample[] _series = null!;
    private int _seriesCount;
    private int _warmupCount;
    private int _total;
    private int _rate;
    private long _completed;
    private long _measured;
    private long _firstCompletion;
    private long _measurementStart;
    private long _measurementEnd;
    private long _cpuStart;
    private long _cpuEnd;
    private long _allocationStart;
    private long _allocationEnd;
    private long _jitStart;
    private long _jitEnd;
    private double _jitMsStart;
    private double _jitMsEnd;
    private long _scheduledStart;
    private int _pendingHandlers;
    private long _pendingCompletions;
    private bool _pending;

    private static int KeyFor(int index, bool pending) => pending
        ? ((index / Partitions / 32) & 1) * Partitions + index % Partitions
        : index % 1024;

    internal static async Task Produce(string topic, string folder, string mode, int warmup, int seconds, int rate)
    {
        await using var producer = new KafkaProducer<int, byte[]>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"], LingerMs = 1
        }, Serializers.Int32, Serializers.ByteArray);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(warmup + seconds + 180));
        await producer.InitializeAsync(deadline.Token);
        while (!File.Exists(Path.Combine(folder, "ready")))
            await Task.Delay(20, deadline.Token);
        var payload = new byte[256];
        var total = checked((seconds + warmup) * rate);
        var start = Stopwatch.GetTimestamp();
        long acknowledged = 0, failed = 0, maxSendLateness = 0;
        Action<RecordMetadata, Exception?> delivery = (_, error) =>
        {
            if (error is null) Interlocked.Increment(ref acknowledged);
            else Interlocked.Increment(ref failed);
        };
        var pending = mode.StartsWith("pending", StringComparison.Ordinal);
        for (var index = 0; index < total; index++)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var scheduled = start + (long)((index / OfferBurst * OfferBurst) * (double)Stopwatch.Frequency / rate);
            if (index % OfferBurst == 0)
            {
                var remaining = scheduled - Stopwatch.GetTimestamp();
                if (remaining > 0)
                    await Task.Delay(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency), deadline.Token);
            }
            maxSendLateness = Math.Max(maxSendLateness, Stopwatch.GetTimestamp() - scheduled);
            BinaryPrimitives.WriteInt32LittleEndian(payload, index);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), scheduled);
            await producer.FireAsync(new ProducerMessage<int, byte[]>
            {
                Topic = topic, Partition = index % Partitions, Key = KeyFor(index, pending), Value = payload
            }, delivery);
        }
        await producer.FlushAsync(deadline.Token);
        WriteJson(folder, "producer.json", new
        {
            Sent = total, Acknowledged = acknowledged, Failed = failed, Rate = rate, OfferBurst,
            ScheduledStart = start, StopwatchFrequency = Stopwatch.Frequency,
            ElapsedSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds,
            MaxSendLatenessMs = maxSendLateness * 1000d / Stopwatch.Frequency
        });
        if (acknowledged != total || failed != 0)
            throw new InvalidOperationException("Producer delivery count mismatch.");
    }

    internal async Task Consume(string topic, string folder, string mode, int warmup, int seconds, int rate,
        CompilationLog compilations)
    {
        _compilations = compilations;
        _pending = mode.StartsWith("pending", StringComparison.Ordinal);
        var batches = mode.EndsWith("batches", StringComparison.Ordinal);
        if (mode is not ("sync-records" or "sync-batches" or "pending-records" or "pending-batches"))
            throw new ArgumentException("Unknown handler mode.");
        _warmupCount = checked(warmup * rate);
        _rate = rate;
        _total = checked((warmup + seconds) * rate);
        _latencies = new long[_total];
        Array.Fill(_latencies, 0L);
        Array.Fill(_lastByKey, -1L);
        _series = new Sample[warmup + seconds + 200];
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(warmup + seconds + 180));
        using var stop = new CancellationTokenSource();
        using var stopSampler = new CancellationTokenSource();
        await using var consumer = new KafkaConsumer<int, Payload>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = topic + "-group",
            AutoOffsetReset = AutoOffsetReset.Earliest, OffsetCommitMode = OffsetCommitMode.Manual,
            EnableAutoOffsetStore = false, PrefetchPipelineDepth = 3,
            FetchMaxWaitMs = 10, QueuedMinMessages = 1
        }, Serializers.Int32, new PayloadDeserializer());
        await consumer.InitializeAsync(watchdog.Token);
        consumer.Assign(Enumerable.Range(0, Partitions).Select(p => new TopicPartition(topic, p)).ToArray());
        // Initialize the offset coordinator before warmup, identically for every phase.
        // A fresh broker otherwise first creates __consumer_offsets during measured shutdown.
        var initialOffsets = Enumerable.Range(0, Partitions)
            .Select(partition => new TopicPartitionOffset(topic, partition, 0)).ToArray();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await consumer.CommitAsync(initialOffsets, watchdog.Token);
                break;
            }
            catch (Dekaf.Errors.KafkaException error) when (error.IsRetriable && attempt < 4)
            {
                Console.WriteLine($"Coordinator initialization attempt {attempt + 1}: {error.Message}");
                await Task.Delay(1000, watchdog.Token);
            }
        }
        for (var partition = 0; partition < Partitions; partition++)
        {
            if (await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, partition), watchdog.Token) != 0)
                throw new InvalidOperationException("Initial committed offset was not confirmed by the broker.");
        }
        Console.WriteLine("Coordinator initialization confirmed: all four committed offsets are zero.");
        var options = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key, MaxConcurrentHandlersPerPartition = 2,
            MaxBufferedRecordsPerPartition = 128, MaxHandlerBatchSize = 16,
            CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
            StopPolicy = PartitionStopPolicy.Drain, StopTimeout = TimeSpan.FromSeconds(20)
        };
        var running = batches
            ? consumer.RunPartitionedBatchesAsync(HandleBatch, options, stop.Token).AsTask()
            : consumer.RunPartitionedAsync(HandleRecord, options, stop.Token).AsTask();
        var observing = ObserveSeries(rate, stopSampler.Token);
        _compilations.Phase("warmup");
        File.WriteAllText(Path.Combine(folder, "ready"), "ready");
        long stopTicks = 0;
        try
        {
            var completed = await Task.WhenAny(_done.Task, running).WaitAsync(watchdog.Token);
            if (completed == running)
            {
                await running;
                throw new InvalidOperationException("Consumer ended before all records completed.");
            }
            await _done.Task;
        }
        finally
        {
            var stopStart = Stopwatch.GetTimestamp();
            try
            {
                await stop.CancelAsync();
                try { await running.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                stopTicks = Stopwatch.GetTimestamp() - stopStart;
            }
            finally
            {
                await stopSampler.CancelAsync();
                await observing;
                WriteJson(folder, "series.json", _series.AsSpan(0, _seriesCount).ToArray());
                using var output = File.Create(Path.Combine(folder, "all-latency-ticks.bin"));
                output.Write(MemoryMarshal.AsBytes(_latencies.AsSpan()));
            }
        }
        if (_completed != _total || _measured != seconds * (long)rate || _latencies.Any(t => t <= 0)
            || Volatile.Read(ref _pendingHandlers) != 0)
            throw new InvalidOperationException("Missing/duplicate records or pending handlers after shutdown.");
        var committed = new long?[Partitions];
        for (var partition = 0; partition < Partitions; partition++)
        {
            committed[partition] = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, partition), watchdog.Token);
            var expected = (_total + Partitions - 1 - partition) / Partitions;
            if (committed[partition] != expected)
                throw new InvalidOperationException($"Partition {partition} committed {committed[partition]}, expected {expected}.");
        }
        if (_pending && (_pendingCompletions == 0 || (batches && _batchCounts[16] == 0)))
            throw new InvalidOperationException("Missing pending completion or configured 16-record batch coverage.");
        var measuredLatencies = _latencies.AsSpan(_warmupCount);
        using (var output = File.Create(Path.Combine(folder, "latency-ticks.bin")))
            output.Write(MemoryMarshal.AsBytes(measuredLatencies));
        measuredLatencies.Sort();
        var elapsed = (_measurementEnd - _measurementStart) / (double)Stopwatch.Frequency;
        var measuredHandlers = _measuredBatchCounts.Sum();
        WriteJson(folder, "metrics.json", new
        {
            Mode = mode, Completed = _completed, Measured = _measured, Failures = 0, BacklogAtEnd = 0,
            OfferedMessagesPerSecond = rate, MeasuredDurationSeconds = elapsed,
            WarmupOfferedSeconds = warmup, WarmupCompleted = _completed - _measured,
            ActualWarmupSeconds = (_measurementStart - _firstCompletion) / (double)Stopwatch.Frequency,
            MeasurementStart = _measurementStart, MeasurementEnd = _measurementEnd,
            JitMethodsStart = _jitStart, JitMethodsEnd = _jitEnd,
            JitMsStart = _jitMsStart, JitMsEnd = _jitMsEnd,
            MessagesPerSecond = _measured / elapsed,
            CpuNsPerMessage = (_cpuEnd - _cpuStart) * 100d / _measured,
            AllocatedBytesPerMessage = (_allocationEnd - _allocationStart) / (double)_measured,
            AllocatedBytesPerHandlerInvocation = (_allocationEnd - _allocationStart) / (double)measuredHandlers,
            P50Ns = Percentile(measuredLatencies, .5), P99Ns = Percentile(measuredLatencies, .99),
            MaxNs = measuredLatencies[^1] * 1_000_000_000d / Stopwatch.Frequency,
            StopwatchFrequency = Stopwatch.Frequency, StopNs = stopTicks * 1_000_000_000d / Stopwatch.Frequency,
            PendingCompletions = _pendingCompletions, PendingAfterStop = _pendingHandlers,
            BatchCounts = _batchCounts, MeasuredBatchCounts = _measuredBatchCounts,
            MeasuredHandlerInvocations = measuredHandlers, CommittedOffsets = committed,
            Scope = "Consumer process including sampler and handler simulation; broker and baseline producer are separate processes. " +
                "Latency spans scheduled producer offer to handler processing completion (after the pending delay), before automatic frontier bookkeeping. " +
                "Pending handlers use Task.Delay(1) once per handler invocation; its allocations are included and batch histogram is retained. " +
                "Per-handler allocation divides the same consumer-process allocation by invocations containing at least one measured record. " +
                "Throughput is completed rate under the declared offered load, not maximum capacity. " +
                "Stop timing begins after all processing completions and includes final frontier publication and broker commits; it is not loaded shutdown latency."
        });
        _process.Dispose();
        _accountingProcess.Dispose();
    }

    private ValueTask HandleRecord(PartitionRecordProcessorContext<int, Payload> context,
        ConsumeResult<int, Payload> record, CancellationToken token)
    {
        Interlocked.Increment(ref _batchCounts[1]);
        if (record.Value.Sequence >= _warmupCount) Interlocked.Increment(ref _measuredBatchCounts[1]);
        if (_pending) return CompletePendingRecord(record);
        Complete(record);
        return default;
    }

    private ValueTask HandleBatch(PartitionBatchProcessorContext<int, Payload> context,
        IReadOnlyList<ConsumeResult<int, Payload>> records, CancellationToken token)
    {
        if (records.Count is < 1 or > 16) throw new InvalidOperationException("Invalid batch size.");
        Interlocked.Increment(ref _batchCounts[records.Count]);
        if (records[^1].Value.Sequence >= _warmupCount) Interlocked.Increment(ref _measuredBatchCounts[records.Count]);
        if (_pending) return CompletePendingBatch(records);
        CompleteBatch(records);
        return default;
    }

    private async ValueTask CompletePendingRecord(ConsumeResult<int, Payload> record)
    {
        Interlocked.Increment(ref _pendingHandlers);
        try
        {
            await Task.Delay(1).ConfigureAwait(false);
            Complete(record);
            Interlocked.Increment(ref _pendingCompletions);
        }
        finally { Interlocked.Decrement(ref _pendingHandlers); }
    }

    private async ValueTask CompletePendingBatch(IReadOnlyList<ConsumeResult<int, Payload>> records)
    {
        Interlocked.Increment(ref _pendingHandlers);
        try
        {
            await Task.Delay(1).ConfigureAwait(false);
            CompleteBatch(records);
            Interlocked.Increment(ref _pendingCompletions);
        }
        finally { Interlocked.Decrement(ref _pendingHandlers); }
    }

    private void CompleteBatch(IReadOnlyList<ConsumeResult<int, Payload>> records)
    {
        var key = records[0].Key;
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index].Key != key) throw new InvalidOperationException("Mixed keys in a batch.");
            Complete(records[index]);
        }
    }

    private void Complete(ConsumeResult<int, Payload> record)
    {
        var payload = record.Value;
        var index = payload.Sequence;
        if ((uint)index >= _total || record.Key != KeyFor(index, _pending)
            || record.Partition != index % Partitions || record.Offset != index / Partitions
            || _lastByKey[record.Key] >= index)
            throw new InvalidOperationException("Invalid record or per-key ordering.");
        _lastByKey[record.Key] = index;
        var now = Stopwatch.GetTimestamp();
        Interlocked.CompareExchange(ref _firstCompletion, now, 0);
        if (index >= _warmupCount && Interlocked.CompareExchange(ref _measurementStart, -1, 0) == 0)
        {
            _accountingProcess.Refresh();
            _cpuStart = _accountingProcess.TotalProcessorTime.Ticks;
            _allocationStart = GC.GetTotalAllocatedBytes(true);
            _jitStart = System.Runtime.JitInfo.GetCompiledMethodCount();
            _jitMsStart = System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds;
            Volatile.Write(ref _measurementStart, Stopwatch.GetTimestamp());
            _compilations.Phase("measured");
        }
        Interlocked.CompareExchange(ref _scheduledStart, payload.Scheduled -
            (long)((index / OfferBurst * OfferBurst) * (double)Stopwatch.Frequency / _rate), 0);
        if (Interlocked.Exchange(ref _latencies[index], Math.Max(1, now - payload.Scheduled)) != 0)
            throw new InvalidOperationException("Duplicate record.");
        if (index >= _warmupCount) Interlocked.Increment(ref _measured);
        if (Interlocked.Increment(ref _completed) == _total)
        {
            _measurementEnd = Stopwatch.GetTimestamp();
            _accountingProcess.Refresh();
            _cpuEnd = _accountingProcess.TotalProcessorTime.Ticks;
            _allocationEnd = GC.GetTotalAllocatedBytes(true);
            _jitEnd = System.Runtime.JitInfo.GetCompiledMethodCount();
            _jitMsEnd = System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds;
            _compilations.Phase("drain");
            _done.TrySetResult();
        }
    }

    private async Task ObserveSeries(int rate, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_seriesCount == _series.Length) throw new InvalidOperationException("Sampler storage exhausted.");
            _process.Refresh();
            var now = Stopwatch.GetTimestamp();
            var completed = Interlocked.Read(ref _completed);
            // Before the first completion the producer's clock origin is unknown.
            // Afterwards count whole scheduled bursts, including the burst at time zero.
            var expected = _scheduledStart == 0 ? -1 : Math.Clamp(
                ((long)((now - _scheduledStart) * (double)rate / Stopwatch.Frequency) / OfferBurst + 1) * OfferBurst,
                0, _total);
            _series[_seriesCount++] = new Sample(now, completed, Interlocked.Read(ref _measured), expected,
                expected < 0 ? -1 : Math.Max(0, expected - completed), _process.TotalProcessorTime.Ticks,
                GC.GetTotalAllocatedBytes(false), GC.GetTotalMemory(false), _process.WorkingSet64,
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                System.Runtime.JitInfo.GetCompiledMethodCount(), System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds,
                ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount, Volatile.Read(ref _pendingHandlers));
            try { await Task.Delay(1000, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }

    private readonly record struct Sample(long Timestamp, long Completed, long Measured, long OfferedBySchedule,
        long Backlog, long CpuTicks, long AllocatedBytes, long HeapBytes, long RssBytes,
        int Gen0, int Gen1, int Gen2, long JitMethods, double JitMs, int Threads, long PendingWork, int PendingHandlers);

    private static double Percentile(ReadOnlySpan<long> values, double percentile)
        => values[(int)Math.Ceiling(values.Length * percentile) - 1] * 1_000_000_000d / Stopwatch.Frequency;

    private static void WriteJson<T>(string folder, string name, T value)
        => File.WriteAllText(Path.Combine(folder, name), JsonSerializer.Serialize(value));
}
