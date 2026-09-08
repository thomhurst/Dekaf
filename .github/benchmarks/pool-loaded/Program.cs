using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;

// Experimental fixture only. No product acceptance follows from a local smoke run.
internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 7)
            throw new ArgumentException("bootstrap output topic messageBytes partitions warmupSeconds measuredSeconds");
        var directory = Path.GetFullPath(args[1]);
        if (Directory.Exists(directory))
            throw new IOException("Output directory already exists.");
        Directory.CreateDirectory(directory);
        using var compilations = new CompilationLog(Path.Combine(directory, "compilations.json"));
        compilations.Phase("initialize");
        var size = int.Parse(args[3]);
        var partitions = int.Parse(args[4]);
        var warmup = int.Parse(args[5]);
        var measured = int.Parse(args[6]);
        if (size < 32 || partitions < 1 || warmup < 1 || measured < 1)
            throw new ArgumentOutOfRangeException(nameof(args));
        var assembly = typeof(Kafka).Assembly;
        await File.WriteAllTextAsync(Path.Combine(directory, "identity.json"), JsonSerializer.Serialize(new
        {
            Args = args, assembly.FullName, assembly.Location,
            ProductSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
            Runtime = Environment.Version.ToString(), Environment.ProcessorCount,
            ServerGc = System.Runtime.GCSettings.IsServerGC,
            Stopwatch.Frequency
        }));
        const int primerSeconds = 20;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(primerSeconds + warmup + measured + 120));
        var admin = Kafka.CreateAdminClient().WithBootstrapServers(args[0]).Build();
        await using (var adminLifetime = new ObservedDisposal(admin, directory, "admin"))
        {
            await admin.CreateTopicsAsync([new NewTopic
            {
                Name = args[2], NumPartitions = partitions, ReplicationFactor = 1
            }], cancellationToken: timeout.Token);
        }

        var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(args[0]).WithClientId("pool-loaded-producer")
            .WithAcks(Acks.All).WithIdempotence(true).WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(128 * 1024).WithBufferMemory(64UL * 1024 * 1024)
            .WithConnectionsPerBroker(1).WithoutAdaptiveConnections().BuildAsync(timeout.Token);
        await using var producerLifetime = new ObservedDisposal(producer, directory, "producer");
        var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(args[0]).WithClientId("pool-loaded-consumer")
            .WithGroupId("pool-loaded-group").WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual).BuildAsync(timeout.Token);
        await using var consumerLifetime = new ObservedDisposal(consumer, directory, "consumer");
        consumer.Partitions.Assign(Enumerable.Range(0, partitions).Select(p => new TopicPartition(args[2], p)).ToArray());
        var workload = new Workload(args[2], size, partitions, primerSeconds, warmup, measured);
        using var consuming = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var consumerTask = workload.ConsumeAsync(consumer, consuming.Token);
        try
        {
            // Exercise complete collection and serialization before the workload warmup.
            // Background tiered compilation from the first report must not begin in measurement.
            compilations.Phase("primer");
            await workload.RunPhaseAsync(producer, 0, primerSeconds, "primer", directory, timeout.Token);
            compilations.Phase("warmup");
            await workload.RunPhaseAsync(producer, 1, warmup, "warmup", directory, timeout.Token);
            compilations.Phase("measured");
            await workload.RunPhaseAsync(producer, 2, measured, "measured", directory, timeout.Token);
            compilations.Phase("finalize");
        }
        finally
        {
            consuming.Cancel();
            await consumerTask;
            await File.WriteAllTextAsync(Path.Combine(directory, "completion.json"), JsonSerializer.Serialize(workload.Completion));
        }
        workload.ThrowIfFailed();
    }
}

// Disposal observations are outside both measured phases. Preserve default client
// disposal behavior and identify which stage is active if an external ceiling fires.
internal sealed class ObservedDisposal(IAsyncDisposable resource, string directory, string name) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        var started = DateTimeOffset.UtcNow;
        await File.WriteAllTextAsync(Path.Combine(directory, "shutdown-" + name + "-start.json"),
            JsonSerializer.Serialize(new { StartedAtUtc = started }));
        var timer = Stopwatch.StartNew();
        string? error = null;
        try
        {
            await resource.DisposeAsync();
        }
        catch (Exception failure)
        {
            error = failure.ToString();
            throw;
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "shutdown-" + name + ".json"),
                JsonSerializer.Serialize(new { StartedAtUtc = started, Seconds = timer.Elapsed.TotalSeconds, Error = error }));
        }
    }
}

internal sealed class Workload
{
    private const int Capacity = 1024;
    private readonly Channel<Slot> _available = Channel.CreateBounded<Slot>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false
    });
    private readonly long[] _sentByPartition;
    private readonly long[] _receivedByPartition;
    private readonly Slot[] _slots = new Slot[Capacity];
    private readonly Phase[] _phases;
    private Exception? _failure;

    public Workload(string topic, int size, int partitions, int primerSeconds, int warmupSeconds, int measuredSeconds)
    {
        // Include the entire 30-second drain and the final boundary interval.
        _phases = [new(checked(primerSeconds + 31)), new(checked(warmupSeconds + 31)), new(checked(measuredSeconds + 31))];
        _sentByPartition = new long[partitions];
        _receivedByPartition = new long[partitions];
        for (var index = 0; index < Capacity; index++)
        {
            _slots[index] = new Slot(this, topic, size, index % partitions, index);
            if (!_available.Writer.TryWrite(_slots[index]))
                throw new InvalidOperationException("Initial slot capacity mismatch.");
        }
    }

    public object Completion => new
    {
        SentByPartition = _sentByPartition, ReceivedByPartition = _receivedByPartition,
        Error = _failure?.ToString(), AvailableSlots = _available.Reader.Count,
        Phases = _phases.Select(p => new { p.Sent, p.Acknowledged, p.Consumed })
    };

    public void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } failure)
            throw new InvalidOperationException("Loaded fixture failed.", failure);
    }

    private void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref _failure, error, null);
        _available.Writer.TryComplete(error);
    }

    public async Task RunPhaseAsync(IKafkaProducer<string, byte[]> producer, int index, int seconds,
        string label, string directory, CancellationToken cancellationToken)
    {
        var phase = _phases[index];
        File.WriteAllText(Path.Combine(directory, label + "-start.json"), JsonSerializer.Serialize(new
        {
            Environment.ProcessId, StartedUtc = DateTimeOffset.UtcNow
        }));
        using var process = Process.GetCurrentProcess();
        var startCpu = process.TotalProcessorTime.TotalMilliseconds;
        var startAllocation = GC.GetTotalAllocatedBytes(precise: true);
        phase.StartTimestamp = Stopwatch.GetTimestamp();
        var timer = Stopwatch.StartNew();
        using var sampling = new CancellationTokenSource();
        var samples = new List<RuntimeSample>(seconds + 40);
        var sampler = SampleAsync(phase, samples, sampling.Token);
        using var ingress = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopIngress = StopIngressAsync(ingress, timer, seconds);
        var offering = OfferAsync(producer, phase, index, ingress.Token);
        double offeredSeconds = 0;
        var drained = false;
        try
        {
            try
            {
                await offering.WaitAsync(ingress.Token);
            }
            catch (OperationCanceledException) when (ingress.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Stop waiting for admission, then retain the final FireAsync until drain.
            }
            cancellationToken.ThrowIfCancellationRequested();
            offeredSeconds = timer.Elapsed.TotalSeconds;
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drain.CancelAfter(TimeSpan.FromSeconds(30));
            await producer.FlushAsync(drain.Token);
            await offering.WaitAsync(drain.Token);
            while (Volatile.Read(ref phase.Consumed) != phase.Sent || Volatile.Read(ref phase.Acknowledged) != phase.Sent
                || _available.Reader.Count != Capacity)
            {
                ThrowIfFailed();
                await Task.Delay(1, drain.Token);
            }
            ThrowIfFailed();
            if (_available.Reader.Count != Capacity)
                throw new InvalidOperationException("Delivery completion left occupied slots.");
            drained = true;
        }
        catch (Exception error)
        {
            Fail(error);
            throw;
        }
        finally
        {
            ingress.Cancel();
            await stopIngress;
            if (!offering.IsCompletedSuccessfully)
                _ = offering.ContinueWith(static task => _ = task.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var elapsed = Stopwatch.GetElapsedTime(phase.StartTimestamp).TotalSeconds;
            var cpu = process.TotalProcessorTime.TotalMilliseconds - startCpu;
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - startAllocation;
            sampling.Cancel();
            await sampler;
            var deliveryIntervals = phase.DeliveryIntervals.GetSnapshot();
            var completionIntervals = phase.CompletionIntervals.GetSnapshot();
            var deliveryBlocks = phase.DeliveryBlocks.GetSnapshot();
            var completionBlocks = phase.CompletionBlocks.GetSnapshot();
            var intervalsComplete = drained
                && deliveryIntervals.OutsideCapacity.Count == 0 && completionIntervals.OutsideCapacity.Count == 0
                && deliveryIntervals.Intervals.Sum(interval => interval.Count) == phase.Acknowledged
                && completionIntervals.Intervals.Sum(interval => interval.Count) == phase.Consumed
                && deliveryBlocks.MatchesIntervalCounts(deliveryIntervals)
                && completionBlocks.MatchesIntervalCounts(completionIntervals);
            if (drained && !intervalsComplete)
                Fail(new InvalidOperationException("Interval observations exceeded capacity or lost completions."));
            await File.WriteAllTextAsync(Path.Combine(directory, label + ".json"), JsonSerializer.Serialize(new
            {
                Seconds = elapsed, OfferedSeconds = offeredSeconds, ConfiguredSeconds = seconds,
                phase.Sent, phase.Acknowledged, phase.Consumed,
                CompletedPerSecond = phase.Consumed / elapsed,
                CpuMs = cpu, CpuUsPerCompleted = cpu * 1000 / Math.Max(1, phase.Consumed),
                AllocatedBytes = allocated, BytesPerCompleted = (double)allocated / Math.Max(1, phase.Consumed),
                DeliveryLatency = phase.Delivery.GetSnapshot(), CompletionLatency = phase.Completion.GetSnapshot(),
                IntervalsStableAfterDrain = drained,
                IntervalCaptureComplete = intervalsComplete,
                DeliveryIntervals = deliveryIntervals, CompletionIntervals = completionIntervals,
                DeliveryBlocks = deliveryBlocks, CompletionBlocks = completionBlocks,
                Samples = samples, Failure = _failure?.ToString()
            }), CancellationToken.None);
        }
        ThrowIfFailed();
    }

    private async Task OfferAsync(IKafkaProducer<string, byte[]> producer, Phase phase, int index,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ThrowIfFailed();
            var started = Stopwatch.GetTimestamp();
            Slot slot;
            try
            {
                slot = await _available.Reader.ReadAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            slot.Prepare(phase, index, started, _sentByPartition[slot.Partition]++);
            await producer.FireAsync(slot.Message, slot.OnDelivered);
            Interlocked.Increment(ref phase.Sent);
        }
    }

    private static async Task StopIngressAsync(CancellationTokenSource ingress, Stopwatch timer, int seconds)
    {
        try
        {
            while (timer.Elapsed.TotalSeconds < seconds)
            {
                var remaining = TimeSpan.FromSeconds(seconds) - timer.Elapsed;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : remaining,
                    ingress.Token);
            }
            ingress.Cancel();
        }
        catch (OperationCanceledException) when (ingress.IsCancellationRequested)
        {
            return;
        }
    }

    public async Task ConsumeAsync(IKafkaConsumer<string, byte[]> consumer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var record in consumer.ConsumeAsync(cancellationToken))
            {
                var value = record.Value ?? throw new InvalidOperationException("Null payload.");
                var started = BinaryPrimitives.ReadInt64LittleEndian(value);
                var sequence = BinaryPrimitives.ReadInt64LittleEndian(value.AsSpan(8));
                var phaseIndex = BinaryPrimitives.ReadInt32LittleEndian(value.AsSpan(16));
                var slotIndex = BinaryPrimitives.ReadInt32LittleEndian(value.AsSpan(20));
                var partition = record.Partition;
                if ((uint)phaseIndex >= _phases.Length || (uint)slotIndex >= _slots.Length
                    || sequence != _receivedByPartition[partition]++
                    || record.Offset != sequence || value[24] != 0x5a || value[^1] != 0xa5)
                    throw new InvalidOperationException("Duplicate, missing, reordered or corrupted record.");
                var phase = _phases[phaseIndex];
                var completed = Stopwatch.GetTimestamp();
                phase.Completion.RecordTicks(completed - started);
                phase.CompletionIntervals.RecordTicks(completed - started, completed - phase.StartTimestamp);
                phase.CompletionBlocks.RecordTicks(completed - started, completed - phase.StartTimestamp);
                Interlocked.Increment(ref phase.Consumed);
                _slots[slotIndex].Consumed(sequence);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private static async Task SampleAsync(Phase phase,
        List<RuntimeSample> samples, CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        using var tick = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                process.Refresh();
                samples.Add(new RuntimeSample(Stopwatch.GetElapsedTime(phase.StartTimestamp).TotalSeconds,
                    Volatile.Read(ref phase.Sent), Volatile.Read(ref phase.Acknowledged), Volatile.Read(ref phase.Consumed),
                    process.TotalProcessorTime.TotalMilliseconds, GC.GetTotalAllocatedBytes(), GC.GetTotalMemory(false),
                    process.WorkingSet64, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                    System.Runtime.JitInfo.GetCompiledMethodCount(), System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds,
                    ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount,
                    phase.Delivery.GetSnapshot(), phase.Completion.GetSnapshot()));
            } while (await tick.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private sealed class Phase(int capacitySeconds)
    {
        public long StartTimestamp;
        public long Sent;
        public long Acknowledged;
        public long Consumed;
        public readonly LatencyTracker Delivery = new();
        public readonly LatencyTracker Completion = new();
        public readonly IntervalLatency DeliveryIntervals = new(capacitySeconds);
        public readonly IntervalLatency CompletionIntervals = new(capacitySeconds);
        public readonly BlockHistogram DeliveryBlocks = new(capacitySeconds);
        public readonly BlockHistogram CompletionBlocks = new(capacitySeconds);
    }

    private sealed class Slot
    {
        private readonly Workload _owner;
        private readonly byte[] _payload;
        private Phase _phase = null!;
        private long _started;
        private long _sequence;
        private int _completions;
        public readonly int Partition;
        public readonly ProducerMessage<string, byte[]> Message;
        public readonly Action<RecordMetadata, Exception?> OnDelivered;

        public Slot(Workload owner, string topic, int size, int partition, int index)
        {
            _owner = owner;
            Partition = partition;
            _payload = new byte[size];
            _payload[24] = 0x5a;
            _payload[^1] = 0xa5;
            BinaryPrimitives.WriteInt32LittleEndian(_payload.AsSpan(20), index);
            Message = new ProducerMessage<string, byte[]> { Topic = topic, Partition = partition, Value = _payload };
            OnDelivered = Delivered;
        }

        public void Prepare(Phase phase, int phaseIndex, long started, long sequence)
        {
            _phase = phase;
            _started = started;
            _sequence = sequence;
            _completions = 0;
            BinaryPrimitives.WriteInt64LittleEndian(_payload, started);
            BinaryPrimitives.WriteInt64LittleEndian(_payload.AsSpan(8), sequence);
            BinaryPrimitives.WriteInt32LittleEndian(_payload.AsSpan(16), phaseIndex);
        }

        private void Delivered(RecordMetadata metadata, Exception? error)
        {
            if (error is not null)
                _owner.Fail(error);
            else if (metadata.Partition != Partition || metadata.Offset != _sequence)
                _owner.Fail(new InvalidOperationException("Incorrect delivery metadata."));
            else
            {
                var completed = Stopwatch.GetTimestamp();
                _phase.Delivery.RecordTicks(completed - _started);
                _phase.DeliveryIntervals.RecordTicks(completed - _started, completed - _phase.StartTimestamp);
                _phase.DeliveryBlocks.RecordTicks(completed - _started, completed - _phase.StartTimestamp);
                Interlocked.Increment(ref _phase.Acknowledged);
            }
            Complete();
        }

        public void Consumed(long sequence)
        {
            if (sequence != _sequence)
                _owner.Fail(new InvalidOperationException("Incorrect consumed slot."));
            Complete();
        }

        private void Complete()
        {
            var completed = Interlocked.Increment(ref _completions);
            if (completed == 1)
                return;
            if (completed != 2 || !_owner._available.Writer.TryWrite(this))
                _owner.Fail(new InvalidOperationException("Duplicate slot return."));
        }
    }

    private sealed record RuntimeSample(double Seconds, long Sent, long Acknowledged, long Consumed,
        double CpuMs, long AllocatedBytes, long HeapBytes, long RssBytes, int Gc0, int Gc1, int Gc2,
        long JitMethods, double JitMs, int Threads, long PendingWork,
        LatencySnapshot DeliveryLatency, LatencySnapshot CompletionLatency);
}
