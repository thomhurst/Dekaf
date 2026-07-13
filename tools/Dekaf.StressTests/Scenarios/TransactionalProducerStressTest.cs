using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class TransactionalProducerStressTest : IStressTestScenario
{
    internal const int RecordsPerTransaction = 100;
    internal const int AbortEveryTransactions = 4;

    public string Name => "producer-transactional";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var runId = $"tx-{Guid.NewGuid():N}";
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;

        var builder = Kafka.CreateProducer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-producer-transactional-dekaf")
            .WithTransactionalId($"stress-{runId}")
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(options.LingerMs))
            .WithBatchSize(options.BatchSize)
            .WithBufferMemory(StressTestHelpers.ProducerBufferMemoryBytes);

        _ = options.Compression switch
        {
            "lz4" => builder.UseLz4Compression(),
            "snappy" => builder.UseSnappyCompression(),
            "zstd" => builder.UseZstdCompression(),
            _ => builder
        };

        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(builder, options);
        var producer = await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
        await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);
        await WarmUpAsync(producer, options.Topic, cancellationToken).ConfigureAwait(false);
        StressTestHelpers.ResetProducerDeliveryDiagnostics(producer);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var gcStats = new GcStats();
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine($"  Running Dekaf transactional EOS stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine($"  Pattern: {AbortEveryTransactions - 1} committed transaction(s), then 1 aborted; " +
            $"{RecordsPerTransaction:N0} records each");
        Console.WriteLine($"  Run id: {runId}");
        StressTestHelpers.LogResourceUsage("Initial");

        throughput.Start();
        var progress = new PeriodicProgressReporter(throughput);
        var samplerTask = StressTestHelpers.RunSamplerAsync(throughput, runCts.Token);
        var resourceMonitorTask = StressTestHelpers.RunResourceMonitorAsync(runCts.Token);
        var committedMessages = 0L;
        var abortedMessages = 0L;
        var failedCommitMessages = 0L;
        var transactionIndex = 0L;

        while (!runCts.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shouldCommit = transactionIndex % AbortEveryTransactions != AbortEveryTransactions - 1;
            var transactionAccepted = 0L;
            await using var transaction = producer.BeginTransaction();

            try
            {
                for (var recordIndex = 0; recordIndex < RecordsPerTransaction; recordIndex++)
                {
                    var ordinal = shouldCommit
                        ? committedMessages + transactionAccepted
                        : abortedMessages + transactionAccepted;
                    var key = shouldCommit
                        ? TransactionalSequenceOracle.CommittedKey(runId, ordinal)
                        : TransactionalSequenceOracle.AbortedKey(runId, ordinal);

                    await transaction.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = options.Topic,
                        Key = key,
                        Value = messageValue
                    }, cancellationToken).ConfigureAwait(false);

                    transactionAccepted++;
                    throughput.RecordMessage(options.MessageSizeBytes);
                }

                if (shouldCommit)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    committedMessages += transactionAccepted;
                }
                else
                {
                    await transaction.AbortAsync(cancellationToken).ConfigureAwait(false);
                    abortedMessages += transactionAccepted;
                }

                transactionIndex++;
                progress.RecordMessage();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throughput.RecordError(exception, "Transactional produce loop", throughput.MessageCount);
                try
                {
                    await transaction.AbortAsync(CancellationToken.None).ConfigureAwait(false);
                    if (shouldCommit)
                        failedCommitMessages += transactionAccepted;
                    else
                        abortedMessages += transactionAccepted;
                }
                catch (Exception abortException)
                {
                    throughput.RecordError(abortException, "Abort failed transaction", throughput.MessageCount);
                }

                break;
            }
        }

        runCts.Cancel();
        throughput.Stop();
        gcStats.Capture();
        try { await samplerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        try { await resourceMonitorTask.ConfigureAwait(false); } catch (OperationCanceledException) { }

        Console.WriteLine(
            $"  Workload complete: accepted={throughput.MessageCount:N0}, committed={committedMessages:N0}, " +
            $"aborted={abortedMessages + failedCommitMessages:N0}, " +
            $"failedCommitAborts={failedCommitMessages:N0}, transactions={transactionIndex:N0}");
        StressTestHelpers.LogResourceUsage("Final");

        var sentinelsCommitted = await CommitPartitionSentinelsAsync(
            producer,
            options,
            runId,
            throughput,
            cancellationToken).ConfigureAwait(false);
        var producerDiagnostics = StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options);
        await DisposeProducerAsync(producer, throughput).ConfigureAwait(false);

        var verification = await VerifyReadCommittedAsync(
            options,
            runId,
            throughput.MessageCount,
            committedMessages,
            abortedMessages,
            failedCommitMessages,
            sentinelsCommitted,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine(
            $"  Transaction verification: accepted={verification.AcceptedMessages:N0}, " +
            $"committed={verification.CommittedMessages:N0}, aborted={verification.AbortedMessages:N0}, " +
            $"delivered={verification.DeliveredMessages:N0}, duplicates={verification.DuplicateMessages:N0}, " +
            $"shortfall={verification.ShortfallMessages:N0}, leakedAborted={verification.LeakedAbortedMessages:N0}, " +
            $"unexpected={verification.UnexpectedMessages:N0}, " +
            $"missingSentinels={verification.MissingSentinelPartitions:N0}, " +
            $"sentinelCommitFailed={verification.SentinelCommitFailed}");

        return new StressTestResult
        {
            Scenario = Name,
            Client = Client,
            DurationMinutes = options.DurationMinutes,
            BrokerCount = options.BrokerCount,
            MessageSizeBytes = options.MessageSizeBytes,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            Throughput = throughput.GetSnapshot(),
            DeliveredMessages = verification.DeliveredMessages,
            GcStats = gcStats.ToSnapshot(),
            CpuTimeSeconds = throughput.CpuTimeSeconds,
            ProducerDeliveryDiagnostics = producerDiagnostics,
            TransactionVerification = verification
        };
    }

    private static async Task WarmUpAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("  Warming up Dekaf transactional producer...");
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "transactional-warmup",
            Value = "warmup"
        }, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> CommitPartitionSentinelsAsync(
        IKafkaProducer<string, string> producer,
        StressTestOptions options,
        string runId,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = producer.BeginTransaction();
            for (var partition = 0; partition < options.Partitions; partition++)
            {
                await transaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = options.Topic,
                    Key = TransactionalSequenceOracle.SentinelKey(runId, partition),
                    Value = "sentinel",
                    Partition = partition
                }, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throughput.RecordError(exception, "Commit partition sentinels", throughput.MessageCount);
            return false;
        }
    }

    private static async Task DisposeProducerAsync(
        IKafkaProducer<string, string> producer,
        ThroughputTracker throughput)
    {
        try
        {
            await producer.DisposeAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throughput.RecordError(exception, "Dispose transactional producer", throughput.MessageCount);
        }
    }

    private static async Task<TransactionVerificationSnapshot> VerifyReadCommittedAsync(
        StressTestOptions options,
        string runId,
        long acceptedMessages,
        long committedMessages,
        long abortedMessages,
        long failedCommitMessages,
        bool sentinelsCommitted,
        CancellationToken cancellationToken)
    {
        var oracle = new TransactionalSequenceOracle(
            runId,
            committedMessages,
            abortedMessages,
            options.Partitions,
            failedCommitMessages);
        if (!sentinelsCommitted)
            return oracle.CreateSnapshot(acceptedMessages, sentinelCommitFailed: true);

        var verificationProgress = new ThroughputTracker();
        verificationProgress.Start();
        using var verificationWatchdog = options.ProgressWatchdog.Track(
            verificationProgress,
            client: "Dekaf",
            scenario: "producer-transactional-read-committed-verification");

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(options.BootstrapServers)
            .WithClientId("stress-transaction-verifier")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync(cancellationToken).ConfigureAwait(false);

        var partitions = Enumerable.Range(0, options.Partitions)
            .Select(partition => new TopicPartition(options.Topic, partition))
            .ToArray();
        consumer.Assign(partitions);

        var verificationMinutes = Math.Clamp(options.DurationMinutes, 2, 30);
        using var verificationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        verificationCts.CancelAfter(TimeSpan.FromMinutes(verificationMinutes));

        try
        {
            await foreach (var record in consumer.ConsumeAsync(verificationCts.Token).ConfigureAwait(false))
            {
                verificationProgress.RecordMessage(0);
                oracle.Observe(record.Key);
                if (oracle.AllSentinelsSeen)
                    break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine(
                $"  Warning: read_committed verification timed out after {verificationMinutes:N0} minute(s).");
        }
        finally
        {
            verificationProgress.Stop();
        }

        return oracle.CreateSnapshot(acceptedMessages);
    }
}
