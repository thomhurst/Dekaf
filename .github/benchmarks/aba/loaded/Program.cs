using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

// Task-scoped load fixture. Producer always uses baseline Dekaf in a separate process.
var role = args[0];
var topic = args[1];
var folder = args[2];
var mode = args[3];
var seconds = int.Parse(args[4]);
var rate = int.Parse(args[5]);
Directory.CreateDirectory(folder);
if (role == "produce")
    await Load.Produce(topic, folder, seconds, rate);
else
    await new Load().Consume(topic, folder, mode, seconds, rate);

internal readonly record struct Payload(int Sequence, long Scheduled);

internal sealed class PayloadDeserializer : IDeserializer<Payload>
{
    public Payload Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        => new(BinaryPrimitives.ReadInt32LittleEndian(data.Span),
            BinaryPrimitives.ReadInt64LittleEndian(data.Span[8..]));
}

internal sealed class Load
{
    private const int WarmupSeconds = 20;
    private const int Partitions = 4;
    private long[] _latencies = null!;
    private readonly long[] _lastByKey = new long[1024];
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _completed;
    private long _measured;
    private int _warmupCount;
    private int _total;
    private long _measurementStart;
    private long _measurementEnd;
    private long _cpuStart;
    private long _cpuEnd;
    private long _allocationStart;
    private long _allocationEnd;
    private long _scheduledStart;
    private readonly Process _process = Process.GetCurrentProcess();

    internal static async Task Produce(string topic, string folder, int seconds, int rate)
    {
        await using var producer = new KafkaProducer<int, byte[]>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"], LingerMs = 1
        }, Serializers.Int32, Serializers.ByteArray);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + WarmupSeconds + 180));
        while (!File.Exists(Path.Combine(folder, "ready")))
            await Task.Delay(20, deadline.Token);
        var payload = new byte[256];
        var total = checked((seconds + WarmupSeconds) * rate);
        var start = Stopwatch.GetTimestamp();
        long acknowledged = 0, failed = 0, maxSendLateness = 0;
        Action<RecordMetadata, Exception?> delivery = (_, error) =>
        {
            if (error is null) Interlocked.Increment(ref acknowledged);
            else Interlocked.Increment(ref failed);
        };
        for (var index = 0; index < total; index++)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var scheduled = start + (long)((index / 100 * 100) * (double)Stopwatch.Frequency / rate);
            // Pace groups of 100 without moving the schedule when producer falls behind.
            if (index % 100 == 0)
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
                Topic = topic, Partition = index % Partitions, Key = index % 1024, Value = payload
            }, delivery);
        }
        await producer.FlushAsync(deadline.Token);
        File.WriteAllText(Path.Combine(folder, "producer.json"), JsonSerializer.Serialize(new
        {
            Sent = total, Acknowledged = acknowledged, Failed = failed, Rate = rate,
            ScheduledStart = start, StopwatchFrequency = Stopwatch.Frequency,
            ElapsedSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds,
            MaxSendLatenessMs = maxSendLateness * 1000d / Stopwatch.Frequency
        }));
        if (acknowledged != total || failed != 0)
            throw new InvalidOperationException("Producer delivery count mismatch.");
    }

    internal async Task Consume(string topic, string folder, string mode, int seconds, int rate)
    {
        _warmupCount = checked(WarmupSeconds * rate);
        _total = checked((seconds + WarmupSeconds) * rate);
        _latencies = new long[_total];
        Array.Fill(_latencies, 0L);
        Array.Fill(_lastByKey, -1L);
        var series = new List<object>();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + WarmupSeconds + 180));
        using var stop = new CancellationTokenSource();
        await using var consumer = new KafkaConsumer<int, Payload>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], AutoOffsetReset = AutoOffsetReset.Earliest,
            OffsetCommitMode = OffsetCommitMode.Manual, EnableAutoOffsetStore = false,
            PrefetchPipelineDepth = mode == "fetch-depth-1" ? 1 : 3,
            FetchMaxWaitMs = 10, QueuedMinMessages = 1
        }, Serializers.Int32, new PayloadDeserializer());
        consumer.Assign(Enumerable.Range(0, Partitions).Select(p => new TopicPartition(topic, p)).ToArray());
        var options = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key, MaxConcurrentHandlersPerPartition = 2,
            MaxBufferedRecordsPerPartition = 128, MaxHandlerBatchSize = 16,
            CommitPolicy = PartitionCommitPolicy.UserManaged, StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(10)
        };
        Task running = mode switch
        {
            "key-records" => consumer.RunPartitionedAsync(HandleRecord, options, stop.Token).AsTask(),
            "key-batches" => consumer.RunPartitionedBatchesAsync(HandleBatch, options, stop.Token).AsTask(),
            _ => ConsumeSequential(consumer, stop.Token)
        };
        File.WriteAllText(Path.Combine(folder, "ready"), "ready");
        var observing = ObserveSeries(series, rate, stop.Token);
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
            await stop.CancelAsync();
            try { await running.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            await observing;
            File.WriteAllText(Path.Combine(folder, "series.json"), JsonSerializer.Serialize(series));
        }
        if (_completed != _total || _measured != seconds * (long)rate || _latencies.Any(t => t == 0))
            throw new InvalidOperationException("Missing or duplicate completed records.");
        // Preserve every measured latency, including maxima, before sorting for percentiles.
        var measured = _latencies.AsSpan(_warmupCount);
        using (var output = File.Create(Path.Combine(folder, "latency-ticks.bin")))
            output.Write(MemoryMarshal.AsBytes(measured));
        measured.Sort();
        var elapsed = (_measurementEnd - _measurementStart) / (double)Stopwatch.Frequency;
        var result = new
        {
            Mode = mode, Completed = _completed, Measured = _measured, Failures = 0, BacklogAtEnd = 0,
            OfferedMessagesPerSecond = rate, MeasuredDurationSeconds = elapsed, WarmupSeconds,
            MessagesPerSecond = _measured / elapsed,
            CpuNsPerMessage = (_cpuEnd - _cpuStart) * 100d / _measured,
            AllocatedBytesPerMessage = (_allocationEnd - _allocationStart) / (double)_measured,
            P50Ns = Percentile(measured, .5), P99Ns = Percentile(measured, .99),
            MaxNs = measured[^1] * 1_000_000_000d / Stopwatch.Frequency,
            StopwatchFrequency = Stopwatch.Frequency,
            Scope = "Client process including harness sampler; producer and broker are separate processes. " +
                "Latency is scheduled producer offer to synchronous handler completion, including producer pacing debt, broker and consumer queueing. " +
                "Consumer starts measurement at first measured completion and ends at final completion. " +
                "Allocation includes amortized fetch/dispatch and harness overhead; use MemoryDiagnoser for isolated hot-path attribution."
        };
        File.WriteAllText(Path.Combine(folder, "metrics.json"), JsonSerializer.Serialize(result));
        _process.Dispose();
    }

    private ValueTask HandleRecord(PartitionRecordProcessorContext<int, Payload> context,
        ConsumeResult<int, Payload> record, CancellationToken token)
    {
        Complete(record);
        return default;
    }

    private ValueTask HandleBatch(PartitionBatchProcessorContext<int, Payload> context,
        IReadOnlyList<ConsumeResult<int, Payload>> records, CancellationToken token)
    {
        for (var i = 0; i < records.Count; i++) Complete(records[i]);
        return default;
    }

    private async Task ConsumeSequential(IKafkaConsumer<int, Payload> consumer, CancellationToken token)
    {
        await foreach (var record in consumer.ConsumeAsync(token)) Complete(record);
    }

    private void Complete(ConsumeResult<int, Payload> record)
    {
        var payload = record.Value;
        var index = payload.Sequence;
        if ((uint)index >= _total || record.Key != index % 1024 || record.Partition != index % Partitions
            || _lastByKey[record.Key] >= index)
            throw new InvalidOperationException("Invalid record or per-key ordering.");
        _lastByKey[record.Key] = index;
        if (index >= _warmupCount && Interlocked.CompareExchange(ref _measurementStart, -1, 0) == 0)
        {
            _cpuStart = _process.TotalProcessorTime.Ticks;
            _allocationStart = GC.GetTotalAllocatedBytes(true);
            _scheduledStart = payload.Scheduled - (long)((index / 100 * 100 - _warmupCount) * (double)Stopwatch.Frequency /
                (_warmupCount / WarmupSeconds));
            Volatile.Write(ref _measurementStart, Stopwatch.GetTimestamp());
        }
        var now = Stopwatch.GetTimestamp();
        if (Interlocked.Exchange(ref _latencies[index], Math.Max(1, now - payload.Scheduled)) != 0)
            throw new InvalidOperationException("Duplicate record.");
        if (index >= _warmupCount) Interlocked.Increment(ref _measured);
        if (Interlocked.Increment(ref _completed) == _total)
        {
            _measurementEnd = Stopwatch.GetTimestamp();
            _cpuEnd = _process.TotalProcessorTime.Ticks;
            _allocationEnd = GC.GetTotalAllocatedBytes(true);
            _done.TrySetResult();
        }
    }

    private async Task ObserveSeries(List<object> series, int rate, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _process.Refresh();
            var now = Stopwatch.GetTimestamp();
            var measured = Interlocked.Read(ref _measured);
            var expected = _scheduledStart == 0 ? 0 : Math.Clamp(
                (long)((now - _scheduledStart) * (double)rate / Stopwatch.Frequency), 0, _total - _warmupCount);
            series.Add(new
            {
                Timestamp = now, Completed = Interlocked.Read(ref _completed), Measured = measured,
                OfferedBySchedule = expected, Backlog = Math.Max(0, expected - measured),
                CpuTicks = _process.TotalProcessorTime.Ticks, AllocatedBytes = GC.GetTotalAllocatedBytes(false),
                HeapBytes = GC.GetTotalMemory(false), RssBytes = _process.WorkingSet64,
                Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2)
            });
            try { await Task.Delay(1000, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }

    private static double Percentile(ReadOnlySpan<long> values, double percentile)
        => values[(int)Math.Ceiling(values.Length * percentile) - 1] * 1_000_000_000d / Stopwatch.Frequency;
}
