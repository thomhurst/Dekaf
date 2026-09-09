using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

var role = args[0];
var topic = args[1];
var folder = Path.GetFullPath(args[2]);
var mode = args[3];
var warmup = int.Parse(args[4]);
var seconds = int.Parse(args[5]);
var rate = int.Parse(args[6]);
Directory.CreateDirectory(folder);
if (role == "produce") await ShareLoad.Produce(topic, folder, warmup, seconds, rate);
else if (role == "consume") await new ShareLoad().Consume(topic, folder, mode, warmup, seconds, rate);
else throw new ArgumentException("Unknown role.");

internal readonly record struct Payload(int Sequence, long Scheduled);
internal sealed class PayloadDeserializer : IDeserializer<Payload>
{
    public Payload Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        => new(BinaryPrimitives.ReadInt32LittleEndian(data.Span), BinaryPrimitives.ReadInt64LittleEndian(data.Span[8..]));
}

internal sealed class ShareLoad
{
    private const int Burst = 128;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Process _accounting = Process.GetCurrentProcess();
    private long[] _scheduled = null!;
    private long[] _latencies = null!;
    private Sample[] _series = null!;
    private int _seriesCount, _total, _warmupCount, _rate;
    private long _processed, _completed, _measured, _failures, _commits;
    private long _firstCompletion, _measurementStart, _measurementEnd, _scheduledStart;
    private long _cpuStart, _cpuEnd, _allocatedStart, _allocatedEnd, _jitStart, _jitEnd;
    private double _jitMsStart, _jitMsEnd;

    internal static async Task Produce(string topic, string folder, int warmup, int seconds, int rate)
    {
        await using var producer = new KafkaProducer<int, byte[]>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"], LingerMs = 1
        }, Serializers.Int32, Serializers.ByteArray);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(warmup + seconds + 180));
        await producer.InitializeAsync(deadline.Token);
        while (!File.Exists(Path.Combine(folder, "ready"))) await Task.Delay(20, deadline.Token);
        var payload = new byte[256];
        var total = checked((warmup + seconds) * rate);
        var start = Stopwatch.GetTimestamp();
        long acknowledged = 0, failed = 0, maxLateness = 0;
        Action<RecordMetadata, Exception?> callback = (_, error) =>
        {
            if (error is null) Interlocked.Increment(ref acknowledged);
            else Interlocked.Increment(ref failed);
        };
        for (var index = 0; index < total; index++)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var scheduled = start + (long)((index / Burst * Burst) * (double)Stopwatch.Frequency / rate);
            if (index % Burst == 0 && scheduled > Stopwatch.GetTimestamp())
                await Task.Delay(TimeSpan.FromSeconds((scheduled - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency), deadline.Token);
            maxLateness = Math.Max(maxLateness, Stopwatch.GetTimestamp() - scheduled);
            BinaryPrimitives.WriteInt32LittleEndian(payload, index);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), scheduled);
            await producer.FireAsync(new ProducerMessage<int, byte[]>
            {
                Topic = topic, Partition = 0, Key = index % 1024, Value = payload
            }, callback);
        }
        await producer.FlushAsync(deadline.Token);
        Save(folder, "producer.json", new { Sent = total, Acknowledged = acknowledged, Failed = failed,
            Rate = rate, OfferBurst = Burst, ScheduledStart = start, StopwatchFrequency = Stopwatch.Frequency,
            ElapsedSeconds = Stopwatch.GetElapsedTime(start).TotalSeconds,
            MaxSendLatenessMs = maxLateness * 1000d / Stopwatch.Frequency });
        if (acknowledged != total || failed != 0) throw new InvalidOperationException("Producer count mismatch.");
    }

    internal async Task Consume(string topic, string folder, string mode, int warmup, int seconds, int rate)
    {
        if (mode is not ("legacy" or "batch") || rate % Burst != 0) throw new ArgumentException("Invalid workload.");
        _rate = rate;
        _warmupCount = checked(warmup * rate);
        _total = checked((warmup + seconds) * rate);
        _scheduled = new long[_total];
        _latencies = new long[_total];
        _series = new Sample[warmup + seconds + 190];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(warmup + seconds + 180));
        using var stopSampler = new CancellationTokenSource();
        await using (var admin = Kafka.CreateAdminClient().WithBootstrapServers("localhost:9092").Build())
        {
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = topic + "-group" }] =
                    [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
            }, cancellationToken: deadline.Token);
        }
        await using var consumer = await Kafka.CreateShareConsumer<int, Payload>()
            .WithBootstrapServers("localhost:9092").WithGroupId(topic + "-group")
            .WithMaxPollRecords(Burst).WithFetchMaxWaitMs(10)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithAcknowledgementCommitCallback(Acknowledged)
            .WithKeyDeserializer(Serializers.Int32).WithValueDeserializer(new PayloadDeserializer())
            .BuildAsync(deadline.Token);
        consumer.Subscribe(topic);
        var observing = Observe(stopSampler.Token);
        File.WriteAllText(Path.Combine(folder, "ready"), "ready");
        long stopTicks = 0;
        try
        {
#if CANDIDATE
            if (mode == "batch")
            {
                await foreach (var batch in consumer.PollBatchesAsync(deadline.Token))
                {
                    foreach (var record in batch)
                    {
                        ProcessRecord(record.Key, record.Value, record.Partition, record.Offset);
                        batch.Acknowledge(record);
                        if (_processed % Burst == 0)
                        {
                            await consumer.CommitAsync(deadline.Token);
                            _commits++;
                        }
                    }
                    if (_processed == _total) break;
                }
            }
            else
#endif
            {
                await foreach (var record in consumer.PollAsync(deadline.Token))
                {
                    ProcessRecord(record.Key, record.Value, record.Partition, record.Offset);
                    consumer.Acknowledge(record);
                    if (_processed % Burst == 0)
                    {
                        await consumer.CommitAsync(deadline.Token);
                        _commits++;
                    }
                    if (_processed == _total) break;
                }
            }
            await consumer.CommitAsync(deadline.Token);
            if (_completed != _total || _failures != 0) throw new InvalidOperationException("Incomplete broker acknowledgements.");
            var stopStart = Stopwatch.GetTimestamp();
            await consumer.CloseAsync(deadline.Token);
            stopTicks = Stopwatch.GetTimestamp() - stopStart;
        }
        finally
        {
            await stopSampler.CancelAsync();
            await observing;
            Save(folder, "series.json", _series.AsSpan(0, _seriesCount).ToArray());
            using var raw = File.Create(Path.Combine(folder, "all-latency-ticks.bin"));
            raw.Write(MemoryMarshal.AsBytes(_latencies.AsSpan()));
        }
        if (_processed != _total || _completed != _total || _measured != seconds * (long)rate
            || _latencies.Any(static value => value <= 0) || _commits != _total / Burst || _failures != 0)
            throw new InvalidOperationException("Invalid final workload counts.");
        var measured = _latencies.AsSpan(_warmupCount);
        using (var raw = File.Create(Path.Combine(folder, "latency-ticks.bin"))) raw.Write(MemoryMarshal.AsBytes(measured));
        measured.Sort();
        var elapsed = (_measurementEnd - _measurementStart) / (double)Stopwatch.Frequency;
        Save(folder, "metrics.json", new
        {
            Mode = mode, Processed = _processed, Completed = _completed, Measured = _measured, Failures = _failures,
            WarmupCompleted = _completed - _measured, ActualWarmupSeconds = (_measurementStart - _firstCompletion) / (double)Stopwatch.Frequency,
            OfferedMessagesPerSecond = rate, MeasuredDurationSeconds = elapsed, MessagesPerSecond = _measured / elapsed,
            CpuNsPerMessage = (_cpuEnd - _cpuStart) * 100d / _measured,
            AllocatedBytesPerMessage = (_allocatedEnd - _allocatedStart) / (double)_measured,
            CpuTicksStart = _cpuStart, CpuTicksEnd = _cpuEnd,
            AllocatedBytesStart = _allocatedStart, AllocatedBytesEnd = _allocatedEnd,
            P50Ns = Percentile(measured, .5), P99Ns = Percentile(measured, .99),
            MaxNs = measured[^1] * 1e9 / Stopwatch.Frequency, StopwatchFrequency = Stopwatch.Frequency,
            MeasurementStart = _measurementStart, MeasurementEnd = _measurementEnd,
            JitMethodsStart = _jitStart, JitMethodsEnd = _jitEnd, JitMsStart = _jitMsStart, JitMsEnd = _jitMsEnd,
            ExplicitCommits = _commits, BacklogAtEnd = _total - _completed,
            PostDrainCloseNs = stopTicks * 1e9 / Stopwatch.Frequency,
            Scope = "Consumer process including observer. Each completion is a successful broker acknowledgement callback. " +
                "Latency spans scheduled producer offer to that callback. Broker and baseline producer are separate. " +
                "Explicit commit every 128 processed records; inline acknowledgements may complete earlier. " +
                "Post-drain close is not shutdown with unfinished work."
        });
        _process.Dispose();
        _accounting.Dispose();
    }

    private void ProcessRecord(int key, Payload payload, int partition, long offset)
    {
        var index = payload.Sequence;
        if ((uint)index >= _total || partition != 0 || offset != index || key != index % 1024
            || Interlocked.CompareExchange(ref _scheduled[index], payload.Scheduled, 0) != 0)
            throw new InvalidOperationException("Invalid or duplicate payload.");
        Interlocked.CompareExchange(ref _scheduledStart, payload.Scheduled -
            (long)((index / Burst * Burst) * (double)Stopwatch.Frequency / _rate), 0);
        Interlocked.Increment(ref _processed);
    }

    private void Acknowledged(ReadOnlySpan<ShareAcknowledgementCommitResult> results)
    {
        foreach (var result in results)
        {
            if (!result.Succeeded || result.TopicPartition.Partition != 0)
            {
                Interlocked.Increment(ref _failures);
                continue;
            }
            foreach (var offset in result.Offsets)
            {
                if ((ulong)offset >= (ulong)_total || Volatile.Read(ref _scheduled[offset]) == 0)
                {
                    Interlocked.Increment(ref _failures);
                    continue;
                }
                var now = Stopwatch.GetTimestamp();
                Interlocked.CompareExchange(ref _firstCompletion, now, 0);
                if (offset >= _warmupCount && Interlocked.CompareExchange(ref _measurementStart, -1, 0) == 0)
                {
                    _accounting.Refresh();
                    _cpuStart = _accounting.TotalProcessorTime.Ticks;
                    _allocatedStart = GC.GetTotalAllocatedBytes(true);
                    _jitStart = System.Runtime.JitInfo.GetCompiledMethodCount();
                    _jitMsStart = System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds;
                    Volatile.Write(ref _measurementStart, Stopwatch.GetTimestamp());
                }
                if (Interlocked.CompareExchange(ref _latencies[offset], Math.Max(1, now - _scheduled[offset]), 0) != 0)
                {
                    Interlocked.Increment(ref _failures);
                    continue;
                }
                if (offset >= _warmupCount) Interlocked.Increment(ref _measured);
                if (Interlocked.Increment(ref _completed) == _total)
                {
                    _measurementEnd = Stopwatch.GetTimestamp();
                    _accounting.Refresh();
                    _cpuEnd = _accounting.TotalProcessorTime.Ticks;
                    _allocatedEnd = GC.GetTotalAllocatedBytes(true);
                    _jitEnd = System.Runtime.JitInfo.GetCompiledMethodCount();
                    _jitMsEnd = System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds;
                }
            }
        }
    }

    private async Task Observe(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_seriesCount == _series.Length) throw new InvalidOperationException("Sampler storage exhausted.");
            _process.Refresh();
            var now = Stopwatch.GetTimestamp();
            var completed = Interlocked.Read(ref _completed);
            var expected = _scheduledStart == 0 ? -1 : Math.Clamp(
                ((long)((now - _scheduledStart) * (double)_rate / Stopwatch.Frequency) / Burst + 1) * Burst, 0, _total);
            _series[_seriesCount++] = new Sample(now, Interlocked.Read(ref _processed), completed, Interlocked.Read(ref _measured),
                expected, expected < 0 ? -1 : Math.Max(0, expected - completed), _process.TotalProcessorTime.Ticks,
                GC.GetTotalAllocatedBytes(false), GC.GetTotalMemory(false), _process.WorkingSet64,
                GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                System.Runtime.JitInfo.GetCompiledMethodCount(), System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds,
                ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount);
            try { await Task.Delay(1000, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }

    private readonly record struct Sample(long Timestamp, long Processed, long Completed, long Measured,
        long OfferedBySchedule, long Backlog, long CpuTicks, long AllocatedBytes, long HeapBytes, long RssBytes,
        int Gen0, int Gen1, int Gen2, long JitMethods, double JitMs, int Threads, long PendingWork);
    private static double Percentile(ReadOnlySpan<long> values, double percentile)
        => values[(int)Math.Ceiling(values.Length * percentile) - 1] * 1e9 / Stopwatch.Frequency;
    private static void Save<T>(string folder, string name, T value)
        => File.WriteAllText(Path.Combine(folder, name), JsonSerializer.Serialize(value));
}
