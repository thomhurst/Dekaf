using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConfluentTransactionalProducerStressTest : IStressTestScenario
{
    public string Name => "producer-transactional";
    public string Client => "Confluent";

    public async Task<StressTestResult> RunAsync(
        StressTestOptions options,
        CancellationToken cancellationToken)
    {
        var messageValue = new string('x', options.MessageSizeBytes);
        var runId = $"tx-{Guid.NewGuid():N}";
        var throughput = new ThroughputTracker();
        var startedAt = DateTime.UtcNow;
        var config = new ConfluentKafka.ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-producer-transactional-confluent",
            TransactionalId = $"stress-{runId}",
            EnableIdempotence = true,
            Acks = ConfluentKafka.Acks.All,
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

        using var producer = new ConfluentKafka.ProducerBuilder<string, string>(config).Build();
        producer.InitTransactions(StressTestHelpers.OperationTimeout);
        await WarmUpAsync(producer, options.Topic, cancellationToken).ConfigureAwait(false);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gcStats = new GcStats();
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(TimeSpan.FromMinutes(options.DurationMinutes));

        Console.WriteLine(
            $"  Running Confluent transactional EOS stress test for {options.DurationMinutes} minutes...");
        Console.WriteLine(
            $"  Pattern: {TransactionalProducerStressTest.AbortEveryTransactions - 1} committed transaction(s), " +
            $"then 1 aborted; {TransactionalProducerStressTest.RecordsPerTransaction:N0} records each");
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
            var shouldCommit = transactionIndex % TransactionalProducerStressTest.AbortEveryTransactions !=
                               TransactionalProducerStressTest.AbortEveryTransactions - 1;
            var transactionAccepted = 0L;
            producer.BeginTransaction();

            try
            {
                for (var recordIndex = 0;
                     recordIndex < TransactionalProducerStressTest.RecordsPerTransaction;
                     recordIndex++)
                {
                    var ordinal = shouldCommit
                        ? committedMessages + transactionAccepted
                        : abortedMessages + transactionAccepted;
                    var key = shouldCommit
                        ? TransactionalSequenceOracle.CommittedKey(runId, ordinal)
                        : TransactionalSequenceOracle.AbortedKey(runId, ordinal);

                    await producer.ProduceAsync(options.Topic, new ConfluentKafka.Message<string, string>
                    {
                        Key = key,
                        Value = messageValue
                    }, cancellationToken).ConfigureAwait(false);

                    transactionAccepted++;
                    throughput.RecordMessage(options.MessageSizeBytes);
                }

                if (shouldCommit)
                {
                    producer.CommitTransaction(StressTestHelpers.OperationTimeout);
                    committedMessages += transactionAccepted;
                }
                else
                {
                    producer.AbortTransaction(StressTestHelpers.OperationTimeout);
                    abortedMessages += transactionAccepted;
                }

                transactionIndex++;
                progress.RecordMessage();
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throughput.RecordError(exception, "Confluent transactional produce loop", throughput.MessageCount);
                try
                {
                    producer.AbortTransaction(StressTestHelpers.OperationTimeout);
                    if (shouldCommit)
                        failedCommitMessages += transactionAccepted;
                    else
                        abortedMessages += transactionAccepted;
                }
                catch (Exception abortException)
                {
                    throughput.RecordError(
                        abortException,
                        "Abort failed Confluent transaction",
                        throughput.MessageCount);
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
        var verification = VerifyReadCommitted(
            options,
            runId,
            throughput.MessageCount,
            committedMessages,
            abortedMessages,
            failedCommitMessages,
            sentinelsCommitted,
            cancellationToken);

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
            TransactionVerification = verification
        };
    }

    private static async Task WarmUpAsync(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("  Warming up Confluent transactional producer...");
        producer.BeginTransaction();
        await producer.ProduceAsync(topic, new ConfluentKafka.Message<string, string>
        {
            Key = "transactional-warmup",
            Value = "warmup"
        }, cancellationToken).ConfigureAwait(false);
        producer.CommitTransaction(StressTestHelpers.OperationTimeout);
    }

    private static async Task<bool> CommitPartitionSentinelsAsync(
        ConfluentKafka.IProducer<string, string> producer,
        StressTestOptions options,
        string runId,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        try
        {
            producer.BeginTransaction();
            for (var partition = 0; partition < options.Partitions; partition++)
            {
                await producer.ProduceAsync(
                    new ConfluentKafka.TopicPartition(options.Topic, partition),
                    new ConfluentKafka.Message<string, string>
                    {
                        Key = TransactionalSequenceOracle.SentinelKey(runId, partition),
                        Value = "sentinel"
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            producer.CommitTransaction(StressTestHelpers.OperationTimeout);
            return true;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throughput.RecordError(exception, "Commit Confluent partition sentinels", throughput.MessageCount);
            return false;
        }
    }

    private static TransactionVerificationSnapshot VerifyReadCommitted(
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
            client: "Confluent",
            scenario: "producer-transactional-read-committed-verification");
        var config = new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "stress-transaction-verifier-confluent",
            GroupId = $"stress-transaction-verifier-{Guid.NewGuid():N}",
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            IsolationLevel = ConfluentKafka.IsolationLevel.ReadCommitted
        };
        using var consumer = new ConfluentKafka.ConsumerBuilder<string, string>(config).Build();
        consumer.Assign(Enumerable.Range(0, options.Partitions).Select(partition =>
            new ConfluentKafka.TopicPartitionOffset(
                options.Topic,
                partition,
                ConfluentKafka.Offset.Beginning)));

        var verificationMinutes = Math.Clamp(options.DurationMinutes, 2, 30);
        using var verificationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        verificationCts.CancelAfter(TimeSpan.FromMinutes(verificationMinutes));

        try
        {
            while (!oracle.AllSentinelsSeen)
            {
                var record = consumer.Consume(verificationCts.Token);
                verificationProgress.RecordMessage(0);
                oracle.Observe(record.Message.Key);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine(
                $"  Warning: Confluent read_committed verification timed out after " +
                $"{verificationMinutes:N0} minute(s).");
        }
        finally
        {
            verificationProgress.Stop();
        }

        return oracle.CreateSnapshot(acceptedMessages);
    }
}
