using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.StressTests.Scenarios;
using ConfluentConsumerConfig = Confluent.Kafka.ConsumerConfig;

namespace Dekaf.StressTests.FaultInjection;

internal static class FaultInjectionRunner
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromMinutes(1);

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
            var allowedSuffix = options.AllowedFailureWindows.Contains(window.Name) ? " (allowed)" : string.Empty;
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
            !window.Succeeded && !allowedFailureWindows.Contains(window.Name)) ? 1 : 0;
    }

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

        using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        windowCts.CancelAfter(OperationTimeout);
        using var liveConsumerCts = CancellationTokenSource.CreateLinkedTokenSource(windowCts.Token);
        var liveState = new LiveConsumerState();
        var liveConsumerTask = RunLiveConsumerAsync(
            environment.BootstrapServers,
            topic,
            liveState,
            liveConsumerCts.Token);

        IKafkaProducer<string, string>? producer = null;
        try
        {
            producer = await Kafka.CreateProducer<string, string>()
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

            await producer.DisposeAsync().ConfigureAwait(false);
            producer = null;

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

            await environment.WaitForTopicHealthyAsync(topic, windowCts.Token).ConfigureAwait(false);
            var liveConsumerFailure = await WaitForLiveConsumerRecoveryAsync(
                liveConsumerTask,
                liveState,
                producerOutcome.FirstPostHealMessageId,
                windowCts.Token).ConfigureAwait(false);

            liveConsumerCts.Cancel();
            liveConsumerFailure ??= await AwaitLiveConsumerShutdownAsync(liveConsumerTask).ConfigureAwait(false);
            result.LiveConsumerMessages = Volatile.Read(ref liveState.MessageCount);

            var brokerDelivered = await StressTestHelpers.QueryTotalEndOffsetAsync(
                environment.BootstrapServers,
                topic,
                options.PartitionCount).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Broker end-offset query failed after fault recovery.");
            var consumedIds = await ReadBrokerLogWithConfluentAsync(
                environment.BootstrapServers,
                topic,
                options.PartitionCount,
                windowCts.Token).ConfigureAwait(false);
            var verification = FaultWindowVerifier.Verify(
                producerOutcome.AcceptedMessages,
                brokerDelivered,
                deliveryErrors.Keys,
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
            result.Succeeded = verification.Succeeded && liveConsumerFailure is null;

            Console.WriteLine(
                $"  accepted={result.AcceptedMessages:N0} errors={result.DeliveryErrors:N0} " +
                $"broker={result.BrokerDeliveredMessages:N0} oracle={result.OracleConsumedMessages:N0} " +
                $"live-consumed={result.LiveConsumerMessages:N0}");
            Console.WriteLine(
                $"  unexplained-loss={result.UnexplainedLoss:N0} duplicates={result.Duplicates:N0} " +
                $"oracle-mismatch={result.OracleCountMismatch:N0}");

            if (!verification.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Strict fault-window verification failed: unexplained loss={verification.UnexplainedLossCount}, " +
                    $"duplicates={verification.DuplicateCount}, oracle mismatch={verification.OracleCountMismatch}, " +
                    $"missing IDs={verification.MissingIds.Count}, " +
                    $"unexpected IDs={verification.UnexpectedIds.Count}.");
            }

            if (liveConsumerFailure is not null)
            {
                throw new InvalidOperationException(
                    "Live Dekaf consumer failed instead of recovering after the fault window.",
                    liveConsumerFailure);
            }
        }
        finally
        {
            liveConsumerCts.Cancel();
            _ = await AwaitLiveConsumerShutdownAsync(liveConsumerTask).ConfigureAwait(false);
            if (producer is not null)
            {
                try { await producer.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { Console.WriteLine($"  Producer cleanup failed: {ex.Message}"); }
            }
        }
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

            readyForFault.TrySetResult();
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
                .WaitAsync(TimeSpan.FromMinutes(2), CancellationToken.None)
                .ConfigureAwait(false);
            return new ProducerOutcome(accepted, firstPostHealMessageId);
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
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + RecoveryTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (liveConsumerTask.IsCompleted)
            {
                var failure = await AwaitLiveConsumerShutdownAsync(liveConsumerTask).ConfigureAwait(false);
                return failure ?? new InvalidOperationException(
                    "Live Dekaf consumer stopped before observing a post-heal message.");
            }

            if (Volatile.Read(ref state.MaxMessageId) >= firstPostHealMessageId)
            {
                Console.WriteLine($"  Live consumer recovered through message " +
                    $"{Volatile.Read(ref state.MaxMessageId):N0}");
                return null;
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Live Dekaf consumer did not observe post-heal message {firstPostHealMessageId:N0}.");
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

    private sealed record ProducerOutcome(long AcceptedMessages, long FirstPostHealMessageId);

    private sealed class LiveConsumerState
    {
        internal long MessageCount;
        internal long MaxMessageId = -1;
    }
}
