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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(warmup + measured + 120));
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(args[0]).Build();
        await admin.CreateTopicsAsync([new NewTopic
        {
            Name = args[2], NumPartitions = partitions, ReplicationFactor = 1
        }], cancellationToken: timeout.Token);

        await using var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(args[0]).WithClientId("pool-loaded-producer")
            .WithAcks(Acks.All).WithIdempotence(true).WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(128 * 1024).WithBufferMemory(64UL * 1024 * 1024)
            .WithConnectionsPerBroker(1).WithoutAdaptiveConnections().BuildAsync(timeout.Token);
        await using var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(args[0]).WithClientId("pool-loaded-consumer")
            .WithGroupId("pool-loaded-group").WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual).BuildAsync(timeout.Token);
        consumer.Partitions.Assign(Enumerable.Range(0, partitions).Select(p => new TopicPartition(args[2], p)).ToArray());
        var workload = new Workload(args[2], size, partitions);
        using var consuming = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var consumerTask = workload.ConsumeAsync(consumer, consuming.Token);
        try
        {
            await workload.RunPhaseAsync(producer, 0, warmup, directory, timeout.Token);
            await workload.RunPhaseAsync(producer, 1, measured, directory, timeout.Token);
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
    private readonly Phase[] _phases = [new(), new()];
    private Exception? _failure;

    public Workload(string topic, int size, int partitions)
    {
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
        string directory, CancellationToken cancellationToken)
    {
        var phase = _phases[index];
        using var process = Process.GetCurrentProcess();
        var startCpu = process.TotalProcessorTime.TotalMilliseconds;
        var startAllocation = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        using var sampling = new CancellationTokenSource();
        var samples = new List<RuntimeSample>(seconds + 40);
        var sampler = SampleAsync(phase, timer, samples, sampling.Token);
        double offeredSeconds = 0;
        try
        {
            while (timer.Elapsed.TotalSeconds < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfFailed();
                var started = Stopwatch.GetTimestamp();
                var slot = await _available.Reader.ReadAsync(cancellationToken);
                slot.Prepare(phase, index, started, _sentByPartition[slot.Partition]++);
                await producer.FireAsync(slot.Message, slot.OnDelivered);
                Interlocked.Increment(ref phase.Sent);
            }
            offeredSeconds = timer.Elapsed.TotalSeconds;
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drain.CancelAfter(TimeSpan.FromSeconds(30));
            await producer.FlushAsync(drain.Token);
            while (Volatile.Read(ref phase.Consumed) != phase.Sent || Volatile.Read(ref phase.Acknowledged) != phase.Sent
                || _available.Reader.Count != Capacity)
            {
                ThrowIfFailed();
                await Task.Delay(1, drain.Token);
            }
            ThrowIfFailed();
            if (_available.Reader.Count != Capacity)
                throw new InvalidOperationException("Delivery completion left occupied slots.");
        }
        catch (Exception error)
        {
            Fail(error);
            throw;
        }
        finally
        {
            var elapsed = timer.Elapsed.TotalSeconds;
            var cpu = process.TotalProcessorTime.TotalMilliseconds - startCpu;
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - startAllocation;
            sampling.Cancel();
            await sampler;
            var label = index == 0 ? "warmup" : "measured";
            await File.WriteAllTextAsync(Path.Combine(directory, label + ".json"), JsonSerializer.Serialize(new
            {
                Seconds = elapsed, OfferedSeconds = offeredSeconds, ConfiguredSeconds = seconds,
                phase.Sent, phase.Acknowledged, phase.Consumed,
                CompletedPerSecond = phase.Consumed / elapsed,
                CpuMs = cpu, CpuUsPerCompleted = cpu * 1000 / Math.Max(1, phase.Consumed),
                AllocatedBytes = allocated, BytesPerCompleted = (double)allocated / Math.Max(1, phase.Consumed),
                DeliveryLatency = phase.Delivery.GetSnapshot(), CompletionLatency = phase.Completion.GetSnapshot(),
                Samples = samples, Failure = _failure?.ToString()
            }), CancellationToken.None);
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
                phase.Completion.RecordTicks(Stopwatch.GetTimestamp() - started);
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

    private static async Task SampleAsync(Phase phase, Stopwatch timer,
        List<RuntimeSample> samples, CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        using var tick = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                process.Refresh();
                samples.Add(new RuntimeSample(timer.Elapsed.TotalSeconds,
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

    private sealed class Phase
    {
        public long Sent;
        public long Acknowledged;
        public long Consumed;
        public readonly LatencyTracker Delivery = new();
        public readonly LatencyTracker Completion = new();
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
                _phase.Delivery.RecordTicks(Stopwatch.GetTimestamp() - _started);
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
