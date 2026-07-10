using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ProducerRoundTripStressTest : IStressTestScenario
{
    public string Name => "producer-roundtrip";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        RoundTripScenarioHelpers.ValidateOptions(options);

        var throughput = new ThroughputTracker();
        var factory = new RoundTripMessageFactory(Guid.NewGuid().ToString("N"), options.Partitions, options.MessageSizeBytes);
        var startedAt = DateTime.UtcNow;

        await using var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-roundtrip-consumer-dekaf")
            .WithGroupId($"stress-roundtrip-dekaf-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);

        var startOffsets = await StressTestHelpers.QueryEndOffsetsAsync(
            consumer,
            options.Topic,
            options.Partitions,
            cancellationToken);

        var builder = Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-roundtrip-producer-dekaf")
            // Deliberately exercise retry duplicate detection; idempotent producers
            // would suppress the failure class this scenario is meant to expose.
            .WithIdempotence(false)
            .WithAcks(Acks.All)
            .WithPartitioner(PartitionerType.Murmur2)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes)
            .WithConnectionsPerBroker(options.ConnectionsPerBroker);

        _ = options.Compression switch
        {
            "lz4" => builder.UseLz4Compression(),
            "snappy" => builder.UseSnappyCompression(),
            "zstd" => builder.UseZstdCompression(),
            _ => builder
        };
        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(builder, options);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var deliveryErrorListener = CreateDeliveryErrorListener(throughput);
        throughput.Start();

        ProducerDeliveryDiagnosticsSnapshot? producerDiagnostics;
        await using (var producer = await builder.BuildAsync(cancellationToken))
        {
            using var watchdog = options.ProgressWatchdog.Track(
                throughput,
                Client,
                Name,
                () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options));
            using var produceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            produceTimeout.CancelAfter(RoundTripScenarioHelpers.GetTimeout(options));

            Console.WriteLine($"  Producing {options.RoundTripMessages:N0} sequenced messages with Dekaf...");
            for (var ordinal = 0; ordinal < options.RoundTripMessages; ordinal++)
            {
                if (RoundTripScenarioHelpers.TryRecordProduceTimeout(
                        produceTimeout.IsCancellationRequested,
                        throughput,
                        client: "Dekaf",
                        ordinal: ordinal,
                        cancellationToken: cancellationToken))
                {
                    break;
                }

                var message = factory.Create(ordinal % options.Partitions);
                try
                {
                    await producer.FireAsync(options.Topic, message.Key, message.Value).ConfigureAwait(false);
                    throughput.RecordMessage(message.Value.Length);
                }
                catch (Exception ex)
                {
                    throughput.RecordError(ex, "Round-trip produce", ordinal);
                }

                if ((ordinal + 1) % 50_000 == 0)
                {
                    Console.WriteLine($"  Produced {ordinal + 1:N0} / {options.RoundTripMessages:N0}");
                }
            }

            await StressTestHelpers.FlushWithTimeoutAsync(producer, throughput).ConfigureAwait(false);
            producerDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);
        }

        var endOffsets = await StressTestHelpers.QueryEndOffsetsAsync(
            consumer,
            options.Topic,
            options.Partitions,
            cancellationToken);
        var delivered = RoundTripScenarioHelpers.CountRecords(startOffsets, endOffsets);
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);
        var timedOut = await ConsumeAndValidateAsync(
            consumer,
            options,
            startOffsets,
            endOffsets,
            validator,
            throughput,
            cancellationToken).ConfigureAwait(false);

        throughput.Stop();
        gcStats.Capture();
        var validation = validator.CreateSnapshot(timedOut);
        RoundTripScenarioHelpers.LogValidation(validation);

        return RoundTripScenarioHelpers.CreateResult(
            Name,
            Client,
            options,
            startedAt,
            throughput,
            gcStats,
            delivered,
            validation,
            producerDiagnostics);
    }

    internal static DekafDeliveryErrorListener CreateDeliveryErrorListener(ThroughputTracker throughput) =>
        new(throughput);

    internal static async Task<bool> ConsumeAndValidateAsync(
        IKafkaConsumer<string, byte[]> consumer,
        StressTestOptions options,
        long[] startOffsets,
        long[] endOffsets,
        RoundTripValidator validator,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var completion = new RoundTripCompletionTracker(startOffsets, endOffsets);
        if (completion.IsComplete)
        {
            return false;
        }

        consumer.IncrementalAssign(Enumerable.Range(0, options.Partitions)
            .Select(partition => new TopicPartitionOffset(options.Topic, partition, startOffsets[partition])));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RoundTripScenarioHelpers.GetTimeout(options));

        try
        {
            await foreach (var record in consumer.ConsumeAsync(timeout.Token).ConfigureAwait(false))
            {
                var partition = record.Partition;
                if (!completion.IsTrackedPartition(partition))
                {
                    validator.Record(record.Key, record.Value, partition, record.Offset);
                    continue;
                }

                if (completion.Record(partition, record.Offset))
                {
                    validator.Record(record.Key, record.Value, partition, record.Offset);
                }
                else
                {
                    validator.RecordUnexpected();
                }

                if (completion.IsComplete)
                {
                    return false;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throughput.RecordError(ex, "Round-trip consume");
            return false;
        }

        return !completion.IsComplete;
    }
}

internal sealed class ConfluentProducerRoundTripStressTest : IStressTestScenario
{
    public string Name => "producer-roundtrip";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        RoundTripScenarioHelpers.ValidateOptions(options);

        var throughput = new ThroughputTracker();
        var factory = new RoundTripMessageFactory(Guid.NewGuid().ToString("N"), options.Partitions, options.MessageSizeBytes);
        var startedAt = DateTime.UtcNow;

        var consumerConfig = new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-roundtrip-consumer-confluent",
            GroupId = $"stress-roundtrip-confluent-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConfluentKafka.ConsumerBuilder<string, byte[]>(consumerConfig).Build();
        var startOffsets = ConfluentStressTestHelpers.QueryEndOffsets(
            consumer,
            options.Topic,
            options.Partitions,
            TimeSpan.FromSeconds(30));

        var producerConfig = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-roundtrip-producer-confluent",
            // Match Dekaf's non-idempotent retry semantics so duplicate detection is exercised.
            EnableIdempotence = false,
            Acks = ConfluentKafka.Acks.All,
            Partitioner = ConfluentKafka.Partitioner.Murmur2,
            LingerMs = options.LingerMs,
            BatchSize = options.BatchSize,
            QueueBufferingMaxKbytes = ConfluentStressTestHelpers.QueueBufferingMaxKbytes,
            QueueBufferingMaxMessages = ConfluentStressTestHelpers.QueueBufferingMaxMessages,
            CompressionType = options.Compression switch
            {
                "lz4" => ConfluentKafka.CompressionType.Lz4,
                "snappy" => ConfluentKafka.CompressionType.Snappy,
                "zstd" => ConfluentKafka.CompressionType.Zstd,
                _ => ConfluentKafka.CompressionType.None
            }
        };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        throughput.Start();

        using (var producer = new ConfluentKafka.ProducerBuilder<string, byte[]>(producerConfig).Build())
        {
            using var watchdog = options.ProgressWatchdog.Track(throughput, Client, Name);
            using var produceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            produceTimeout.CancelAfter(RoundTripScenarioHelpers.GetTimeout(options));

            Action<ConfluentKafka.DeliveryReport<string, byte[]>> deliveryHandler = report =>
                RecordDeliveryReportError(throughput, report.Error);

            Console.WriteLine($"  Producing {options.RoundTripMessages:N0} sequenced messages with Confluent.Kafka...");
            for (var ordinal = 0; ordinal < options.RoundTripMessages; ordinal++)
            {
                if (RoundTripScenarioHelpers.TryRecordProduceTimeout(
                        produceTimeout.IsCancellationRequested,
                        throughput,
                        client: "Confluent",
                        ordinal: ordinal,
                        cancellationToken: cancellationToken))
                {
                    break;
                }

                var message = factory.Create(ordinal % options.Partitions);
                try
                {
                    ConfluentStressTestHelpers.ProduceWithBackpressure(
                        producer,
                        options.Topic,
                        new ConfluentKafka.Message<string, byte[]>
                        {
                            Key = message.Key,
                            Value = message.Value
                        },
                        deliveryHandler,
                        produceTimeout.Token);
                    throughput.RecordMessage(message.Value.Length);
                }
                catch (OperationCanceledException) when (produceTimeout.IsCancellationRequested)
                {
                    _ = RoundTripScenarioHelpers.TryRecordProduceTimeout(
                        produceTimeout.IsCancellationRequested,
                        throughput,
                        client: "Confluent",
                        ordinal: ordinal,
                        cancellationToken: cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    throughput.RecordError(ex, "Round-trip produce", ordinal);
                }

                if ((ordinal + 1) % 50_000 == 0)
                {
                    Console.WriteLine($"  Produced {ordinal + 1:N0} / {options.RoundTripMessages:N0}");
                    await Task.Yield();
                }
            }

            ConfluentStressTestHelpers.FlushWithTimeout(producer, throughput);
        }

        var endOffsets = ConfluentStressTestHelpers.QueryEndOffsets(
            consumer,
            options.Topic,
            options.Partitions,
            TimeSpan.FromSeconds(30));
        var delivered = RoundTripScenarioHelpers.CountRecords(startOffsets, endOffsets);
        var validator = new RoundTripValidator(factory.ExpectedPerPartition);
        var timedOut = ConsumeAndValidate(
            consumer,
            options,
            startOffsets,
            endOffsets,
            validator,
            throughput,
            cancellationToken);

        throughput.Stop();
        gcStats.Capture();
        var validation = validator.CreateSnapshot(timedOut);
        RoundTripScenarioHelpers.LogValidation(validation);

        return RoundTripScenarioHelpers.CreateResult(
            Name,
            Client,
            options,
            startedAt,
            throughput,
            gcStats,
            delivered,
            validation,
            producerDeliveryDiagnostics: null);
    }

    internal static void RecordDeliveryReportError(ThroughputTracker throughput, ConfluentKafka.Error error)
    {
        if (!error.IsError)
        {
            return;
        }

        throughput.RecordDeliveryError(
            "Confluent.Kafka.Error",
            error.ToString(),
            "Round-trip delivery report");
    }

    private static bool ConsumeAndValidate(
        ConfluentKafka.IConsumer<string, byte[]> consumer,
        StressTestOptions options,
        long[] startOffsets,
        long[] endOffsets,
        RoundTripValidator validator,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var completion = new RoundTripCompletionTracker(startOffsets, endOffsets);
        if (completion.IsComplete)
        {
            return false;
        }

        consumer.Assign(Enumerable.Range(0, options.Partitions)
            .Select(partition => new ConfluentKafka.TopicPartitionOffset(
                options.Topic,
                partition,
                startOffsets[partition])));

        var deadline = DateTime.UtcNow + RoundTripScenarioHelpers.GetTimeout(options);
        while (!completion.IsComplete && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var record = consumer.Consume(TimeSpan.FromMilliseconds(100));
                if (record is null)
                {
                    continue;
                }

                var partition = record.Partition.Value;
                var offset = record.Offset.Value;
                if (!completion.IsTrackedPartition(partition))
                {
                    validator.Record(record.Message.Key, record.Message.Value, partition, offset);
                    continue;
                }

                if (completion.Record(partition, offset))
                {
                    validator.Record(record.Message.Key, record.Message.Value, partition, offset);
                }
                else
                {
                    validator.RecordUnexpected();
                }
            }
            catch (ConfluentKafka.ConsumeException ex)
            {
                throughput.RecordError(ex, "Round-trip consume");
            }
        }

        return !completion.IsComplete;
    }
}

internal static class RoundTripScenarioHelpers
{
    public static void ValidateOptions(StressTestOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RoundTripMessages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MessageSizeBytes, RoundTripMessageCodec.MinimumPayloadSize);
    }

    public static TimeSpan GetTimeout(StressTestOptions options) =>
        TimeSpan.FromMinutes(Math.Max(5, options.DurationMinutes));

    public static bool TryRecordProduceTimeout(
        bool timeoutExpired,
        ThroughputTracker throughput,
        string client,
        long ordinal,
        CancellationToken cancellationToken)
    {
        if (!timeoutExpired)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        throughput.RecordError(
            new TimeoutException($"{client} round-trip produce phase exceeded its timeout."),
            "Round-trip produce",
            ordinal);
        return true;
    }

    public static long CountRecords(long[] startOffsets, long[] endOffsets)
    {
        if (startOffsets.Length != endOffsets.Length)
        {
            throw new ArgumentException("Start and end offset arrays must have the same length.");
        }

        var count = 0L;
        for (var partition = 0; partition < startOffsets.Length; partition++)
        {
            if (startOffsets[partition] < 0 || endOffsets[partition] < startOffsets[partition])
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endOffsets),
                    "Every offset range must be non-negative and end at or after its start.");
            }

            count += endOffsets[partition] - startOffsets[partition];
        }

        return count;
    }

    public static void LogValidation(RoundTripValidationSnapshot validation)
    {
        Console.WriteLine(
            $"  Round-trip validation: expected={validation.ExpectedMessages:N0}, " +
            $"consumed={validation.ConsumedMessages:N0}, missing={validation.MissingMessages:N0}, " +
            $"duplicates={validation.DuplicateMessages:N0}, corrupt={validation.CorruptMessages:N0}, " +
            $"out-of-order={validation.OutOfOrderMessages:N0}, " +
            $"mispartitioned={validation.MispartitionedMessages:N0}, " +
            $"unexpected={validation.UnexpectedMessages:N0}, timed-out={validation.TimedOut}");
    }

    public static StressTestResult CreateResult(
        string scenario,
        string client,
        StressTestOptions options,
        DateTime startedAt,
        ThroughputTracker throughput,
        GcStats gcStats,
        long delivered,
        RoundTripValidationSnapshot validation,
        ProducerDeliveryDiagnosticsSnapshot? producerDeliveryDiagnostics) =>
        new()
        {
            Scenario = scenario,
            Client = client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            Throughput = throughput.GetSnapshot(),
            DeliveredMessages = delivered,
            Latency = null,
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            IsMessageBounded = true,
            RoundTripValidation = validation,
            ProducerDeliveryDiagnostics = producerDeliveryDiagnostics
        };
}
