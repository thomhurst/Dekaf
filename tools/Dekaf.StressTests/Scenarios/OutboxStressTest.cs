using Dekaf.Consumer;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Dekaf.Serialization;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.StressTests.Scenarios;

/// <summary>Measures an empty relay, then committed EF writes through real Kafka delivery.</summary>
internal sealed class OutboxStressTest : IStressTestScenario
{
    internal const int WriteBatchSize = 32;
    internal const double IdleFraction = 0.25;
    public string Name => "outbox";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MessageSizeBytes, 24);
        if (options.BrokerCount != 1 || options.ConnectionsPerBroker != 1 || options.Compression != "none" || options.AdaptiveConnections)
            throw new ArgumentException("The outbox workload requires one broker, one connection and no compression.", nameof(options));
        if (!options.EnableProducerDeliveryDiagnostics)
            throw new ArgumentException("The outbox workload requires --producer-delivery-diagnostics.", nameof(options));
        var directory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, $"outbox-{Guid.NewGuid():N}.db");
        var success = false;
        try
        {
            var result = await RunDatabaseAsync(options, database, cancellationToken).ConfigureAwait(false);
            success = true;
            return result;
        }
        finally
        {
            if (success)
            {
                // Only this invocation's database files; pooled connections are disabled.
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    File.Delete(database + suffix);
            }
            else
                Console.Error.WriteLine($"Outbox failure database retained at {database}.");
        }
    }

    private async Task<StressTestResult> RunDatabaseAsync(StressTestOptions options, string database, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var state = new OutboxWorkloadState(options.MessageSizeBytes, options.Partitions, Random.Shared.NextInt64(1, long.MaxValue));
        var producerBuilder = OutboxServiceCollectionExtensions.CreateRelayProducerBuilder(builder => builder
            .WithBootstrapServers(options.BootstrapServers)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs)).WithBatchSize(options.BatchSize)
            .WithConnectionsPerBroker(1).WithoutAdaptiveConnections(), StressClientLogging.LoggerFactory);
        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(producerBuilder, options);
        var producer = await producerBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        await using var publisher = new ObservedOutboxPublisher(new DekafOutboxPublisher(producer), state);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOutboxPublisher>(publisher);
        services.AddDekafEntityFrameworkCoreOutboxStore<OutboxContext>((_, builder) => builder.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = database, Pooling = false, DefaultTimeout = 30 }.ToString()));
        services.AddSingleton<IOutboxStore>(provider => new ObservedOutboxStore<OutboxContext>(
            new EfCoreOutboxStore<OutboxContext>(provider.GetRequiredService<IDbContextFactory<OutboxContext>>()), state));
        services.AddDekafOutboxRelay(new OutboxRelayOptions());
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<OutboxContext>>();
        var store = (ObservedOutboxStore<OutboxContext>)provider.GetRequiredService<IOutboxStore>();
        var relay = provider.GetServices<IHostedService>().OfType<OutboxRelayService>().Single();
        await using (var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        }
        await relay.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Console.WriteLine($"  Outbox idle warmup: {options.ProducerWarmupSeconds}s; no writer or Kafka reader is active.");
            var idleWarmup = await OutboxIdlePhase.RunAsync(TimeSpan.FromSeconds(options.ProducerWarmupSeconds),
                store.Snapshot, state, cancellationToken).ConfigureAwait(false);
            var idleDuration = TimeSpan.FromMinutes(options.DurationMinutes * IdleFraction);
            var activeDuration = TimeSpan.FromMinutes(options.DurationMinutes * (1 - IdleFraction));
            Console.WriteLine($"  Outbox idle measurement: {idleDuration.TotalSeconds}s.");
            var idle = await OutboxIdlePhase.RunAsync(idleDuration, store.Snapshot, state, cancellationToken).ConfigureAwait(false);
            await VerifyRollbackAsync(factory, options, state, cancellationToken).ConfigureAwait(false);

            await using var consumer = new KafkaConsumer<string, byte[]>(new ConsumerOptions
            {
                BootstrapServers = options.BootstrapServers.Split(','),
                AutoOffsetReset = AutoOffsetReset.None,
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false,
                ConnectionsPerBroker = 1,
                MaxConnectionsPerBroker = 1,
                EnableAdaptiveConnections = false
            }, Serializers.String, Serializers.ByteArray);
            await consumer.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var assignments = new TopicPartitionOffset[options.Partitions];
            for (var p = 0; p < assignments.Length; p++)
                assignments[p] = new TopicPartitionOffset(options.Topic, p, 0);
            consumer.IncrementalAssign(assignments);
            using var readerLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var reader = ReadAsync(consumer, state, readerLifetime.Token);
            try
            {
                var keys = new string[OutboxRelayOptions.DefaultBucketCount];
                for (var i = 0; i < keys.Length; i++)
                    keys[i] = $"outbox-key-{i}";
                var measured = new ThroughputTracker();
                Console.WriteLine($"  Warming up Dekaf outbox: {options.ProducerWarmupSeconds}s of committed writes and complete drain.");
                measured.Warmup = await ProducerWarmup.RunAsync(options.ProducerWarmupSeconds,
                    RunCycleAsync, cancellationToken).ConfigureAwait(false);
                var committedStart = state.Committed;
                var completedStart = state.Completed;
                var duplicatesStart = state.Duplicates;
                var operationsStart = store.Snapshot();
                Console.WriteLine($"  Running Dekaf outbox stress test: {activeDuration.TotalSeconds}s active after {idleDuration.TotalSeconds}s measured idle.");
                var run = await RunCycleAsync(activeDuration, measured, cancellationToken).ConfigureAwait(false);
                state.ThrowIfFailed();
                return new StressTestResult
                {
                    Scenario = Name, Client = Client, DurationMinutes = options.DurationMinutes,
                    MessageSizeBytes = options.MessageSizeBytes, BrokerCount = options.BrokerCount,
                    StartedAtUtc = started, CompletedAtUtc = DateTime.UtcNow,
                    Throughput = run.Throughput, GcStats = run.Gc, Latency = state.Latency.GetSnapshot(),
                    CpuTimeSeconds = measured.CpuTimeSeconds, ConsumedMessages = state.Completed - completedStart,
                    Idempotent = true,
                    ProducerDeliveryDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options)
                        ?? throw new InvalidOperationException("The outbox workload requires producer delivery diagnostics."),
                    Outbox = new OutboxWorkloadSnapshot(idleWarmup, idle, activeDuration.TotalSeconds,
                        run.WorkloadSeconds,
                        state.Committed - committedStart, state.Completed - completedStart,
                        state.Duplicates - duplicatesStart, store.Snapshot().Since(operationsStart))
                };

                async Task<ProducerWorkloadResult> RunCycleAsync(TimeSpan duration, ThroughputTracker tracker, CancellationToken token)
                {
                    state.BeginCycle(tracker);
                    StressTestHelpers.ResetProducerDeliveryDiagnostics(producer);
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
                            if (!state.TryReserve(out var sequence))
                            {
                                await Task.Delay(1, token).ConfigureAwait(false);
                                continue;
                            }
                            await using var context = await factory.CreateDbContextAsync(token).ConfigureAwait(false);
                            var count = 0;
                            do
                            {
                                var key = (int)(sequence % keys.Length);
                                context.AddOutboxMessage(options.Topic, keys[key], state.CreatePayload(sequence),
                                    Serializers.String, Serializers.ByteArray, partition: key % options.Partitions);
                                count++;
                            } while (count < WriteBatchSize && state.TryReserve(out sequence));
                            await context.SaveChangesAsync(token).ConfigureAwait(false);
                            state.RecordCommitted(count);
                        }
                        workloadSeconds = tracker.Elapsed.TotalSeconds;
                        using var drain = CancellationTokenSource.CreateLinkedTokenSource(token);
                        drain.CancelAfter(StressTestHelpers.OperationTimeout);
                        while (state.Completed != state.Committed || store.Published != state.Committed
                            || await PendingCountAsync(factory, drain.Token).ConfigureAwait(false) != 0)
                        {
                            state.ThrowIfFailed();
                            await Task.Delay(1, drain.Token).ConfigureAwait(false);
                        }
                        await producer.FlushAsync(drain.Token).ConfigureAwait(false);
                        var ends = await StressTestHelpers.QueryEndOffsetsAsync(consumer, options.Topic, options.Partitions, drain.Token).ConfigureAwait(false);
                        while (!state.HasReadThrough(ends))
                        {
                            state.ThrowIfFailed();
                            await Task.Delay(1, drain.Token).ConfigureAwait(false);
                        }
                        if (ends.Sum() != state.Completed + state.Duplicates)
                            throw new InvalidOperationException("Kafka offsets do not match unique and duplicate outbox deliveries.");
                        state.ThrowIfFailed();
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
            finally
            {
                readerLifetime.Cancel();
                await reader.ConfigureAwait(false);
            }
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(StressTestHelpers.OperationTimeout);
            await relay.StopAsync(shutdown.Token).ConfigureAwait(false);
            if (relay.ExecuteTask is { } execution)
                await execution.WaitAsync(shutdown.Token).ConfigureAwait(false);
            state.ThrowIfFailed();
        }
    }

    private static async Task ReadAsync(KafkaConsumer<string, byte[]> consumer, OutboxWorkloadState state, CancellationToken token)
    {
        try
        {
            await foreach (var record in consumer.ConsumeAsync(token).ConfigureAwait(false))
                state.Process(record.Value, record.Partition, record.Offset);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { state.RecordFailure(error); }
    }

    private static async Task VerifyRollbackAsync(IDbContextFactory<OutboxContext> factory, StressTestOptions options,
        OutboxWorkloadState state, CancellationToken cancellationToken)
    {
        await using (var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            context.AddOutboxMessage(options.Topic, "rolled-back", state.CreatePayload(-1), Serializers.String, Serializers.ByteArray);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        if (await PendingCountAsync(factory, cancellationToken).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("The rolled-back outbox row remains in the database.");
    }

    private static async Task<long> PendingCountAsync(IDbContextFactory<OutboxContext> factory, CancellationToken token)
    {
        await using var context = await factory.CreateDbContextAsync(token).ConfigureAwait(false);
        return await context.Set<OutboxMessage>().LongCountAsync(token).ConfigureAwait(false);
    }

    public sealed class OutboxContext(DbContextOptions<OutboxContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }
}
