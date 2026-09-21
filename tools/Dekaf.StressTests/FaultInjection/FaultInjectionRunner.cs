using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;
using ConfluentConsumerConfig = Confluent.Kafka.ConsumerConfig;

namespace Dekaf.StressTests.FaultInjection;

internal static class FaultInjectionRunner
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProducerFlushTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromMinutes(1);

    // Small transactions keep many commit boundaries inside one fault window.
    internal const int RecordsPerTransaction = 5;
    private const int RequiredTransactionsAfterHeal = 3;

    internal static async Task<int> RunAsync(
        FaultInjectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var plan = FaultInjectionPlan.Build(options.Profile, options.BrokerCount);
        var startedAt = DateTime.UtcNow;
        var report = new FaultInjectionReport
        {
            StartedAtUtc = startedAt,
            MachineName = Environment.MachineName,
            Profile = options.Profile,
            BrokerCount = options.BrokerCount,
            PartitionCount = options.PartitionCount,
            MessageSizeBytes = options.MessageSizeBytes,
            FaultDurationSeconds = checked((int)options.FaultDuration.TotalSeconds)
        };
        var reportPath = Path.Combine(
            options.OutputPath,
            $"fault-injection-{options.Profile}-{options.BrokerCount}brokers-{startedAt:yyyyMMdd-HHmmss}.json");

        Console.WriteLine("Dekaf fault-injection stress suite");
        Console.WriteLine($"Profile: {options.Profile}; brokers: {options.BrokerCount}; windows: {plan.Count}");
        Console.WriteLine($"Fault duration: {options.FaultDuration.TotalSeconds:N0}s");
        Console.WriteLine($"Messages: {options.MessagesBeforeFault:N0} before, up to " +
            $"{options.MaxMessagesDuringFault:N0} during, {options.MessagesAfterFault:N0} after");

        await using var environment = await FaultInjectionKafkaEnvironment
            .CreateAsync(options.BrokerCount, cancellationToken)
            .ConfigureAwait(false);

        foreach (var definition in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine();
            Console.WriteLine($"=== Fault window: {definition.Name} ===");
            var result = new FaultWindowRunResult
            {
                Name = definition.Name,
                StartedAtUtc = DateTime.UtcNow
            };

            try
            {
                await RunWindowAsync(environment, definition, options, result, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Succeeded = false;
                result.Failure = ex.ToString();
                Console.WriteLine($"  FAILED: {ex}");
            }
            finally
            {
                result.CompletedAtUtc = DateTime.UtcNow;
                report.Windows.Add(result);
                report.CompletedAtUtc = result.CompletedAtUtc;
                await report.SaveAsync(reportPath, CancellationToken.None).ConfigureAwait(false);
            }
        }

        report.CompletedAtUtc = DateTime.UtcNow;
        await report.SaveAsync(reportPath, CancellationToken.None).ConfigureAwait(false);
        var failed = report.Windows.Where(window => !window.Succeeded).ToArray();
        Console.WriteLine();
        Console.WriteLine($"Fault report: {reportPath}");
        Console.WriteLine($"Windows: {report.Windows.Count - failed.Length} passed, {failed.Length} failed");
        foreach (var window in failed)
        {
            var allowedSuffix = IsAllowedFailure(window, options.AllowedFailureWindows)
                ? " (allowed)"
                : string.Empty;
            Console.WriteLine($"  - {window.Name}{allowedSuffix}: {FirstLine(window.Failure)}");
        }

        return DetermineExitCode(report.Windows, options.AllowedFailureWindows);
    }

    internal static int DetermineExitCode(
        IEnumerable<FaultWindowRunResult> windows,
        IReadOnlySet<string> allowedFailureWindows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(allowedFailureWindows);

        return windows.Any(window =>
            !window.Succeeded && !IsAllowedFailure(window, allowedFailureWindows)) ? 1 : 0;
    }

    internal static LiveConsumerFailureKind ClassifyLiveConsumerFailure(
        Exception? recoveryFailure,
        Exception? shutdownFailure,
        bool consumerExitedBeforeCancellation)
    {
        if (shutdownFailure is not null
            && (recoveryFailure is null || !consumerExitedBeforeCancellation))
        {
            return LiveConsumerFailureKind.Shutdown;
        }

        return recoveryFailure is not null
            ? LiveConsumerFailureKind.Recovery
            : LiveConsumerFailureKind.None;
    }

    internal static long GetExpectedBrokerDeliveryCount(long acceptedMessages, long deliveryErrorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedMessages);
        ArgumentOutOfRangeException.ThrowIfNegative(deliveryErrorCount);
        if (deliveryErrorCount > acceptedMessages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveryErrorCount),
                "Delivery errors cannot exceed accepted messages.");
        }

        return acceptedMessages - deliveryErrorCount;
    }

    internal static bool IsFaultWindowDeliveryError(
        long messageId,
        long firstFaultMessageId,
        long firstPostHealMessageId) =>
        messageId >= firstFaultMessageId && messageId < firstPostHealMessageId;

    private static async Task RunWindowAsync(
        FaultInjectionKafkaEnvironment environment,
        FaultWindowDefinition definition,
        FaultInjectionOptions options,
        FaultWindowRunResult result,
        CancellationToken cancellationToken)
    {
        var topic = $"fault-{definition.Name}-{Guid.NewGuid():N}";
        await environment.CreateTopicAsync(topic, options.PartitionCount, cancellationToken)
            .ConfigureAwait(false);

        var transactionTopic = $"fault-{definition.Name}-txn-{Guid.NewGuid():N}";
        await environment.CreateTopicAsync(transactionTopic, options.PartitionCount, cancellationToken)
            .ConfigureAwait(false);

        using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        windowCts.CancelAfter(OperationTimeout);
        using var liveConsumerCts = CancellationTokenSource.CreateLinkedTokenSource(windowCts.Token);
        using var joiningConsumerCts = CancellationTokenSource.CreateLinkedTokenSource(windowCts.Token);
        var liveState = new LiveConsumerState();
        var joiningState = new LiveConsumerState();
        var stopTransactions = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<TransactionalOutcome>? transactionalTask = null;
        Task? joiningConsumerTask = null;
        var liveConsumerTask = RunLiveConsumerAsync(
            environment.BootstrapServers,
            topic,
            liveState,
            liveConsumerCts.Token);

        IKafkaProducer<string, string>? producer = null;
        try
        {
            producer = await Kafka.CreateProducer<string, string>()
                .WithLoggerFactory(StressClientLogging.LoggerFactory)
                .WithBootstrapServers(environment.BootstrapServers)
                .WithClientId($"fault-producer-{definition.Name}")
                .WithIdempotence(true)
                .WithAcks(Dekaf.Producer.Acks.All)
                .WithLinger(TimeSpan.FromMilliseconds(20))
                .WithBatchSize(1024 * 1024)
                .WithBufferMemory(64UL * 1024 * 1024)
                .WithMaxBlock(TimeSpan.FromMinutes(2))
                .WithRequestTimeout(TimeSpan.FromSeconds(10))
                .WithDeliveryTimeout(TimeSpan.FromMinutes(2))
                .BuildAsync(windowCts.Token)
                .ConfigureAwait(false);

            var readyForFault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var faultActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var faultHealed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var deliveryErrors = new ConcurrentDictionary<long, string>();
            var callbackCount = 0L;

            // Transactions commit through the whole window on their own topic, so the strict
            // per-message accounting of the main topic is untouched.
            transactionalTask = RunTransactionalLoadAsync(
                environment.BootstrapServers,
                transactionTopic,
                definition.Name,
                faultHealed.Task,
                stopTransactions.Task,
                windowCts.Token);
            // Joins its group while the fault is active; the live consumer above joined before it.
            joiningConsumerTask = RunJoiningConsumerAsync(
                environment.BootstrapServers,
                topic,
                faultActive.Task,
                joiningState,
                joiningConsumerCts.Token);

            var produceTask = RunProducerLoadAsync(
                producer,
                topic,
                options,
                readyForFault,
                faultActive,
                faultHealed,
                deliveryErrors,
                () => Interlocked.Increment(ref callbackCount),
                windowCts.Token);
            var faultTask = RunFaultAsync(
                environment,
                definition,
                topic,
                options.FaultDuration,
                readyForFault,
                faultActive,
                faultHealed,
                windowCts.Token);

            ProducerOutcome producerOutcome;
            try
            {
                await Task.WhenAll(produceTask, faultTask).ConfigureAwait(false);
                producerOutcome = await produceTask.ConfigureAwait(false);
            }
            catch
            {
                windowCts.Cancel();
                throw;
            }

            var producerToDispose = producer;
            producer = null;
            await AwaitProducerDisposalAsync(
                producerToDispose.DisposeAsync(),
                StressTestHelpers.OperationTimeout).ConfigureAwait(false);

            result.AcceptedMessages = producerOutcome.AcceptedMessages;
            result.DeliveryErrors = deliveryErrors.Count;
            result.DeliveryCallbacks = callbackCount;
            result.LiveConsumerMessages = Volatile.Read(ref liveState.MessageCount);
            result.DeliveryErrorSamples = deliveryErrors
                .OrderBy(pair => pair.Key)
                .Take(20)
                .Select(pair => new DeliveryErrorSample(pair.Key, pair.Value))
                .ToArray();

            if (callbackCount != producerOutcome.AcceptedMessages)
            {
                throw new InvalidOperationException(
                    $"Only {callbackCount:N0} of {producerOutcome.AcceptedMessages:N0} delivery callbacks completed.");
            }

            var faultWindowDeliveryErrorIds = deliveryErrors.Keys
                .Where(messageId => IsFaultWindowDeliveryError(
                    messageId,
                    producerOutcome.FirstFaultMessageId,
                    producerOutcome.FirstPostHealMessageId))
                .ToArray();
            var healthyPhaseDeliveryErrorIds = deliveryErrors.Keys
                .Where(messageId => !IsFaultWindowDeliveryError(
                    messageId,
                    producerOutcome.FirstFaultMessageId,
                    producerOutcome.FirstPostHealMessageId))
                .Order()
                .ToArray();
            if (healthyPhaseDeliveryErrorIds.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{healthyPhaseDeliveryErrorIds.Length:N0} delivery errors occurred outside the active " +
                    $"fault window (message IDs: {string.Join(", ", healthyPhaseDeliveryErrorIds.Take(20))}).");
            }

            await environment.WaitForTopicHealthyAsync(topic, windowCts.Token).ConfigureAwait(false);
            var liveConsumerRecoveryFailure = await WaitForLiveConsumerRecoveryAsync(
                liveConsumerTask,
                liveState,
                producerOutcome.FirstPostHealMessageId,
                windowCts.Token).ConfigureAwait(false);

            var consumerExitedBeforeCancellation = liveConsumerTask.IsCompleted;
            liveConsumerCts.Cancel();
            var liveConsumerShutdownFailure = await AwaitLiveConsumerShutdownAsync(liveConsumerTask)
                .ConfigureAwait(false);
            var liveConsumerFailureKind = ClassifyLiveConsumerFailure(
                liveConsumerRecoveryFailure,
                liveConsumerShutdownFailure,
                consumerExitedBeforeCancellation);
            result.LiveConsumerMessages = Volatile.Read(ref liveState.MessageCount);

            var joiningConsumerFailure = await WaitForLiveConsumerRecoveryAsync(
                joiningConsumerTask,
                joiningState,
                producerOutcome.FirstPostHealMessageId,
                windowCts.Token,
                "Joining").ConfigureAwait(false);
            joiningConsumerCts.Cancel();
            joiningConsumerFailure ??= await AwaitLiveConsumerShutdownAsync(joiningConsumerTask)
                .ConfigureAwait(false);
            result.JoiningConsumerMessages = Volatile.Read(ref joiningState.MessageCount);
            result.JoiningConsumerRecoveryFailed = joiningConsumerFailure is not null;

            stopTransactions.TrySetResult();
            var transactionalOutcome = await transactionalTask.ConfigureAwait(false);
            var visibleTransactionRecords = await ReadCommittedTransactionRecordsWithConfluentAsync(
                environment.BootstrapServers,
                transactionTopic,
                options.PartitionCount,
                windowCts.Token).ConfigureAwait(false);
            var transactionVerification = TransactionWindowVerifier.Verify(
                transactionalOutcome.Outcomes,
                visibleTransactionRecords,
                RecordsPerTransaction);
            result.TransactionsCommitted = transactionVerification.CommittedCount;
            result.TransactionsAborted = transactionVerification.AbortedCount;
            result.TransactionsUnknown = transactionVerification.UnknownCount;
            result.TransactionsCommittedAfterHeal = transactionalOutcome.CommittedAfterHeal;
            result.TransactionViolations = DescribeTransactionViolations(transactionVerification);
            result.TransactionalProducerRecoveryFailed = transactionalOutcome.Failure is not null;

            var expectedBrokerDeliveryCount = GetExpectedBrokerDeliveryCount(
                producerOutcome.AcceptedMessages,
                faultWindowDeliveryErrorIds.Length);
            var brokerDrain = new ThroughputTracker();
            var brokerDelivered = await StressTestHelpers.QueryTotalEndOffsetAfterProducerDrainAsync(
                environment.BootstrapServers,
                topic,
                options.PartitionCount,
                startOffset: 0,
                acceptedMessages: expectedBrokerDeliveryCount,
                throughput: brokerDrain,
                operation: "Fault recovery drain").ConfigureAwait(false)
                ?? throw new InvalidOperationException("Broker end-offset query failed after fault recovery.");
            var consumedIds = await ReadBrokerLogWithConfluentAsync(
                environment.BootstrapServers,
                topic,
                options.PartitionCount,
                windowCts.Token).ConfigureAwait(false);
            var verification = FaultWindowVerifier.Verify(
                producerOutcome.AcceptedMessages,
                brokerDelivered,
                faultWindowDeliveryErrorIds,
                consumedIds,
                requireZeroDuplicates: true);

            result.BrokerDeliveredMessages = brokerDelivered;
            result.OracleConsumedMessages = consumedIds.Count;
            result.UnexplainedLoss = verification.UnexplainedLossCount;
            result.Duplicates = verification.DuplicateCount;
            result.OracleCountMismatch = verification.OracleCountMismatch;
            result.MissingIds = verification.MissingIds.Take(100).ToArray();
            result.DuplicateIds = verification.DuplicateIds.Take(100).ToArray();
            result.UnexpectedIds = verification.UnexpectedIds.Take(100).ToArray();
            result.Succeeded = verification.Succeeded
                && liveConsumerFailureKind == LiveConsumerFailureKind.None
                && joiningConsumerFailure is null
                && transactionVerification.Succeeded
                && transactionalOutcome.Failure is null;

            Console.WriteLine(
                $"  accepted={result.AcceptedMessages:N0} errors={result.DeliveryErrors:N0} " +
                $"broker={result.BrokerDeliveredMessages:N0} oracle={result.OracleConsumedMessages:N0} " +
                $"live-consumed={result.LiveConsumerMessages:N0}");
            Console.WriteLine(
                $"  unexplained-loss={result.UnexplainedLoss:N0} duplicates={result.Duplicates:N0} " +
                $"oracle-mismatch={result.OracleCountMismatch:N0}");
            Console.WriteLine(
                $"  transactions committed={result.TransactionsCommitted:N0} aborted={result.TransactionsAborted:N0} " +
                $"unknown={result.TransactionsUnknown:N0} committed-after-heal={result.TransactionsCommittedAfterHeal:N0} " +
                $"joining-consumed={result.JoiningConsumerMessages:N0}");

            if (brokerDrain.ErrorCount > 0)
            {
                throw new InvalidOperationException(
                    "Broker end offsets did not catch up after fault recovery.");
            }

            if (!verification.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Strict fault-window verification failed: unexplained loss={verification.UnexplainedLossCount}, " +
                    $"duplicates={verification.DuplicateCount}, oracle mismatch={verification.OracleCountMismatch}, " +
                    $"missing IDs={verification.MissingIds.Count}, " +
                    $"unexpected IDs={verification.UnexpectedIds.Count}.");
            }

            if (liveConsumerFailureKind == LiveConsumerFailureKind.Recovery)
            {
                result.LiveConsumerRecoveryFailed = true;
                throw new InvalidOperationException(
                    "Live Dekaf consumer failed instead of recovering after the fault window.",
                    liveConsumerRecoveryFailure);
            }

            if (liveConsumerFailureKind == LiveConsumerFailureKind.Shutdown)
            {
                result.LiveConsumerShutdownFailed = true;
                throw new InvalidOperationException(
                    "Live Dekaf consumer recovered but failed to stop after the fault window.",
                    liveConsumerShutdownFailure);
            }

            if (joiningConsumerFailure is not null)
            {
                throw new InvalidOperationException(
                    "Dekaf consumer that joined its group during the fault did not recover after it.",
                    joiningConsumerFailure);
            }

            if (!transactionVerification.Succeeded)
            {
                throw new InvalidOperationException(
                    "Transactional atomicity failed: " + string.Join("; ", result.TransactionViolations));
            }

            if (transactionalOutcome.Failure is not null)
            {
                throw new InvalidOperationException(
                    "Transactional Dekaf producer did not recover after the fault window.",
                    transactionalOutcome.Failure);
            }
        }
        finally
        {
            liveConsumerCts.Cancel();
            joiningConsumerCts.Cancel();
            _ = await AwaitLiveConsumerShutdownAsync(liveConsumerTask).ConfigureAwait(false);
            if (joiningConsumerTask is not null)
            {
                _ = await AwaitLiveConsumerShutdownAsync(joiningConsumerTask).ConfigureAwait(false);
            }

            if (transactionalTask is not null && !transactionalTask.IsCompleted)
            {
                // Only reached when the window failed before the transactional load was stopped.
                windowCts.Cancel();
                try { _ = await transactionalTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Console.WriteLine($"  Transactional producer cleanup failed: {ex.Message}"); }
            }

            if (producer is not null)
            {
                try
                {
                    await AwaitProducerDisposalAsync(
                        producer.DisposeAsync(),
                        StressTestHelpers.OperationTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) { Console.WriteLine($"  Producer cleanup failed: {ex.Message}"); }
            }
        }
    }

    internal static async Task AwaitProducerDisposalAsync(ValueTask disposal, TimeSpan timeout)
    {
        try
        {
            await disposal.AsTask()
                .WaitAsync(timeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Producer disposal did not complete within {timeout.TotalSeconds:N0} seconds.",
                ex);
        }
    }

    internal static async Task<long> CompletePreFaultPhaseAsync(
        ValueTask producerFlush,
        long acceptedMessages,
        TaskCompletionSource readyForFault,
        CancellationToken cancellationToken)
    {
        await producerFlush.AsTask()
            .WaitAsync(ProducerFlushTimeout, cancellationToken).ConfigureAwait(false);
        readyForFault.TrySetResult();
        return acceptedMessages;
    }

    private static async Task<ProducerOutcome> RunProducerLoadAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        FaultInjectionOptions options,
        TaskCompletionSource readyForFault,
        TaskCompletionSource faultActive,
        TaskCompletionSource faultHealed,
        ConcurrentDictionary<long, string> deliveryErrors,
        Action callbackCompleted,
        CancellationToken cancellationToken)
    {
        var accepted = 0L;
        var payload = new string('x', options.MessageSizeBytes - 21);
        try
        {
            for (var i = 0; i < options.MessagesBeforeFault; i++)
            {
                await ProduceOneAsync().ConfigureAwait(false);
            }

            Console.WriteLine($"  Flushing {accepted:N0} pre-fault messages...");
            var firstFaultMessageId = await CompletePreFaultPhaseAsync(
                producer.FlushAsync(CancellationToken.None),
                accepted,
                readyForFault,
                cancellationToken).ConfigureAwait(false);
            await faultActive.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            var duringFault = 0;
            while (!faultHealed.Task.IsCompleted && duringFault < options.MaxMessagesDuringFault)
            {
                await ProduceOneAsync().ConfigureAwait(false);
                duringFault++;
                if ((duringFault & 255) == 0)
                {
                    await Task.Yield();
                }
            }

            await faultHealed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var firstPostHealMessageId = accepted;
            for (var i = 0; i < options.MessagesAfterFault; i++)
            {
                await ProduceOneAsync().ConfigureAwait(false);
            }

            Console.WriteLine($"  Flushing {accepted:N0} accepted messages...");
            await producer.FlushAsync(CancellationToken.None)
                .AsTask()
                .WaitAsync(ProducerFlushTimeout, CancellationToken.None)
                .ConfigureAwait(false);
            return new ProducerOutcome(accepted, firstFaultMessageId, firstPostHealMessageId);
        }
        catch (Exception ex)
        {
            readyForFault.TrySetException(ex);
            throw;
        }

        async ValueTask ProduceOneAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageId = accepted;
            var key = messageId.ToString(CultureInfo.InvariantCulture);
            var value = $"{messageId:D20}|{payload}";
            await producer.FireAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = key,
                Value = value
            }, (_, exception) =>
            {
                if (exception is not null)
                {
                    deliveryErrors.TryAdd(messageId, $"{exception.GetType().Name}: {exception.Message}");
                }

                callbackCompleted();
            }).ConfigureAwait(false);
            accepted++;
        }
    }

    private static async Task RunFaultAsync(
        FaultInjectionKafkaEnvironment environment,
        FaultWindowDefinition definition,
        string topic,
        TimeSpan duration,
        TaskCompletionSource readyForFault,
        TaskCompletionSource faultActive,
        TaskCompletionSource faultHealed,
        CancellationToken cancellationToken)
    {
        try
        {
            await readyForFault.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            Action activated = () => faultActive.TrySetResult();
            switch (definition.Kind)
            {
                case FaultWindowKind.ConnectionReset:
                case FaultWindowKind.HalfOpen:
                case FaultWindowKind.SlowClose:
                case FaultWindowKind.LatencyAndBandwidth:
                    await environment.ExecuteNetworkFaultAsync(
                        definition.Kind,
                        duration,
                        activated,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.BrokerKillAndRestart:
                    await environment.KillAndRestartBrokerAsync(
                        nodeId: 1,
                        topic,
                        duration,
                        activated,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.LeaderElection:
                    await environment.ForceLeaderElectionAsync(
                        topic,
                        duration,
                        activated,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FaultWindowKind.RollingRestart:
                    await environment.RollingRestartAsync(
                        topic,
                        duration,
                        activated,
                        cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, "Unknown fault kind.");
            }
        }
        finally
        {
            faultActive.TrySetResult();
            faultHealed.TrySetResult();
        }
    }

    private static async Task RunLiveConsumerAsync(
        string bootstrapServers,
        string topic,
        LiveConsumerState state,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithLoggerFactory(StressClientLogging.LoggerFactory)
                .WithBootstrapServers(bootstrapServers)
                .WithClientId($"fault-live-consumer-{Guid.NewGuid():N}")
                .WithGroupId($"fault-live-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(Dekaf.Consumer.AutoOffsetReset.Earliest)
                .BuildAsync(cancellationToken)
                .ConfigureAwait(false);
            consumer.Subscribe(topic);

            await foreach (var record in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!long.TryParse(record.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var messageId))
                {
                    throw new InvalidDataException($"Live consumer received invalid message ID '{record.Key}'.");
                }

                Interlocked.Increment(ref state.MessageCount);
                if (messageId > Volatile.Read(ref state.MaxMessageId))
                {
                    Volatile.Write(ref state.MaxMessageId, messageId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<Exception?> WaitForLiveConsumerRecoveryAsync(
        Task liveConsumerTask,
        LiveConsumerState state,
        long firstPostHealMessageId,
        CancellationToken cancellationToken,
        string label = "Live")
    {
        var deadline = DateTime.UtcNow + RecoveryTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (liveConsumerTask.IsCompleted)
            {
                var failure = await AwaitLiveConsumerShutdownAsync(liveConsumerTask).ConfigureAwait(false);
                return failure ?? new InvalidOperationException(
                    $"{label} Dekaf consumer stopped before observing a post-heal message.");
            }

            if (Volatile.Read(ref state.MaxMessageId) >= firstPostHealMessageId)
            {
                Console.WriteLine($"  {label} consumer recovered through message " +
                    $"{Volatile.Read(ref state.MaxMessageId):N0}");
                return null;
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        return new TimeoutException(
            $"{label} Dekaf consumer did not observe post-heal message {firstPostHealMessageId:N0}.");
    }

    private static async Task<Exception?> AwaitLiveConsumerShutdownAsync(Task liveConsumerTask)
    {
        try
        {
            await liveConsumerTask.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None)
                .ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("  Warning: live consumer did not stop within 30 seconds.");
            return new TimeoutException("Live Dekaf consumer did not stop within 30 seconds.");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task RunJoiningConsumerAsync(
        string bootstrapServers,
        string topic,
        Task faultActive,
        LiveConsumerState state,
        CancellationToken cancellationToken)
    {
        try
        {
            await faultActive.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Built, subscribed and joined while the fault is active: metadata bootstrap, coordinator
        // discovery, the join heartbeat and the first offset lookups all meet the fault.
        await RunLiveConsumerAsync(bootstrapServers, topic, state, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TransactionalOutcome> RunTransactionalLoadAsync(
        string bootstrapServers,
        string topic,
        string windowName,
        Task faultHealed,
        Task stopRequested,
        CancellationToken cancellationToken)
    {
        var transactionalId = $"fault-txn-{windowName}-{Guid.NewGuid():N}";
        var outcomes = new List<TransactionOutcome>();
        var committedAfterHeal = 0;
        long? stopRequestedAt = null;
        Exception? lastFailure = null;
        Exception? recoveryFailure = null;
        IKafkaProducer<string, string>? producer = null;
        try
        {
            for (var transactionId = 0L; ; transactionId++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stopRequested.IsCompleted)
                {
                    if (committedAfterHeal >= RequiredTransactionsAfterHeal)
                    {
                        break;
                    }

                    stopRequestedAt ??= Stopwatch.GetTimestamp();
                    if (Stopwatch.GetElapsedTime(stopRequestedAt.Value) > RecoveryTimeout)
                    {
                        recoveryFailure = new TimeoutException(
                            $"Only {committedAfterHeal} of {RequiredTransactionsAfterHeal} transactions committed " +
                            $"within {RecoveryTimeout.TotalSeconds:N0}s of the end of the window.", lastFailure);
                        break;
                    }
                }

                // Sampled before the transaction starts: a transaction that began under the
                // fault does not count as recovery even if it commits after the heal.
                var startedAfterHeal = faultHealed.IsCompleted;
                ITransaction<string, string>? transaction = null;
                var outcome = TransactionOutcomeKind.Unknown;
                try
                {
                    producer ??= await CreateTransactionalProducerAsync(
                        bootstrapServers, transactionalId, windowName, cancellationToken).ConfigureAwait(false);
                    transaction = producer.BeginTransaction();
                    for (var index = 0; index < RecordsPerTransaction; index++)
                    {
                        _ = await transaction.ProduceAsync(
                            topic,
                            FormatTransactionRecordKey(transactionId, index),
                            "transactional",
                            cancellationToken).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    outcome = TransactionOutcomeKind.Committed;
                    if (startedAfterHeal)
                    {
                        committedAfterHeal++;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    if (transaction is not null
                        && await TryAbortAsync(transaction, cancellationToken).ConfigureAwait(false))
                    {
                        outcome = TransactionOutcomeKind.Aborted;
                    }
                    else
                    {
                        // The outcome is unknown and this producer may be fatal. A successor with
                        // the same transactional id fences it and settles the transaction.
                        await DisposeQuietlyAsync(producer).ConfigureAwait(false);
                        producer = null;
                        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (transaction is not null)
                    {
                        try { await transaction.DisposeAsync().ConfigureAwait(false); }
                        catch (Exception ex) { lastFailure = ex; }
                    }
                }

                if (transaction is not null)
                {
                    outcomes.Add(new TransactionOutcome(transactionId, outcome));
                }
            }
        }
        finally
        {
            await DisposeQuietlyAsync(producer).ConfigureAwait(false);
        }

        // Leave no transaction open: the read-committed oracle reads to the last stable offset,
        // and an unsettled transaction would hide every record behind it. InitTransactions on a
        // successor with the same transactional id settles whatever the last producer left.
        try
        {
            var settler = await CreateTransactionalProducerAsync(
                bootstrapServers, transactionalId, windowName, cancellationToken).ConfigureAwait(false);
            await DisposeQuietlyAsync(settler).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            recoveryFailure ??= new InvalidOperationException(
                "A successor producer could not settle the last transaction after the window.", ex);
        }

        return new TransactionalOutcome(outcomes, committedAfterHeal, recoveryFailure);
    }

    private static async Task<IKafkaProducer<string, string>> CreateTransactionalProducerAsync(
        string bootstrapServers,
        string transactionalId,
        string windowName,
        CancellationToken cancellationToken)
    {
        var producer = await Kafka.CreateProducer<string, string>()
            .WithLoggerFactory(StressClientLogging.LoggerFactory)
            .WithBootstrapServers(bootstrapServers)
            .WithClientId($"fault-txn-producer-{windowName}")
            .WithTransactionalId(transactionalId)
            .WithAcks(Dekaf.Producer.Acks.All)
            // Each record is awaited, so lingering would only stretch a transaction; short
            // transactions put more commit boundaries inside the fault.
            .WithLinger(TimeSpan.Zero)
            .WithMaxBlock(TimeSpan.FromSeconds(30))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await producer.InitTransactionsAsync(cancellationToken).ConfigureAwait(false);
            return producer;
        }
        catch
        {
            await DisposeQuietlyAsync(producer).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<bool> TryAbortAsync(
        ITransaction<string, string> transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.AbortAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task DisposeQuietlyAsync(IKafkaProducer<string, string>? producer)
    {
        if (producer is null)
        {
            return;
        }

        try
        {
            await AwaitProducerDisposalAsync(producer.DisposeAsync(), StressTestHelpers.OperationTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Transactional producer disposal failed: {ex.Message}");
        }
    }

    internal static string FormatTransactionRecordKey(long transactionId, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{transactionId}:{index}");

    internal static bool TryParseTransactionRecordKey(string? key, out VisibleTransactionRecord record)
    {
        record = default;
        if (key is null)
        {
            return false;
        }

        var separator = key.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0
            || !long.TryParse(key.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var transactionId)
            || !int.TryParse(key.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return false;
        }

        record = new VisibleTransactionRecord(transactionId, index);
        return true;
    }

    internal static IReadOnlyList<string> DescribeTransactionViolations(TransactionWindowVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        var violations = new List<string>();
        Add("committed but not fully visible", verification.CommittedButIncomplete);
        Add("aborted but visible", verification.AbortedButVisible);
        Add("unknown outcome but partly visible", verification.UnknownButPartial);
        Add("visible more than once", verification.DuplicatedTransactions);
        Add("visible but never started", verification.UnexpectedTransactions);
        return violations;

        void Add(string description, IReadOnlyList<long> transactionIds)
        {
            if (transactionIds.Count > 0)
            {
                violations.Add($"{description}: {string.Join(", ", transactionIds.Take(20))}");
            }
        }
    }

    private static Task<IReadOnlyList<VisibleTransactionRecord>> ReadCommittedTransactionRecordsWithConfluentAsync(
        string bootstrapServers,
        string topic,
        int partitionCount,
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<VisibleTransactionRecord>>(() =>
        {
            var config = new ConfluentConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = $"fault-txn-oracle-{Guid.NewGuid():N}",
                EnableAutoCommit = false,
                EnablePartitionEof = true,
                IsolationLevel = Confluent.Kafka.IsolationLevel.ReadCommitted,
                AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
                SocketTimeoutMs = 10_000
            };
            using var consumer = new Confluent.Kafka.ConsumerBuilder<string, string>(config).Build();
            var assignments = new List<Confluent.Kafka.TopicPartitionOffset>(partitionCount);
            for (var partition = 0; partition < partitionCount; partition++)
            {
                assignments.Add(new Confluent.Kafka.TopicPartitionOffset(
                    new Confluent.Kafka.TopicPartition(topic, partition),
                    Confluent.Kafka.Offset.Beginning));
            }

            consumer.Assign(assignments);

            // Commit and abort markers occupy offsets, so the record count is not known up front.
            // Every transaction is settled by now, so the end of each partition is its end.
            var records = new List<VisibleTransactionRecord>();
            var partitionsAtEnd = new HashSet<int>();
            var stopwatch = Stopwatch.StartNew();
            while (partitionsAtEnd.Count < partitionCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stopwatch.Elapsed > OperationTimeout)
                {
                    throw new TimeoutException(
                        $"Read-committed oracle reached the end of {partitionsAtEnd.Count} of {partitionCount} partitions.");
                }

                var record = consumer.Consume(TimeSpan.FromSeconds(1));
                if (record is null)
                {
                    continue;
                }

                if (record.IsPartitionEOF)
                {
                    partitionsAtEnd.Add(record.Partition.Value);
                    continue;
                }

                if (!TryParseTransactionRecordKey(record.Message.Key, out var visible))
                {
                    throw new InvalidDataException(
                        $"Read-committed oracle received invalid transaction record key '{record.Message.Key}'.");
                }

                records.Add(visible);
            }

            consumer.Close();
            return records;
        }, cancellationToken);

    private static Task<IReadOnlyList<long>> ReadBrokerLogWithConfluentAsync(
        string bootstrapServers,
        string topic,
        int partitionCount,
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<long>>(() =>
        {
            var config = new ConfluentConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = $"fault-oracle-{Guid.NewGuid():N}",
                EnableAutoCommit = false,
                EnablePartitionEof = true,
                AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
                SocketTimeoutMs = 10_000
            };
            using var consumer = new Confluent.Kafka.ConsumerBuilder<string, string>(config).Build();
            var assignments = new List<Confluent.Kafka.TopicPartitionOffset>(partitionCount);
            var expectedCount = 0L;
            for (var partition = 0; partition < partitionCount; partition++)
            {
                var topicPartition = new Confluent.Kafka.TopicPartition(topic, partition);
                var watermarks = consumer.QueryWatermarkOffsets(topicPartition, TimeSpan.FromSeconds(10));
                assignments.Add(new Confluent.Kafka.TopicPartitionOffset(topicPartition, watermarks.Low));
                expectedCount += watermarks.High.Value - watermarks.Low.Value;
            }

            consumer.Assign(assignments);
            var ids = new List<long>(checked((int)Math.Min(expectedCount, int.MaxValue)));
            var stopwatch = Stopwatch.StartNew();
            while (ids.Count < expectedCount && stopwatch.Elapsed < OperationTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = consumer.Consume(TimeSpan.FromSeconds(1));
                if (record is null || record.IsPartitionEOF)
                {
                    continue;
                }

                if (!long.TryParse(record.Message.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var messageId))
                {
                    throw new InvalidDataException(
                        $"Confluent oracle received invalid message ID '{record.Message.Key}'.");
                }

                ids.Add(messageId);
            }

            if (ids.Count != expectedCount)
            {
                throw new TimeoutException(
                    $"Confluent oracle read {ids.Count:N0} of {expectedCount:N0} broker records.");
            }

            consumer.Close();
            return ids;
        }, cancellationToken);

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "strict verification failed";
        }

        var newline = text.IndexOfAny(['\r', '\n']);
        return newline < 0 ? text : text[..newline];
    }

    private static bool IsAllowedFailure(
        FaultWindowRunResult window,
        IReadOnlySet<string> allowedFailureWindows) =>
        // The allowance covers the live consumer's recovery alone. Every flag is recorded before
        // the first failure is thrown, so another failure in the same window is never hidden by it.
        window.LiveConsumerRecoveryFailed
        && !window.JoiningConsumerRecoveryFailed
        && !window.TransactionalProducerRecoveryFailed
        && window.TransactionViolations.Count == 0
        && allowedFailureWindows.Contains(window.Name);

    private sealed record ProducerOutcome(
        long AcceptedMessages,
        long FirstFaultMessageId,
        long FirstPostHealMessageId);

    private sealed record TransactionalOutcome(
        IReadOnlyCollection<TransactionOutcome> Outcomes,
        int CommittedAfterHeal,
        Exception? Failure);

    private sealed class LiveConsumerState
    {
        internal long MessageCount;
        internal long MaxMessageId = -1;
    }
}
