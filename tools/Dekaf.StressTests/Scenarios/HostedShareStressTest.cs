using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dekaf.Admin;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.StressTests.Scenarios;

/// <summary>
/// Measures a bounded live producer and two keyed hosted share consumers together.
/// Share groups cannot seek a fixed replay corpus. CPU and allocations therefore include
/// the feeder, serialization and both workers; latency ends at successful processing.
/// </summary>
internal sealed class HostedShareStressTest(bool telemetryEnabled = false) : IStressTestScenario
{
    public string Name => telemetryEnabled ? "hosted-share-telemetry" : "hosted-share";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MessageSizeBytes, 16);
        if (options.BrokerCount != 1 || options.ConnectionsPerBroker != 1 || options.Compression != "none")
            throw new ArgumentException("Hosted-share requires one broker, one producer connection and no compression.");
        var group = $"stress-hosted-share-{Guid.NewGuid():N}";
        await using (var admin = Kafka.CreateAdminClient().WithBootstrapServers(options.BootstrapServers).Build())
        {
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                    [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        using var telemetry = telemetryEnabled ? new ShareTelemetryObserver(options.TelemetryReceiverEndpoint
            ?? throw new ArgumentException("Subscribed stress requires a broker telemetry receiver.")) : null;
        if (telemetry is not null)
            await telemetry.ConfigureAsync(options.BootstrapServers, cancellationToken).ConfigureAwait(false);
        var state = new HostedShareWorkloadState(options.Topic, options.MessageSizeBytes);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(state);
        builder.Services.AddDekaf(dekaf => dekaf
            .AddShareConsumerService<Worker, string, byte[]>("first", consumer => Configure(consumer, 0))
            .AddShareConsumerService<Worker, string, byte[]>("second", consumer => Configure(consumer, 1)));
        using var host = builder.Build();
        var producerBuilder = Kafka.CreateProducer<string, byte[]>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithIdempotence(true).WithAcks(Acks.All)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs)).WithBatchSize(options.BatchSize)
            .WithConnectionsPerBroker(1).WithoutAdaptiveConnections();
        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(producerBuilder, options);
        await using var producer = await producerBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        var workers = host.Services.GetServices<IHostedService>().OfType<Worker>().ToArray();
        if (workers.Length != 2 || ReferenceEquals(workers[0].Consumer, workers[1].Consumer))
            throw new InvalidOperationException("The two keyed registrations did not create independent workers.");

        var measured = new ThroughputTracker();
        StressTestResult result;
        var workloadComplete = false;
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"  Warming up hosted-share: {options.ProducerWarmupSeconds}s, two keyed workers, {HostedShareWorkloadState.WindowSize} outstanding records maximum.");
            measured.Warmup = await ProducerWarmup.RunAsync(options.ProducerWarmupSeconds,
                RunCycleAsync, cancellationToken).ConfigureAwait(false);
            if (telemetry is not null)
            {
                for (var i = 0; i < workers.Length; i++) telemetry.ObserveIdentity(i, workers[i].Consumer);
                await telemetry.VerifyAsync(false, options.OutputDirectory, cancellationToken).ConfigureAwait(false);
            }
            var warmupProcessed = new[] { workers[0].Processed, workers[1].Processed };
            var started = DateTime.UtcNow;
            var run = await RunCycleAsync(TimeSpan.FromMinutes(options.DurationMinutes), measured, cancellationToken).ConfigureAwait(false);
            using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdown.CancelAfter(StressTestHelpers.OperationTimeout);
            await host.StopAsync(shutdown.Token).ConfigureAwait(false);
            for (var i = 0; i < workers.Length; i++)
            {
                var worker = workers[i];
                if (worker.ExecuteTask is { } execution) await execution.WaitAsync(shutdown.Token).ConfigureAwait(false);
                ValidateWorkerProgress(warmupProcessed[i], worker.Processed);
            }
            state.ThrowIfFailed();
            Console.WriteLine($"  Hosted workers stopped after processing {state.Completed:N0} unique records; duplicates={state.Duplicates:N0}.");
            result = CreateResult(options, started, run, measured, state.Latency, producer);
            workloadComplete = true;
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(StressTestHelpers.OperationTimeout);
            try { await host.StopAsync(shutdown.Token).ConfigureAwait(false); }
            finally
            {
                // Observe every cleanup even if another worker faults during disposal.
                try { await Task.WhenAll(workers.Select(worker => worker.DisposeAsync().AsTask())).ConfigureAwait(false); }
                finally
                {
                    if (!workloadComplete && telemetry is not null)
                        await telemetry.CaptureFailureAsync(options.OutputDirectory).ConfigureAwait(false);
                }
            }
        }

        if (telemetry is not null)
            result.ShareTelemetry = await telemetry.VerifyAsync(true, options.OutputDirectory, cancellationToken).ConfigureAwait(false);
        return result;

        void Configure(ShareConsumerBuilder<string, byte[]> consumer, int worker)
        {
            consumer.WithLoggerFactory(StressClientLogging.LoggerFactory)
                .WithBootstrapServers(options.BootstrapServers).WithGroupId(group)
                .WithAcknowledgementCommitCallback(state.ObserveAcknowledgements);
            if (telemetry is not null)
                consumer.WithClientId(telemetry.ClientIds[worker])
                    .RegisterMetricForSubscription(new ApplicationTelemetryMetric(ShareTelemetryObserver.ApplicationMetric,
                        ApplicationTelemetryMetricKind.Gauge, () => worker + 1));
        }

        async Task<ProducerWorkloadResult> RunCycleAsync(TimeSpan duration, ThroughputTracker tracker, CancellationToken token)
        {
            state.BeginCycle(tracker);
            StressTestHelpers.ResetProducerDeliveryDiagnostics(producer);
            var payload = new byte[options.MessageSizeBytes];
            if (tracker.Warmup is not null)
                Console.WriteLine($"  Running {Client} {Name} stress test for {options.DurationMinutes} minutes...");
            using var deliveryErrors = new DekafDeliveryErrorListener(tracker, options.Topic);
            using var gc = new GcStats();
            using var ingress = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var sampling = new CancellationTokenSource();
            tracker.Start();
            var stop = ProducerWorkload.StopIngressAsync(ingress, duration, TimeProvider.System);
            using var watchdog = options.ProgressWatchdog.Track(tracker, Client, Name);
            var sampler = StressTestHelpers.RunSamplerAsync(tracker, sampling.Token);
            var resources = StressTestHelpers.RunResourceMonitorAsync(sampling.Token);
            double workloadSeconds;
            try
            {
                while (!ingress.IsCancellationRequested)
                {
                    state.ThrowIfFailed();
                    if (!state.CanProduce)
                    {
                        await Task.Delay(1, token).ConfigureAwait(false);
                        continue;
                    }
                    var sequence = state.Produced;
                    BinaryPrimitives.WriteInt64LittleEndian(payload, sequence);
                    BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), Stopwatch.GetTimestamp());
                    // FireAsync copies the reusable payload before completing. This path has
                    // no cancellation overload; the outstanding window bounds admission.
                    await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(sequence), payload).ConfigureAwait(false);
                    state.RecordProduced();
                }
                workloadSeconds = tracker.Elapsed.TotalSeconds;
                using var drain = CancellationTokenSource.CreateLinkedTokenSource(token);
                drain.CancelAfter(StressTestHelpers.OperationTimeout);
                await producer.FlushAsync(drain.Token).ConfigureAwait(false);
                // Process publishes shared completion before the worker updates its own count.
                // Drain both counters so a late warmup increment cannot pass measured validation.
                while (state.Completed != state.Produced || workers[0].Processed + workers[1].Processed != state.Produced)
                {
                    state.ThrowIfFailed();
                    await Task.Delay(1, drain.Token).ConfigureAwait(false);
                }
                state.ThrowIfFailed();
            }
            catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException($"Hosted-share drain incomplete: produced={state.Produced}, completed={state.Completed}, duplicates={state.Duplicates}, workers={string.Join(",", workers.Select(worker => $"{worker.Processed}:{worker.ExecuteTask?.Status}"))}.", exception);
            }
            finally
            {
                ingress.Cancel();
                sampling.Cancel();
                await stop.ConfigureAwait(false);
                await Task.WhenAll(sampler, resources).ConfigureAwait(false);
                tracker.Stop();
                gc.Capture();
            }
            token.ThrowIfCancellationRequested();
            return new ProducerWorkloadResult(workloadSeconds, tracker.GetSnapshot(), gc.ToSnapshot());
        }
    }

    internal static void ValidateWorkerProgress(long warmupProcessed, long totalProcessed)
    {
        if (totalProcessed <= warmupProcessed)
            throw new InvalidOperationException("A keyed worker processed no records in the measured phase.");
    }

    internal StressTestResult CreateResult(StressTestOptions options, DateTime started,
        ProducerWorkloadResult run, ThroughputTracker measured, LatencyTracker latency,
        IKafkaProducer<string, byte[]> producer) => new()
    {
        Scenario = Name, Client = Client, DurationMinutes = options.DurationMinutes,
        MessageSizeBytes = options.MessageSizeBytes, BrokerCount = options.BrokerCount,
        StartedAtUtc = started, CompletedAtUtc = DateTime.UtcNow,
        Throughput = run.Throughput, GcStats = run.Gc, Latency = latency.GetSnapshot(),
        CpuTimeSeconds = measured.CpuTimeSeconds, ConsumedMessages = measured.MessageCount,
        Idempotent = true,
        ProducerDeliveryDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options)
            ?? throw new InvalidOperationException("Hosted-share results require producer delivery diagnostics.")
    };

    private sealed class Worker(IKafkaShareConsumer<string, byte[]> consumer, HostedShareWorkloadState state)
        : KafkaShareConsumerService<string, byte[]>(consumer,
            StressClientLogging.LoggerFactory.CreateLogger("HostedShareStress"))
    {
        public IKafkaShareConsumer<string, byte[]> Consumer { get; } = consumer;
        private long _processed;
        public long Processed => Volatile.Read(ref _processed);
        protected override IEnumerable<string> Topics => [state.Topic];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, byte[]> result, CancellationToken cancellationToken)
        {
            if (state.Process(result.Value)) Volatile.Write(ref _processed, _processed + 1);
            return ValueTask.CompletedTask;
        }
        protected override ValueTask OnErrorAsync(Exception exception, ShareConsumeResult<string, byte[]>? result, CancellationToken cancellationToken)
        {
            state.RecordFailure(exception);
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Fixed memory, exact per-slot generations, and unique completion counts across both workers.</summary>
internal sealed class HostedShareWorkloadState
{
    internal const int WindowSize = 16_384;
    private readonly long[] _completedSequences = new long[WindowSize];
    private readonly int _messageSize;
    private ThroughputTracker? _throughput;
    private Exception? _failure;
    private long _completed;
    private long _duplicates;
    public string Topic { get; }
    public long Produced { get; private set; }
    public long Completed => Interlocked.Read(ref _completed);
    public long Duplicates => Interlocked.Read(ref _duplicates);
    public LatencyTracker Latency { get; } = new();
    public bool CanProduce => Produced - Completed < WindowSize
        && Volatile.Read(ref _completedSequences[Produced % WindowSize]) == Produced - WindowSize;

    public HostedShareWorkloadState(string topic, int messageSize)
    {
        Topic = topic;
        _messageSize = messageSize;
        for (var i = 0; i < WindowSize; i++) _completedSequences[i] = i - WindowSize;
    }

    public void BeginCycle(ThroughputTracker throughput)
    {
        if (Completed != Produced) throw new InvalidOperationException("Previous hosted-share cycle has not drained.");
        ThrowIfFailed();
        Latency.Reset();
        _throughput = throughput;
    }

    public void RecordProduced() => Produced++;

    public bool Process(byte[]? payload)
    {
        if (payload is null || payload.Length != _messageSize || payload.Length < 16)
            throw new InvalidOperationException("Hosted-share payload size does not match the workload.");
        var sequence = BinaryPrimitives.ReadInt64LittleEndian(payload);
        if (sequence < 0) throw new InvalidOperationException("Hosted-share sequence is negative.");
        var timestamp = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(8));
        var ticks = Stopwatch.GetTimestamp() - timestamp;
        if (timestamp <= 0 || ticks < 0) throw new InvalidOperationException("Hosted-share timestamp is invalid.");
        ref var slot = ref _completedSequences[sequence % WindowSize];
        var previous = Interlocked.CompareExchange(ref slot, sequence, sequence - WindowSize);
        if (previous >= sequence)
        {
            Interlocked.Increment(ref _duplicates);
            return false;
        }
        if (previous != sequence - WindowSize)
            throw new InvalidOperationException("Hosted-share sequence skipped an unfinished slot generation.");
        Latency.RecordTicks(ticks);
        _throughput!.RecordMessage(payload.Length);
        Interlocked.Increment(ref _completed);
        return true;
    }

    public void ObserveAcknowledgements(ReadOnlySpan<ShareAcknowledgementCommitResult> results)
    {
        foreach (var result in results)
            if (result.Exception is { } failure) RecordFailure(failure);
    }

    public void RecordFailure(Exception exception) => Interlocked.CompareExchange(ref _failure, exception, null);
    public void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
        if (_throughput?.DeliveryErrorCount > 0) throw new InvalidOperationException("The hosted-share feeder reported a delivery failure.");
    }
}
