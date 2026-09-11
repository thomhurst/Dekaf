using System.Buffers.Binary;
using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class KeyedConsumerStressTest : IStressTestScenario
{
    public string Name => "consumer-keyed";
    public string Client => "Dekaf";

    public Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        KeyedConsumerWorkload.Validate(options.KeyedShape, options.Partitions, options.KeyedRecordsPerPartition, options.MessageSizeBytes);
        return options.KeyedShape == "scalar"
            ? RunAsync<int>(options, static key => key, cancellationToken)
            : RunAsync<byte[]>(options, key => KeyedConsumerWorkload.ReadBinaryKey(options.KeyedShape, key), cancellationToken);
    }

    private async Task<StressTestResult> RunAsync<TKey>(StressTestOptions options, Func<TKey, int> keyId, CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<TKey, byte[]>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-keyed-consumer")
            .WithGroupId($"stress-keyed-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        var partitions = new TopicPartition[options.Partitions];
        for (var p = 0; p < partitions.Length; p++) partitions[p] = new(options.Topic, p);
        consumer.Partitions.Assign(partitions);
        var ends = await StressTestHelpers.QueryEndOffsetsAsync(consumer, options.Topic, options.Partitions, cancellationToken).ConfigureAwait(false);
        foreach (var end in ends)
            if (end != options.KeyedRecordsPerPartition) throw new InvalidOperationException("Seeded keyed replay partition has an unexpected end offset.");

        var processing = new PartitionedProcessingOptions
        {
            Ordering = PartitionedProcessingOrder.Key,
            MaxConcurrentHandlersPerPartition = KeyedConsumerWorkload.HandlerConcurrency,
            MaxBufferedRecordsPerPartition = KeyedConsumerWorkload.BufferedRecords,
            StopPolicy = PartitionStopPolicy.Drain,
            StopTimeout = TimeSpan.FromSeconds(30),
            ErrorPolicy = PartitionWorkerErrorPolicy.StopConsumer,
            CommitPolicy = PartitionCommitPolicy.UserManaged
        };
        long passes = 0;
        long completed = 0;
        double boundarySeconds = 0;
        var result = await StressTestHelpers.RunConsumerAsync(options, this,
            async (throughput, durationToken) =>
            {
                passes = 0;
                completed = 0;
                boundarySeconds = 0;
                while (!durationToken.IsCancellationRequested)
                {
                    // Runtime and every handler from the preceding pass have stopped.
                    // Seek never races a handler or changes offsets beneath queued work.
                    var boundary = Stopwatch.GetTimestamp();
                    consumer.Positions.SeekToBeginning(partitions);
                    consumer.Partitions.Resume(partitions);
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    stop.CancelAfter(TimeSpan.FromSeconds(60));
                    var pass = new KeyedConsumerPass(options.Partitions, options.KeyedRecordsPerPartition, throughput, stop);
                    boundarySeconds += Stopwatch.GetElapsedTime(boundary).TotalSeconds;
                    try
                    {
                        await consumer.RunPartitionedAsync((_, record, _) =>
                        {
                            var index = pass.Enter(record.Partition, keyId(record.Key!), record.Offset, record.Value);
                            if (record.Offset / KeyedConsumerWorkload.KeyCount % KeyedConsumerWorkload.YieldInterval == 0)
                                return CompleteAfterYieldAsync(pass, index, record.Value.Length);
                            pass.Complete(index, record.Value.Length);
                            return ValueTask.CompletedTask;
                        }, processing, stop.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // Only full completion is success; the 60-second deadline below
                        // fails a partial pass, including a dropped final queued record.
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    boundary = Stopwatch.GetTimestamp();
                    pass.ValidateComplete();
                    completed += pass.Completed;
                    passes++;
                    boundarySeconds += Stopwatch.GetElapsedTime(boundary).TotalSeconds;
                }
            }, StressTestOptions.HighThroughputConsumerConnectionsPerBroker,
            captureConsumerDiagnostics: () => StressTestHelpers.CaptureConsumerDiagnostics(consumer),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        result.KeyedConsumer = new KeyedConsumerSnapshot
        {
            Shape = options.KeyedShape, KeySizeBytes = KeyedConsumerWorkload.KeySize(options.KeyedShape),
            Partitions = options.Partitions, RecordsPerPartition = options.KeyedRecordsPerPartition,
            CompletedPasses = passes, CompletedRecords = completed, ReplayBookkeepingSeconds = boundarySeconds
        };
        return result;
    }

    // Deterministic asynchronous work keeps multiple key lanes active. Most handlers
    // complete synchronously; one in 64 records per key pays this state machine/yield.
    private static async ValueTask CompleteAfterYieldAsync(KeyedConsumerPass pass, int index, int valueBytes)
    {
        await Task.Yield();
        pass.Complete(index, valueBytes);
    }

    internal static Task SeedAsync(string bootstrapServers, string topic, string shape, int partitions, int recordsPerPartition, int messageSize)
    {
        KeyedConsumerWorkload.Validate(shape, partitions, recordsPerPartition, messageSize);
        return shape == "scalar"
            ? SeedAsync<int>(bootstrapServers, topic, partitions, recordsPerPartition, messageSize, static id => id)
            : SeedAsync<byte[]>(bootstrapServers, topic, partitions, recordsPerPartition, messageSize, id => KeyedConsumerWorkload.CreateBinaryKey(shape, id));
    }

    private static async Task SeedAsync<TKey>(string bootstrapServers, string topic, int partitions, int recordsPerPartition, int messageSize, Func<int, TKey> createKey)
    {
        await using var producer = await Kafka.CreateProducer<TKey, byte[]>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(bootstrapServers).WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(5)).WithBatchSize(1_048_576).BuildAsync().ConfigureAwait(false);
        var keys = new TKey[KeyedConsumerWorkload.KeyCount];
        for (var i = 0; i < keys.Length; i++) keys[i] = createKey(i);
        var value = new byte[messageSize];
        for (var offset = 0; offset < recordsPerPartition; offset++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(value, offset);
            for (var partition = 0; partition < partitions; partition++)
                await producer.FireAsync(new ProducerMessage<TKey, byte[]>
                {
                    Topic = topic, Partition = partition, Key = keys[offset % keys.Length], Value = value
                }).ConfigureAwait(false);
            if (offset % 1024 == 1023) await producer.FlushAsync().ConfigureAwait(false);
        }
        await producer.FlushAsync().ConfigureAwait(false);
        Console.WriteLine($"Keyed replay seeded: {partitions} partitions, {recordsPerPartition} records/partition.");
    }
}
