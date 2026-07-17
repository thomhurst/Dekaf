using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks.Sources;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Behavioral tests for BrokerSender's send loop, verifying:
/// - Response completion wakes the send loop (via event channel, not Task.WhenAny)
/// - In-flight request limiting via _pendingResponses.Count
/// - Fire-and-forget (Acks.None) path doesn't track in-flight
/// - Faulted responses wake the send loop
///
/// These tests use controllable mock connections to verify send loop timing behavior.
/// Synchronization uses deterministic TCS signals from mock callbacks, not Task.Delay.
/// </summary>
public sealed class BrokerSenderSendLoopTests
{
    [Test]
    public async Task DeliveryLatencyOrigin_SealWithinLinger_UsesSealNotCreation()
    {
        // Sealing within the configured linger: the origin stays the seal, so opted-in
        // batching delay never reads as queueing to the window governor.
        var readyBatch = CreateReadyBatchSealedAfter(
            creationAgeTicks: Stopwatch.Frequency / 10,
            lingerMs: 200);

        try
        {
            var originTimestamp = BrokerSender.GetOldestBatchOriginTimestamp([readyBatch], 1);

            await Assert.That(readyBatch.GovernedOriginTicks)
                .IsEqualTo(readyBatch.StopwatchSealedTicks);
            await Assert.That(originTimestamp).IsEqualTo(readyBatch.StopwatchSealedTicks);
        }
        finally
        {
            readyBatch.TrySetMemoryReleased();
            readyBatch.Fail(new InvalidOperationException("test cleanup"));
        }
    }

    [Test]
    public async Task DeliveryLatencyOrigin_SealStalledPastLinger_UsesLingerDeadline()
    {
        // Sealing stalled past the linger deadline (admission-flush stall, drain starvation):
        // the overrun is delay the caller pays, so the origin becomes the linger deadline
        // rather than the late seal that would hide it from the window governor.
        const int lingerMs = 10;
        var readyBatch = CreateReadyBatchSealedAfter(
            creationAgeTicks: Stopwatch.Frequency / 10,
            lingerMs);
        var lingerTicks = lingerMs * Stopwatch.Frequency / 1_000;

        try
        {
            var originTimestamp = BrokerSender.GetOldestBatchOriginTimestamp([readyBatch], 1);

            await Assert.That(originTimestamp)
                .IsEqualTo(readyBatch.StopwatchCreatedTicks + lingerTicks);
            await Assert.That(originTimestamp).IsLessThan(readyBatch.StopwatchSealedTicks);
        }
        finally
        {
            readyBatch.TrySetMemoryReleased();
            readyBatch.Fail(new InvalidOperationException("test cleanup"));
        }
    }

    private static ReadyBatch CreateReadyBatchSealedAfter(long creationAgeTicks, int lingerMs)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            LingerMs = lingerMs,
            DeliveryLatencyTargetMs = 10
        };
        var partitionBatch = new PartitionBatch(new TopicPartition("test-topic", 0), options);
        var simulatedCreationTimestamp = Stopwatch.GetTimestamp() - creationAgeTicks;
        typeof(PartitionBatch)
            .GetField("_createdStopwatchTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(partitionBatch, simulatedCreationTimestamp);
        var appendResult = partitionBatch.TryAppendFromSpans(
            timestamp: 0,
            keyData: ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            valueData: [1],
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            estimatedSize: 1);
        if (!appendResult.Success)
            throw new InvalidOperationException("Test batch append failed.");

        return partitionBatch.Complete()!;
    }

    private sealed class TrackingPipelinedResponseSource : IPipelinedResponseSource<ProduceResponse>
    {
        public int AbandonCalls;

        public void Abandon(short token) => Interlocked.Increment(ref AbandonCalls);

        public ProduceResponse GetResult(short token) => throw new InvalidOperationException();

        public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Pending;

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendFailure_AfterPipelinedResponseAcquired_IsAbandoned(
        CancellationToken cancellationToken)
    {
        var responseSource = new TrackingPipelinedResponseSource();
        var connection = new TestKafkaConnection { PipelinedResponseSource = responseSource };
        var (pool, _) = CreateScaleTrackingPool(connection);
        var options = CreateOptions(retryBackoffMs: 60_000, retryBackoffMaxMs: 60_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            onPipelinedResponseAcquired: () => throw new InvalidOperationException("test"));

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));

            await WaitUntilAsync(
                () => Volatile.Read(ref responseSource.AbandonCalls) == 1,
                cancellationToken);

            await Assert.That(Volatile.Read(ref responseSource.AbandonCalls)).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task ShouldMicroLinger_TransactionalAwaitedBatch_ReturnsFalse()
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition("test-topic", 0),
            new RecordBatch(),
            completionSourcesArray: null,
            completionSourcesCount: 1,
            dataSize: 1,
            recordCount: 1);

        var shouldLinger = BrokerSender.ShouldMicroLinger([batch], 1, isTransactional: true);

        await Assert.That(shouldLinger).IsFalse();
    }

    [Test]
    public async Task ShouldMicroLinger_FireAndForgetOrPlainProducer_ReturnsTrue()
    {
        var fireAndForget = new ReadyBatch();
        fireAndForget.Initialize(
            new TopicPartition("test-topic", 0),
            new RecordBatch(),
            completionSourcesArray: null,
            completionSourcesCount: 0,
            dataSize: 1,
            recordCount: 1);
        var awaited = new ReadyBatch();
        awaited.Initialize(
            new TopicPartition("test-topic", 1),
            new RecordBatch(),
            completionSourcesArray: null,
            completionSourcesCount: 1,
            dataSize: 1,
            recordCount: 1);

        await Assert.That(BrokerSender.ShouldMicroLinger([fireAndForget], 1, isTransactional: true))
            .IsTrue();
        await Assert.That(BrokerSender.ShouldMicroLinger([awaited], 1, isTransactional: false))
            .IsTrue();
        await Assert.That(BrokerSender.ShouldMicroLinger([awaited, awaited], 2, isTransactional: true))
            .IsTrue();
    }

    [Test]
    public async Task IsMatchingResponsePartition_RequiresExactTopicPartition()
    {
        var expected = new TopicPartition("test-topic", 2);

        await Assert.That(BrokerSender.IsMatchingResponsePartition(
            expected,
            responseTopic: "test-topic",
            responsePartition: 2)).IsTrue();
        await Assert.That(BrokerSender.IsMatchingResponsePartition(
            expected,
            responseTopic: "test-topic",
            responsePartition: 3)).IsFalse();
        await Assert.That(BrokerSender.IsMatchingResponsePartition(
            expected,
            responseTopic: "other-topic",
            responsePartition: 2)).IsFalse();
    }

    [Test]
    public async Task ShouldPublishResponseReadyEvent_DepthOneUsesDirectSignalOnly()
    {
        await Assert.That(BrokerSender.ShouldPublishResponseReadyEvent(totalMaxInFlight: 1))
            .IsFalse();
        await Assert.That(BrokerSender.ShouldPublishResponseReadyEvent(totalMaxInFlight: 2))
            .IsTrue();
    }

    [Test]
    public async Task SelectPendingResponseWaitMs_UsesRequestDeadline()
    {
        await Assert.That(BrokerSender.SelectPendingResponseWaitMs(nextWakeupMs: 30_000))
            .IsEqualTo(30_000);
        await Assert.That(BrokerSender.SelectPendingResponseWaitMs(nextWakeupMs: 0))
            .IsEqualTo(1);
    }

    [Test]
    public async Task SelectWaveCoalesceBounds_ScalesWithLingerAndRetainsHardCaps()
    {
        var zeroLinger = BrokerSender.SelectWaveCoalesceBounds(lingerMs: 0);
        var oneMillisecondLinger = BrokerSender.SelectWaveCoalesceBounds(lingerMs: 1);
        var configuredLinger = BrokerSender.SelectWaveCoalesceBounds(lingerMs: 5);
        var longLinger = BrokerSender.SelectWaveCoalesceBounds(lingerMs: 20);

        await Assert.That(zeroLinger.QuietMicroseconds).IsEqualTo(75);
        await Assert.That(zeroLinger.MaximumMicroseconds).IsEqualTo(500);
        await Assert.That(oneMillisecondLinger.QuietMicroseconds).IsEqualTo(200);
        await Assert.That(oneMillisecondLinger.MaximumMicroseconds).IsEqualTo(400);
        await Assert.That(configuredLinger.QuietMicroseconds).IsEqualTo(1_000);
        await Assert.That(configuredLinger.MaximumMicroseconds).IsEqualTo(2_000);
        await Assert.That(longLinger.QuietMicroseconds).IsEqualTo(1_000);
        await Assert.That(longLinger.MaximumMicroseconds).IsEqualTo(2_000);
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_WaveCoalesce_RearmsAfterIdleWaitAtDefaultLinger(
        CancellationToken cancellationToken)
    {
        var connection = new TestKafkaConnection
        {
            SendProduceFireAndForgetWithCallerTimeout = () => ValueTask.CompletedTask
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions(
            acks: Acks.None,
            enableIdempotence: false,
            lingerMs: 0,
            enableDeliveryDiagnostics: true);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var injectSibling = 0;
        BrokerSender? sender = null;
        sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            onWaveCoalesceStarted: () =>
            {
                if (Volatile.Read(ref injectSibling) != 0)
                    sender!.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            });

        try
        {
            // Prime both known partitions in one request. The next single-partition event
            // must enter the wave-coalesce path rather than being fully drained already.
            sender.EnqueueBulk(
            [
                CreateTestBatch(valueTaskSourcePool, "test-topic", 0),
                CreateTestBatch(valueTaskSourcePool, "test-topic", 1)
            ]);
            await WaitUntilAsync(
                () => accumulator.GetDeliveryDiagnosticsSnapshot()
                    .ProduceRequestCount == 1,
                cancellationToken);

            Volatile.Write(ref injectSibling, 1);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await WaitUntilAsync(
                () => accumulator.GetDeliveryDiagnosticsSnapshot()
                    .ProduceRequestCount == 2,
                cancellationToken);

            // The sender is fully idle again. Waiting for this event must re-arm the wave.
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await WaitUntilAsync(
                () => accumulator.GetDeliveryDiagnosticsSnapshot()
                    .ProduceRequestCount == 3,
                cancellationToken);

            var diagnostic = accumulator.GetDeliveryDiagnosticsSnapshot();
            await Assert.That(connection.SendFireAndForgetWithCallerTimeoutCalls).IsEqualTo(3);
            await Assert.That(diagnostic.CoalesceWidthHistogram
                    .Single(bucket => bucket.MinimumWidth == 2).RequestCount)
                .IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_UnexpectedExit_TerminatesEverySurvivingDelivery(
        CancellationToken cancellationToken)
    {
        var connection = new TestKafkaConnection
        {
            SendProduceFireAndForgetWithCallerTimeout = () => ValueTask.CompletedTask
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions(
            acks: Acks.None,
            enableIdempotence: false,
            lingerMs: 0);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var injectedFailure = new InvalidOperationException("injected send-loop failure");
        var allRerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedCount = 0;
        var crashEnabled = 0;
        ReadyBatch? queuedDuringCrash = null;
        BrokerSender? sender = null;
        sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            rerouteBatch: (batch, _) =>
            {
                batch.Fail(injectedFailure);
                if (Interlocked.Increment(ref reroutedCount) == 2)
                    allRerouted.TrySetResult();
            },
            onWaveCoalesceStarted: () =>
            {
                if (Interlocked.Exchange(ref crashEnabled, 0) == 0)
                    return;

                sender!.Enqueue(queuedDuringCrash!);
                throw injectedFailure;
            });

        try
        {
            var primeFirst = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 0);
            var primeSecond = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 1);
            sender.EnqueueBulk([primeFirst.Batch, primeSecond.Batch]);
            await Task.WhenAll(primeFirst.Delivery, primeSecond.Delivery).WaitAsync(cancellationToken);

            var coalesced = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 0);
            var queued = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 1);
            queuedDuringCrash = queued.Batch;
            Volatile.Write(ref crashEnabled, 1);

            sender.Enqueue(coalesced.Batch);
            await allRerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(sender.IsAlive).IsFalse();
            await Assert.That(Volatile.Read(ref reroutedCount)).IsEqualTo(2);
            await Assert.That(async () => await coalesced.Delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessage(injectedFailure.Message);
            await Assert.That(async () => await queued.Delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessage(injectedFailure.Message);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_UnexpectedExit_RedeliversPendingIdempotentBatches(
        CancellationToken cancellationToken)
    {
        var pendingResponse = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>([pendingResponse]);
        var (pool, _) = CreateMockConnection(responses);
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 2, enableIdempotence: true, lingerMs: 0);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var injectedFailure = new InvalidOperationException("injected pending-response failure");
        var allRerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedSequences = new ConcurrentQueue<int>();
        var reroutedCount = 0;
        var crashEnabled = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            rerouteBatch: (batch, _) =>
            {
                reroutedSequences.Enqueue(batch.RecordBatch.BaseSequence);
                batch.Fail(injectedFailure);
                if (Interlocked.Increment(ref reroutedCount) == 3)
                    allRerouted.TrySetResult();
            },
            onWaveCoalesceStarted: () =>
            {
                if (Interlocked.Exchange(ref crashEnabled, 0) != 0)
                    throw injectedFailure;
            });

        try
        {
            var pendingFirst = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 0);
            var pendingSecond = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 1);
            sender.EnqueueBulk([pendingFirst.Batch, pendingSecond.Batch]);
            await WaitUntilAsync(() => GetPendingResponseCount(sender) == 1, cancellationToken);

            var coalesced = CreateTestBatchWithDelivery(valueTaskSourcePool, "test-topic", 0);
            Volatile.Write(ref crashEnabled, 1);
            sender.Enqueue(coalesced.Batch);
            await allRerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(sender.IsAlive).IsFalse();
            await Assert.That(Volatile.Read(ref reroutedCount)).IsEqualTo(3);
            await Assert.That(reroutedSequences.Count(sequence => sequence >= 0)).IsEqualTo(2);
            await Assert.That(async () => await pendingFirst.Delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessage(injectedFailure.Message);
            await Assert.That(async () => await pendingSecond.Delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessage(injectedFailure.Message);
            await Assert.That(async () => await coalesced.Delivery.WaitAsync(cancellationToken))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessage(injectedFailure.Message);
        }
        finally
        {
            pendingResponse.TrySetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 0));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_WaveCoalesce_RearmsWhileResponseIsInFlight(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResponse = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var (pool, connection) = CreateMockConnection(responses);
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            acks: Acks.All,
            maxInFlight: 5,
            enableIdempotence: false,
            lingerMs: 0,
            enableDeliveryDiagnostics: true);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var injectSibling = 0;
        BrokerSender? sender = null;
        sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            onWaveCoalesceStarted: () =>
            {
                if (Volatile.Read(ref injectSibling) != 0)
                    sender!.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            });

        try
        {
            sender.EnqueueBulk(
            [
                CreateTestBatch(valueTaskSourcePool, "test-topic", 0),
                CreateTestBatch(valueTaskSourcePool, "test-topic", 1)
            ]);
            await WaitUntilAsync(
                () => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1,
                cancellationToken);

            Volatile.Write(ref injectSibling, 1);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await WaitUntilAsync(
                () => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 2,
                cancellationToken);

            await Assert.That(firstResponse.Task.IsCompleted).IsFalse();
            var diagnostic = accumulator.GetDeliveryDiagnosticsSnapshot();
            await Assert.That(diagnostic.CoalesceWidthHistogram
                    .Single(bucket => bucket.MinimumWidth == 2).RequestCount)
                .IsEqualTo(2);

            firstResponse.SetResult(CreateSuccessResponseForPartitions("test-topic", 2));
            secondResponse.SetResult(CreateSuccessResponseForPartitions("test-topic", 2));
        }
        finally
        {
            firstResponse.TrySetResult(CreateSuccessResponseForPartitions("test-topic", 2));
            secondResponse.TrySetResult(CreateSuccessResponseForPartitions("test-topic", 2));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions(Acks acks = Acks.All, int maxInFlight = 1,
        int retryBackoffMs = 100, int retryBackoffMaxMs = 1000,
        int deliveryTimeoutMs = 30_000, int requestTimeoutMs = 30_000,
        int connectionsPerBroker = 1, bool enableAdaptiveConnections = true,
        bool enableIdempotence = true, int batchSize = 1_048_576,
        long? unackedByteBudgetCapOverride = null, long? scaleCooldownMsOverride = null,
        string? transactionalId = null, int lingerMs = 0,
        bool enableDeliveryDiagnostics = false) => new()
        {
            BootstrapServers = ["localhost:9092"],
            MaxInFlightRequestsPerConnection = maxInFlight,
            ConnectionsPerBroker = connectionsPerBroker,
            EnableAdaptiveConnections = enableAdaptiveConnections,
            EnableIdempotence = enableIdempotence,
            Acks = acks,
            DeliveryTimeoutMs = deliveryTimeoutMs,
            RetryBackoffMs = retryBackoffMs,
            RetryBackoffMaxMs = retryBackoffMaxMs,
            RequestTimeoutMs = requestTimeoutMs,
            BatchSize = batchSize,
            LingerMs = lingerMs,
            EnableDeliveryDiagnostics = enableDeliveryDiagnostics,
            UnackedByteBudgetCapOverride = unackedByteBudgetCapOverride,
            ScaleCooldownMsOverride = scaleCooldownMsOverride,
            TransactionalId = transactionalId
        };

    private readonly List<ScriptedProduceResponses> _scriptedResponses = [];

    /// <summary>
    /// Links the test's cancellation token to every mock connection script created so far,
    /// so an unscripted (extra) send aborts in-test waits immediately instead of hanging
    /// until the test timeout (#2187).
    /// </summary>
    private CancellationToken GuardUnscriptedSends(CancellationToken testToken)
    {
        var guarded = testToken;
        foreach (var scripted in _scriptedResponses)
            guarded = scripted.Guard(guarded);
        return guarded;
    }

    [After(Test)]
    public void FailTestOnUnscriptedSend()
    {
        InvalidOperationException? failure = null;
        foreach (var scripted in _scriptedResponses)
        {
            scripted.Dispose();
            failure ??= scripted.UnscriptedSendFailure;
        }

        _scriptedResponses.Clear();
        if (failure is not null)
            throw failure;
    }

    /// <summary>
    /// Creates a mock connection pool that returns a controllable mock connection.
    /// The mock connection queues response tasks so each SendPipelinedAfterWriteAsync call
    /// returns the next TaskCompletionSource's task from the queue. An unscripted (extra)
    /// send parks the sender and fails the test with a named diagnostic instead of feeding
    /// BrokerSender's retry loop forever (#2187).
    /// The optional onSend callback fires each time SendPipelinedAfterWriteAsync is called,
    /// enabling deterministic synchronization without Task.Delay.
    /// </summary>
    private (IConnectionPool pool, TestKafkaConnection connection) CreateMockConnection(
        Queue<TaskCompletionSource<ProduceResponse>> responseQueue,
        Action? onSend = null)
    {
        var connection = new TestKafkaConnection();
        var scripted = new ScriptedProduceResponses(responseQueue, onSend);
        _scriptedResponses.Add(scripted);

        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted.Dequeue());

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(connection);

        return (pool, connection);
    }

    /// <summary>
    /// Creates a mock pool whose ScaleConnectionGroupAsync resolves the requested width and
    /// signals the returned TCS — shared by the scale-up trigger tests.
    /// </summary>
    private static (IConnectionPool pool, TaskCompletionSource<int> scaleRequested) CreateScaleTrackingPool(
        TestKafkaConnection connection)
    {
        var scaleRequested = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.ScaleConnectionGroupAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var targetCount = (int)callInfo[1]!;
                scaleRequested.TrySetResult(targetCount);
                return new ValueTask<int>(targetCount);
            });

        return (pool, scaleRequested);
    }

    /// <summary>
    /// Creates a minimal ReadyBatch suitable for send loop testing.
    /// Sets MemoryReleased=true to skip accumulator memory tracking.
    /// </summary>
    private static ReadyBatch CreateTestBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        string topic, int partition, int messageCount = 1,
        bool markMemoryReleased = true, int dataSize = 100)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(messageCount);
        for (var i = 0; i < messageCount; i++)
            sources[i] = pool.Rent();

        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            messageCount,
            dataSize: dataSize);

        if (markMemoryReleased)
        {
            // Skip accumulator memory tracking in tests
            batch.TrySetMemoryReleased();
        }

        return batch;
    }

    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTestBatchWithDelivery(
        ValueTaskSourcePool<RecordMetadata> pool,
        string topic,
        int partition)
    {
        var batch = new ReadyBatch();
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            completionSourcesCount: 1,
            dataSize: 100);
        batch.TrySetMemoryReleased();
        return (batch, delivery);
    }

    /// <summary>
    /// Creates a ProduceResponse with success for the given topic/partition.
    /// </summary>
    private static ProduceResponse CreateSuccessResponse(
        string topic, int partition, long baseOffset, int throttleTimeMs = 0) =>
        new()
        {
            ThrottleTimeMs = throttleTimeMs,
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = baseOffset
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateErrorResponse(string topic, int partition, ErrorCode errorCode) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = errorCode,
                            BaseOffset = -1
                        }
                    ]
                }
            ]
        };

    private static ProduceResponse CreateSuccessResponseForPartitions(string topic, int partitionCount) =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = partitionCount,
                    PartitionResponses = Enumerable.Range(0, partitionCount)
                        .Select(partition => new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = partition + 1
                        }).ToArray()
                }
            ]
        };

    private static MetadataResponse CreateLeaderMetadataResponse(
        string topic,
        int leaderId,
        int leaderEpoch,
        int partitionCount = 1) =>
        new()
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9094 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic,
                    Partitions = Enumerable.Range(0, partitionCount)
                        .Select(partition => new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = partition,
                            LeaderId = leaderId,
                            LeaderEpoch = leaderEpoch,
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1, 2]
                        }).ToArray()
                }
            ]
        };

    /// <summary>
    /// Creates a BrokerSender with standard test defaults, reducing constructor boilerplate.
    /// </summary>
    private static BrokerSender CreateSender(
        IConnectionPool pool,
        ProducerOptions options,
        RecordAccumulator accumulator,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?> onAcknowledgement,
        MetadataManager? metadataManager = null,
        Action<ReadyBatch, int>? rerouteBatch = null,
        Action<int>? onBrokerThrottle = null,
        Func<long>? getTimestamp = null,
        Func<int, CancellationToken, ValueTask>? delayForThrottle = null,
        Action? onBlockedBucketRequeued = null,
        Action? onPipelinedResponseAcquired = null,
        Action? onWaveCoalesceStarted = null,
        BrokerUnackedByteBudget? unackedBudget = null,
        int produceApiVersion = 9,
        bool isTransactional = false,
        bool usesTransactionV2 = false,
        Func<ReadyBatch[], int, Action<Exception?>, HashSet<TopicPartition>, HashSet<TopicPartition>,
            TransactionPartitionEnrollmentResult>?
            tryEnsurePartitionsInTransaction = null) =>
        new(
            brokerId: 1, pool,
            metadataManager ?? new MetadataManager(pool, options.BootstrapServers),
            accumulator, options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            getProduceApiVersion: () => produceApiVersion,
            setProduceApiVersion: _ => { },
            isTransactional: () => isTransactional,
            tryEnsurePartitionsInTransaction: tryEnsurePartitionsInTransaction,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch,
            onAcknowledgement: onAcknowledgement,
            logger: null,
            onBrokerThrottle: onBrokerThrottle,
            getTimestamp: getTimestamp,
            delayForThrottle: delayForThrottle,
            onBlockedBucketRequeued: onBlockedBucketRequeued,
            unackedBudget: unackedBudget,
            usesTransactionV2: () => usesTransactionV2,
            onPipelinedResponseAcquired: onPipelinedResponseAcquired,
            onWaveCoalesceStarted: onWaveCoalesceStarted);

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(120_000)]
    public async Task SendLoop_ConcurrentTransactions_RetriesOnlyForTv2(
        bool usesTransactionV2,
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, retryResponse]);
        var sends = Enumerable.Range(0, 2).Select(_ => new TaskCompletionSource()).ToArray();
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responses, () =>
        {
            sends[Interlocked.Increment(ref sendCount) - 1].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledged = new TaskCompletionSource<Exception?>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, exception) => acknowledged.TrySetResult(exception),
            produceApiVersion: ProduceRequest.ImplicitTransactionPartitionEnrollmentVersion,
            isTransactional: true,
            usesTransactionV2: usesTransactionV2);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await sends[0].Task.WaitAsync(cancellationToken);
            firstResponse.SetResult(CreateErrorResponse(
                "test-topic", partition: 0, ErrorCode.ConcurrentTransactions));

            var next = await Task.WhenAny(sends[1].Task, acknowledged.Task)
                .WaitAsync(cancellationToken);
            if (usesTransactionV2)
            {
                await Assert.That(next).IsSameReferenceAs(sends[1].Task);
                retryResponse.SetResult(
                    CreateSuccessResponse("test-topic", partition: 0, baseOffset: 42));
                await Assert.That(await acknowledged.Task.WaitAsync(cancellationToken)).IsNull();
                await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
            }
            else
            {
                await Assert.That(next).IsSameReferenceAs(acknowledged.Task);
                await Assert.That(await acknowledged.Task.WaitAsync(cancellationToken)).IsNotNull();
                await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);
            }
        }
        finally
        {
            retryResponse.TrySetResult(
                CreateSuccessResponse("test-topic", partition: 0, baseOffset: 42));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task DisposeAsync_PendingPipelinedResponse_IsAbandoned(
        CancellationToken cancellationToken)
    {
        var responseSource = new TrackingPipelinedResponseSource();
        var connection = new TestKafkaConnection { PipelinedResponseSource = responseSource };
        var (pool, _) = CreateScaleTrackingPool(connection);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await WaitUntilAsync(
                () => Volatile.Read(ref connection.SendPipelinedAfterWriteCalls) == 1,
                cancellationToken);

            await sender.DisposeAsync();

            await Assert.That(Volatile.Read(ref responseSource.AbandonCalls)).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoopPressure_ScalesConnections_WhenCoalescingBacklogPersists(CancellationToken cancellationToken)
    {
        const int partitionCount = 8;
        const int batchCount = 512;

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var connection = new TestKafkaConnection();
        var sendCount = 0;
        connection.SendProducePipelinedAfterWrite = () =>
        {
            Interlocked.Increment(ref sendCount);
            return new ValueTask<Task<ProduceResponse>>(
                Task.FromResult(CreateSuccessResponseForPartitions("test-topic", partitionCount)));
        };

        var (pool, scaleRequested) = CreateScaleTrackingPool(connection);

        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            for (var i = 0; i < batchCount; i++)
                sender.Enqueue(CreateTestBatch(vtPool, "test-topic", i % partitionCount));

            var targetCount = await scaleRequested.Task.WaitAsync(cancellationToken);

            await Assert.That(targetCount).IsGreaterThan(1);
            await Assert.That(Volatile.Read(ref sendCount)).IsGreaterThan(0);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipelinedProduce_UsesConnectionOwnedTimeoutsForConcurrentSends(CancellationToken cancellationToken)
    {
        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var (pool, connection) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 5);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledgedCount = 0;
        var allAcknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref acknowledgedCount) == 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 0));

            await sendSignals[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 1));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref connection.SendPipelinedAfterWriteCalls)).IsEqualTo(2);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedWithCallerTimeoutCalls)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedWithCallerTimeoutAfterWriteCalls)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref connection.SendPipelinedCalls)).IsEqualTo(0);

            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 43));
            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_TransactionEnrollmentPending_SendsAlreadyEnrolledPartition(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResponse = new TaskCompletionSource<ProduceResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var (pool, _) = CreateMockConnection(responses, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            if (index < sendSignals.Length)
                sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 2, transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskPool = new ValueTaskSourcePool<RecordMetadata>();
        var enrollmentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Exception?>? completeEnrollment = null;
        var partitionZeroEnrolled = false;

        TransactionPartitionEnrollmentResult TryEnsure(
            ReadyBatch[] batches,
            int count,
            Action<Exception?> completed,
            HashSet<TopicPartition> pendingPartitions,
            HashSet<TopicPartition> failedPartitions)
        {
            for (var i = 0; i < count; i++)
            {
                if (batches[i].TopicPartition.Partition == 0 && !partitionZeroEnrolled)
                {
                    pendingPartitions.Add(batches[i].TopicPartition);
                    completeEnrollment = completed;
                    enrollmentStarted.TrySetResult();
                    return TransactionPartitionEnrollmentResult.Pending;
                }
            }

            return TransactionPartitionEnrollmentResult.Enrolled;
        }

        var acknowledged = 0;
        var allAcknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, exception) =>
            {
                if (exception is null && Interlocked.Increment(ref acknowledged) == 2)
                    allAcknowledged.TrySetResult();
            },
            isTransactional: true,
            tryEnsurePartitionsInTransaction: TryEnsure);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 0));
            await enrollmentStarted.Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 1));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            firstResponse.SetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 10));
            partitionZeroEnrolled = true;
            completeEnrollment!(null);

            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 11));
            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            firstResponse.TrySetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 10));
            secondResponse.TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 11));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_TransactionEnrollmentReset_FailsParkedBatch(
        CancellationToken cancellationToken)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateOptions(transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskPool = new ValueTaskSourcePool<RecordMetadata>();
        var enrollmentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgedError = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Exception?>? completeEnrollment = null;

        TransactionPartitionEnrollmentResult TryEnsure(
            ReadyBatch[] batches,
            int count,
            Action<Exception?> completed,
            HashSet<TopicPartition> pendingPartitions,
            HashSet<TopicPartition> failedPartitions)
        {
            pendingPartitions.Add(batches[0].TopicPartition);
            completeEnrollment = completed;
            enrollmentStarted.TrySetResult();
            return TransactionPartitionEnrollmentResult.Pending;
        }

        var sender = CreateSender(
            connectionPool,
            options,
            accumulator,
            (_, _, _, _, exception) => acknowledgedError.TrySetResult(exception),
            isTransactional: true,
            tryEnsurePartitionsInTransaction: TryEnsure);

        try
        {
            var batch = CreateTestBatch(valueTaskPool, "test-topic", partition: 0);
            var completion = batch.DoneTask;
            sender.Enqueue(batch);
            await enrollmentStarted.Task.WaitAsync(cancellationToken);

            var enrollmentError = new TransactionException(
                "Transaction partition enrollment was reset before completion.");
            completeEnrollment!(enrollmentError);

            await Assert.That(await completion.AsTask().WaitAsync(cancellationToken)).IsFalse();
            await Assert.That(await acknowledgedError.Task.WaitAsync(cancellationToken))
                .IsSameReferenceAs(enrollmentError);
            await connectionPool.DidNotReceiveWithAnyArgs()
                .GetConnectionAsync(default, default);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_TransactionEnrollmentFailure_FailsAffectedBatch(
        CancellationToken cancellationToken)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateOptions(transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskPool = new ValueTaskSourcePool<RecordMetadata>();
        var enrollmentError = new TransactionException(
            ErrorCode.ProducerFenced,
            "Transaction partition enrollment failed.");
        var sender = CreateSender(
            connectionPool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            isTransactional: true,
            tryEnsurePartitionsInTransaction: (batches, _, _, _, failedPartitions) =>
            {
                failedPartitions.Add(batches[0].TopicPartition);
                return TransactionPartitionEnrollmentResult.Failed(enrollmentError);
            });

        try
        {
            var batch = CreateTestBatch(valueTaskPool, "test-topic", partition: 0);
            var completion = batch.DoneTask;

            sender.Enqueue(batch);

            await Assert.That(await completion.AsTask().WaitAsync(cancellationToken)).IsFalse();
            await connectionPool.DidNotReceiveWithAnyArgs()
                .GetConnectionAsync(default, default);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_TransactionEnrollmentFailure_DoesNotFailLaterPendingPartition(
        CancellationToken cancellationToken)
    {
        var response = new TaskCompletionSource<ProduceResponse>();
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>([response]);
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (connectionPool, _) = CreateMockConnection(responses, sendStarted.SetResult);
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskPool = new ValueTaskSourcePool<RecordMetadata>();
        var enrollmentCalls = 0;
        var firstPartitionPending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoPartitionsPending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Exception?>? enrollmentCompleted = null;
        var failedAcknowledgement = new TaskCompletionSource<Exception?>();
        var successfulAcknowledgement = new TaskCompletionSource<Exception?>();
        var enrollmentError = new TransactionException("Partition enrollment failed.");

        TransactionPartitionEnrollmentResult TryEnsure(
            ReadyBatch[] batches,
            int count,
            Action<Exception?> completed,
            HashSet<TopicPartition> pendingPartitions,
            HashSet<TopicPartition> failedPartitions)
        {
            var call = Interlocked.Increment(ref enrollmentCalls);
            if (call <= 2)
            {
                pendingPartitions.Add(batches[0].TopicPartition);
                enrollmentCompleted = completed;
                if (call == 1)
                    firstPartitionPending.TrySetResult();
                else
                    twoPartitionsPending.TrySetResult();
                return TransactionPartitionEnrollmentResult.Pending;
            }

            if (call == 3)
            {
                var failedPartition = new TopicPartition("test-topic", 0);
                var laterPartition = new TopicPartition("test-topic", 1);
                failedPartitions.Add(failedPartition);
                pendingPartitions.Add(laterPartition); // Simulates the long-lived pending accumulator.
                return TransactionPartitionEnrollmentResult.Failed(enrollmentError);
            }

            return TransactionPartitionEnrollmentResult.Enrolled;
        }

        var sender = CreateSender(
            connectionPool,
            options,
            accumulator,
            (topicPartition, _, _, _, exception) =>
            {
                var completion = topicPartition.Partition == 0
                    ? failedAcknowledgement
                    : successfulAcknowledgement;
                completion.TrySetResult(exception);
            },
            isTransactional: true,
            tryEnsurePartitionsInTransaction: TryEnsure);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 0));
            await firstPartitionPending.Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 1));
            await twoPartitionsPending.Task.WaitAsync(cancellationToken);
            enrollmentCompleted!(null);

            await Assert.That(await failedAcknowledgement.Task.WaitAsync(cancellationToken))
                .IsSameReferenceAs(enrollmentError);
            await sendStarted.Task.WaitAsync(cancellationToken);
            response.SetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 42));
            await Assert.That(await successfulAcknowledgement.Task.WaitAsync(cancellationToken)).IsNull();
        }
        finally
        {
            response.TrySetResult(CreateSuccessResponse("test-topic", partition: 1, baseOffset: 42));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_TransactionEnrollmentFailure_UnmutesRetryPartition(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (connectionPool, _) = CreateMockConnection(responses, () =>
            sendSignals[Interlocked.Increment(ref sendCount) - 1].TrySetResult());
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            transactionalId: "test-transaction");
        var accumulator = new RecordAccumulator(options);
        var valueTaskPool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledgements = new[]
        {
            new TaskCompletionSource<Exception?>(),
            new TaskCompletionSource<Exception?>()
        };
        var acknowledgementCount = 0;
        var enrollmentAttempts = 0;
        var enrollmentError = new TransactionException("Partition enrollment failed.");

        TransactionPartitionEnrollmentResult TryEnsure(
            ReadyBatch[] batches,
            int count,
            Action<Exception?> completed,
            HashSet<TopicPartition> pendingPartitions,
            HashSet<TopicPartition> failedPartitions)
        {
            if (Interlocked.Increment(ref enrollmentAttempts) != 2)
                return TransactionPartitionEnrollmentResult.Enrolled;

            failedPartitions.Add(batches[0].TopicPartition);
            return TransactionPartitionEnrollmentResult.Failed(enrollmentError);
        }

        var sender = CreateSender(
            connectionPool,
            options,
            accumulator,
            (_, _, _, _, exception) =>
            {
                var index = Interlocked.Increment(ref acknowledgementCount) - 1;
                acknowledgements[index].TrySetResult(exception);
            },
            isTransactional: true,
            tryEnsurePartitionsInTransaction: TryEnsure);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            firstResponse.SetResult(CreateErrorResponse(
                "test-topic", partition: 0, ErrorCode.NotLeaderOrFollower));
            await Assert.That(await acknowledgements[0].Task.WaitAsync(cancellationToken))
                .IsSameReferenceAs(enrollmentError);

            sender.Enqueue(CreateTestBatch(valueTaskPool, "test-topic", partition: 0));
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 42));
            await Assert.That(await acknowledgements[1].Task.WaitAsync(cancellationToken)).IsNull();
        }
        finally
        {
            firstResponse.TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 41));
            secondResponse.TrySetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 42));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_NonIdempotent_ErrorMutesUntilSamePartitionPipelineDrains(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var firstRetryResponse = new TaskCompletionSource<ProduceResponse>();
        var secondRetryResponse = new TaskCompletionSource<ProduceResponse>();
        var thirdResponse = new TaskCompletionSource<ProduceResponse>();
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse, firstRetryResponse, secondRetryResponse, thirdResponse]);
        var sends = Enumerable.Range(0, 5).Select(_ => new TaskCompletionSource()).ToArray();
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responses, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            if (index < sends.Length)
                sends[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 5,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledged = 0;
        var allAcknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, exception) =>
        {
            if (exception is null && Interlocked.Increment(ref acknowledged) == 3)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await sends[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await sends[1].Task.WaitAsync(cancellationToken);

            firstResponse.SetResult(CreateErrorResponse("test-topic", 0, ErrorCode.RequestTimedOut));
            await WaitUntilAsync(
                () => accumulator.IsMuted(new TopicPartition("test-topic", 0)),
                cancellationToken);
            var thirdBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0);
            sender.Enqueue(thirdBatch);

            await WaitUntilAsync(() => thirdBatch.DiagTrace.Contains('O'), cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            secondResponse.SetResult(CreateErrorResponse("test-topic", 0, ErrorCode.RequestTimedOut));
            await sends[2].Task.WaitAsync(cancellationToken);
            await Assert.That(accumulator.IsMuted(thirdBatch.TopicPartition)).IsTrue();

            firstRetryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));
            await sends[3].Task.WaitAsync(cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(4);

            secondRetryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 43));
            await sends[4].Task.WaitAsync(cancellationToken);
            thirdResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 44));
            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_NonIdempotent_EarlierError_RetriesAfterLaterSuccess(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responses = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse, retryResponse]);
        var sends = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource()).ToArray();
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responses, () =>
        {
            sends[Interlocked.Increment(ref sendCount) - 1].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 5,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledged = 0;
        var allAcknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, exception) =>
        {
            if (exception is null && Interlocked.Increment(ref acknowledged) == 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await sends[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", partition: 0));
            await sends[1].Task.WaitAsync(cancellationToken);

            firstResponse.SetResult(CreateErrorResponse(
                "test-topic", partition: 0, ErrorCode.RequestTimedOut));
            secondResponse.SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 42));

            // The later request may already be appended. The retry waits for it to drain,
            // then writes afterward: this is Kafka's non-idempotent retry-reordering trade-off.
            await sends[2].Task.WaitAsync(cancellationToken);
            retryResponse.SetResult(CreateSuccessResponse("test-topic", partition: 0, baseOffset: 43));
            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipelinedProduce_ReleasesBufferMemoryOnlyAfterWriteCompletes(CancellationToken cancellationToken)
    {
        var responseTcs = new TaskCompletionSource<ProduceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeCanComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new TestKafkaConnection();

        async ValueTask<Task<ProduceResponse>> CompleteAfterWriteGate()
        {
            writeStarted.TrySetResult();
            await writeCanComplete.Task.ConfigureAwait(false);
            return responseTcs.Task;
        }

        connection.SendProducePipelinedAfterWrite = CompleteAfterWriteGate;

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult();
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0, markMemoryReleased: false, dataSize: 0);
            await Assert.That(batch.MemoryReleased).IsFalse();

            sender.Enqueue(batch);
            await writeStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(batch.MemoryReleased).IsFalse();
            await Assert.That(acknowledged.Task.IsCompleted).IsFalse();

            writeCanComplete.SetResult();
            await WaitUntilAsync(() => batch.MemoryReleased, cancellationToken);

            await Assert.That(acknowledged.Task.IsCompleted).IsFalse();

            responseTcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));
            await acknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ResponseCompletion_WakesSendLoopAndProcessesBatch(CancellationToken cancellationToken)
    {
        // Verifies that when a response task completes, the response completion callback
        // pushes a ResponseCompleted event to the unified channel, waking the send loop.

        var tcs = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs);

        var requestSent = new TaskCompletionSource();
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () => requestSent.TrySetResult());
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<(long offset, int count)>();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, count, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult((offset, count));
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Wait for the send loop to actually send the request
            await requestSent.Task.WaitAsync(cancellationToken);

            // Complete the response — UnsafeOnCompleted callback pushes event, send loop wakes and processes
            tcs.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 42));

            // Wait for acknowledgement (with timeout to detect missed wakeup)
            var result = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(result.offset).IsEqualTo(42);
            await Assert.That(result.count).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightLimitEnforced_SecondBatchWaitsForFirstResponse(CancellationToken cancellationToken)
    {
        // With maxInFlight=1 and two batches on different partitions, they coalesce into
        // one request. After the response completes, both batches are acknowledged.
        // This verifies in-flight limiting uses _pendingResponses.Count and that
        // response completion correctly wakes the send loop.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var requestSent = new TaskCompletionSource();
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () => requestSent.TrySetResult());
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOffsets)
                {
                    ackOffsets.Add(offset);
                    if (ackOffsets.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue two batches on different partitions so both can coalesce
            // (same partition would cause carry-over due to one-per-partition rule)
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);

            sender.Enqueue(batch1);
            sender.Enqueue(batch2);

            // Wait for the send loop to send the coalesced request
            await requestSent.Task.WaitAsync(cancellationToken);

            // Complete the first response — this should process both batches since
            // they were coalesced into one request
            tcs1.SetResult(new ProduceResponse
            {
                TopicCount = 1,
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
                        PartitionCount = 2,
                        PartitionResponses =
                        [
                            new ProduceResponsePartitionData
                            {
                                Index = 0,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = 100
                            },
                            new ProduceResponsePartitionData
                            {
                                Index = 1,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = 200
                            }
                        ]
                    }
                ]
            });

            // Wait for all acknowledgements
            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(120_000)]
    public async Task SendLoop_SequentialRequests_SecondSendsAfterFirstResponseCompletes(CancellationToken cancellationToken)
    {
        // With maxInFlight=1, enqueue batches sequentially so each becomes its own
        // request. The second request can only be sent after the first response completes.
        // This verifies the in-flight wait loop correctly uses _pendingResponses.Count
        // and wakes via the event channel.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var allAcknowledged = new TaskCompletionSource();
        var ackCount = 0;

        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref ackCount) >= 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            // Enqueue first batch — send loop sends it immediately
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch1);

            // Wait for first request to be sent (deterministic, no Task.Delay)
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Enqueue second batch while first is still in-flight
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batch2);

            // Second request should NOT have been sent yet (in-flight limit).
            // Give a brief moment for the send loop to process, then verify.
            await Task.Delay(50, cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            // Complete first response — frees in-flight slot
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Wait for second request to be sent (deterministic)
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete second response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightByteBudget_SecondLargeRequestWaitsForFirstResponse(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 100,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 5_000));

            var observationWindow = Task.Delay(100, cancellationToken);
            var firstCompleted = await Task.WhenAny(sendSignals[1].Task, observationWindow);
            await Assert.That(firstCompleted).IsSameReferenceAs(observationWindow);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(1);

            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(120_000)]
    public async Task SendLoop_InFlightByteBudget_DoesNotBlockIdleConnection(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 100,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 5_000));
            await sendSignals[1].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_BlockedBucket_DoesNotPreventFreshWorkOnIdleConnection(
        CancellationToken cancellationToken)
    {
        var responses = new[]
        {
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>()
        };
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = new[]
        {
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource()
        };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 100,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false,
            enableIdempotence: false,
            batchSize: 100);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var blockedBucketRequeued = new TaskCompletionSource();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            onBlockedBucketRequeued: () => blockedBucketRequeued.TrySetResult());

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Partition 2 shares the saturated connection with partition 0. Partition 1
            // can send immediately, and partition 3 must remain serviceable afterward.
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 2, dataSize: 5_000));
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 10));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            await blockedBucketRequeued.Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 3, dataSize: 10));
            await sendSignals[2].Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
            responses[2].SetResult(CreateSuccessResponse("test-topic", 3, baseOffset: 300));
            await sendSignals[3].Task.WaitAsync(cancellationToken);
            responses[3].SetResult(CreateSuccessResponse("test-topic", 2, baseOffset: 400));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_StaleAfterWrite_ByteAccountingSkipsNulledBatch(
        CancellationToken cancellationToken)
    {
        var responses = new[]
        {
            new TaskCompletionSource<ProduceResponse>(),
            new TaskCompletionSource<ProduceResponse>()
        };
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        ReadyBatch? staleBatch = null;
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            if (index == 0)
                Interlocked.Exchange(ref staleBatch!._returnedToPool, 1);
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(
            maxInFlight: 100,
            enableAdaptiveConnections: false,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acknowledged = new TaskCompletionSource();
        var sender = CreateSender(pool, options, accumulator, (tp, _, _, _, ex) =>
        {
            if (tp.Partition == 1 && ex is null)
                acknowledged.TrySetResult();
        });

        try
        {
            staleBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 0);
            sender.Enqueue(staleBatch);
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));
            await acknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FaultedResponse_WakesSendLoopAndRetries(CancellationToken cancellationToken)
    {
        // When a response task faults (e.g., connection error), the response completion callback
        // pushes a ResponseCompleted event. The send loop processes it and retries the batch.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2); // For the retry

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult(offset);
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Wait for first send (deterministic)
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Fault the response — send loop should wake and retry
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for retry send (deterministic — send loop retries after backoff)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete the retry response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 99));

            // Batch should eventually be acknowledged
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(99);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(30_000)]
    public async Task SendLoop_SendFailure_RefreshesMetadataAndReroutes(CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var staleConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
            .Returns(staleConnection);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequests);
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(topic, leaderId: 2, leaderEpoch: 2));
            });

        var options = CreateOptions(retryBackoffMs: 10, retryBackoffMaxMs: 10);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(topic, leaderId: 1, leaderEpoch: 1));
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var rerouted = new TaskCompletionSource<ReadyBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (batch, _) => rerouted.TrySetResult(batch));

        try
        {
            var batch = CreateTestBatch(vtPool, topic, partition: 0);
            sender.Enqueue(batch);

            var reroutedBatch = await rerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(reroutedBatch).IsSameReferenceAs(batch);
            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
            await Assert.That(metadataManager.Metadata.GetPartitionLeader(topic, 0)!.NodeId).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_CoalescedSendFailure_RefreshesMetadataOncePerTopic(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var staleConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
            .Returns(staleConnection);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequests);
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2, partitionCount: 2));
            });

        var options = CreateOptions(retryBackoffMs: 10, retryBackoffMaxMs: 10);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(
            topic, leaderId: 1, leaderEpoch: 1, partitionCount: 2));
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var rerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (_, _) =>
            {
                if (Interlocked.Increment(ref reroutedCount) == 2)
                    rerouted.TrySetResult();
            });

        try
        {
            var firstBatch = CreateTestBatch(valueTaskSourcePool, topic, partition: 0);
            var secondBatch = CreateTestBatch(valueTaskSourcePool, topic, partition: 1);
            sender.EnqueueBulk(
            [
                firstBatch,
                secondBatch
            ]);

            await rerouted.Task.WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
            await Assert.That(accumulator.IsMuted(firstBatch.TopicPartition)).IsFalse();
            await Assert.That(accumulator.IsMuted(secondBatch.TopicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(30_000)]
    public async Task SendLoop_ParallelSendFailures_RefreshMetadataOncePerTopic(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var firstFailedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("First leader connection closed"))
        };
        var secondFailedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Second leader connection closed"))
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(firstFailedConnection));
        pool.GetConnectionByIndexAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(secondFailedConnection));

        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;
        var duplicateMetadataRequest = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref metadataRequests) > 1)
                    duplicateMetadataRequest.TrySetResult();

                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2, partitionCount: 2));
            });

        var options = CreateOptions(
            maxInFlight: 2,
            retryBackoffMs: 500,
            retryBackoffMaxMs: 500,
            connectionsPerBroker: 2);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(
            topic, leaderId: 1, leaderEpoch: 1, partitionCount: 2));
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var allRerouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reroutedCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: (_, _, _, _, _) => { },
            metadataManager,
            rerouteBatch: (_, _) =>
            {
                if (Interlocked.Increment(ref reroutedCount) == 2)
                    allRerouted.TrySetResult();
            });

        try
        {
            sender.EnqueueBulk(
            [
                CreateTestBatch(valueTaskSourcePool, topic, partition: 0),
                CreateTestBatch(valueTaskSourcePool, topic, partition: 1)
            ]);

            var firstResult = await Task.WhenAny(allRerouted.Task, duplicateMetadataRequest.Task)
                .WaitAsync(cancellationToken);

            await Assert.That(firstResult).IsSameReferenceAs(allRerouted.Task);
            await Assert.That(Volatile.Read(ref metadataRequests)).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task SendLoop_ParallelSendFailure_MutesPartitionBeforeSiblingWriteCompletes(
        CancellationToken cancellationToken)
    {
        const string topic = "test-topic";
        var failedConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () =>
                ValueTask.FromException<Task<ProduceResponse>>(new IOException("Leader connection closed"))
        };
        var siblingWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSiblingWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingConnection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = async () =>
            {
                siblingWriteStarted.TrySetResult();
                await releaseSiblingWrite.Task;
                return Task.FromResult(CreateSuccessResponse(topic, partition: 1, baseOffset: 0));
            }
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(failedConnection));
        pool.GetConnectionByIndexAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(siblingConnection));
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var metadataRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(metadataConnection));
        metadataConnection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));
        metadataConnection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                metadataRefreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(CreateLeaderMetadataResponse(
                    topic, leaderId: 2, leaderEpoch: 2));
            });

        var options = CreateOptions(
            maxInFlight: 2,
            retryBackoffMs: 10_000,
            retryBackoffMaxMs: 10_000,
            connectionsPerBroker: 2);
        var accumulator = new RecordAccumulator(options);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        metadataManager.Metadata.Update(CreateLeaderMetadataResponse(topic, leaderId: 1, leaderEpoch: 1));
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            metadataManager);

        try
        {
            var failedBatch = CreateTestBatch(vtPool, topic, partition: 0);
            var siblingBatch = CreateTestBatch(vtPool, topic, partition: 1);
            sender.EnqueueBulk([failedBatch, siblingBatch]);

            await siblingWriteStarted.Task.WaitAsync(cancellationToken);
            await metadataRefreshStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(accumulator.IsMuted(failedBatch.TopicPartition)).IsTrue();
            await Assert.That(failedBatch.RetryNotBefore).IsGreaterThan(0);
        }
        finally
        {
            releaseSiblingWrite.TrySetResult();
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_MultipleInFlight_AllResponsesProcessed(CancellationToken cancellationToken)
    {
        // With maxInFlight=5, multiple requests can be in-flight simultaneously.
        // Completing responses in any order should work because each response
        // pushes a ResponseCompleted event to the unified channel.

        const int batchCount = 3;
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var tcsList = new List<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < batchCount; i++)
        {
            var tcs = new TaskCompletionSource<ProduceResponse>();
            tcsList.Add(tcs);
            responseQueue.Enqueue(tcs);
        }

        var sendCount = 0;
        var allSent = new TaskCompletionSource();

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            // Batches may coalesce into fewer requests, so signal when all TCSs are dequeued
            if (Interlocked.Increment(ref sendCount) >= 1)
                allSent.TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 5);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOffsets)
                {
                    ackOffsets.Add(offset);
                    if (ackOffsets.Count >= batchCount)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batches on different partitions
            for (var i = 0; i < batchCount; i++)
            {
                var batch = CreateTestBatch(vtPool, "test-topic", i);
                sender.Enqueue(batch);
            }

            // Wait for at least one send to occur (deterministic)
            await allSent.Task.WaitAsync(cancellationToken);

            // Complete responses. Batches may coalesce into fewer requests, so each
            // response must include ALL partition data. Complete in reverse order to
            // verify order-independent processing.
            //
            // Each TCS gets its OWN ProduceResponse instance. The send loop returns every
            // processed response to a shared pool (ProduceResponse.Return resets TopicCount
            // to 0). When the batches are sent as more than one request — a legal, timing-
            // dependent coalescing outcome — sharing a single instance across the TCSs would
            // let the first processed request blank the data before later requests read it,
            // leaving their batches unacknowledged and hanging the test.
            static ProduceResponse CreateCombinedResponse(int batchCount) => new()
            {
                TopicCount = 1,
                Responses =
                [
                    new ProduceResponseTopicData
                    {
                        Name = "test-topic",
                        PartitionCount = batchCount,
                        PartitionResponses = Enumerable.Range(0, batchCount)
                            .Select(i => new ProduceResponsePartitionData
                            {
                                Index = i,
                                ErrorCode = ErrorCode.None,
                                BaseOffset = (i + 1) * 100
                            }).ToArray()
                    }
                ]
            };
            for (var i = batchCount - 1; i >= 0; i--)
                tcsList[i].TrySetResult(CreateCombinedResponse(batchCount));

            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
            await Assert.That(ackOffsets).Contains(300L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FireAndForget_CompletesWithoutPendingResponse(CancellationToken cancellationToken)
    {
        // With Acks.None, the send loop uses SendFireAndForgetAsync and completes
        // batches synchronously without adding to _pendingResponses.
        // Verifies fire-and-forget path works with UnsafeOnCompleted response callbacks.

        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);
        connection.SendFireAndForgetWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions(acks: Acks.None);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            acknowledged.TrySetResult(offset);
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // Fire-and-forget should complete quickly without waiting for a response
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(-1); // Fire-and-forget offset is -1
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FireAndForget_StartsSecondWriteBeforeFirstFlushCompletes(CancellationToken cancellationToken)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(1);

        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;
        BrokerSender? sender = null;

        var options = CreateOptions(
            acks: Acks.None,
            maxInFlight: 5,
            enableIdempotence: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var secondBatch = CreateTestBatch(vtPool, "test-topic", 1);

        connection.SendFireAndForgetWithCallerTimeoutAsync<ProduceRequest, ProduceResponse>(
                Arg.Any<ProduceRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var current = Interlocked.Increment(ref sendCount);
                if (current == 1)
                {
                    sender!.Enqueue(secondBatch);
                    return new ValueTask(firstWrite.Task);
                }

                if (current == 2)
                {
                    secondWriteStarted.TrySetResult();
                    return new ValueTask(secondWrite.Task);
                }

                return ValueTask.CompletedTask;
            });

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var acknowledgedCount = 0;
        var allAcknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        sender = CreateSender(pool, options, accumulator, (_, _, _, _, ex) =>
        {
            if (ex is null && Interlocked.Increment(ref acknowledgedCount) == 2)
                allAcknowledged.TrySetResult();
        });

        try
        {
            sender.Enqueue(CreateTestBatch(vtPool, "test-topic", 0));

            await secondWriteStarted.Task.WaitAsync(cancellationToken);

            await Assert.That(firstWrite.Task.IsCompleted).IsFalse();
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            firstWrite.SetResult();
            secondWrite.SetResult();

            await allAcknowledged.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (sender is not null)
                await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_FaultedResponseDuringInFlightWait_RetryBatchSurvivesCarryOverSwap(CancellationToken cancellationToken)
    {
        // Regression test for the carry-over list swap bug:
        // With maxInFlight=1, batch A is sent (fills the slot). Batch B arrives in the
        // next iteration, gets coalesced, and enters the in-flight capacity wait (line 529).
        // When batch A's response faults during the wait, ProcessCompletedResponses adds
        // the retry batch to a carry-over list. The bug was using pendingCarryOver (already
        // cleared at line 492) instead of newCarryOver — the retry batch was silently lost.
        //
        // This test verifies the retry batch survives: we should see 3 sends
        // (batch A, batch B after slot freed, batch A retry) and both partitions acknowledged.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var tcs3 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1); // Batch A — will fault
        responseQueue.Enqueue(tcs2); // Batch B — sent after slot freed
        responseQueue.Enqueue(tcs3); // Batch A retry

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 1, retryBackoffMs: 0, retryBackoffMaxMs: 0);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackPartitions = new List<int>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackPartitions)
                {
                    ackPartitions.Add(tp.Partition);
                    if (ackPartitions.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Enqueue batch A (partition 0) — send loop sends it immediately
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchA);

            // Wait for batch A to be sent (fills in-flight slot)
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Enqueue batch B (partition 1) — send loop reads it, coalesces it,
            // but enters in-flight capacity wait (1 in-flight >= maxInFlight=1).
            // Fault batch A's response immediately after enqueue — the send loop
            // will either find it already faulted when entering the capacity wait
            // (ProcessCompletedResponses handles it inline), or wake up from the
            // fault if already waiting. Both paths exercise the carry-over fix.
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchB);
            tcs1.SetException(new IOException("Connection reset"));

            // Wait for batch B to be sent (slot freed by processing faulted response)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete batch B's response
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            // Wait for batch A retry to be sent (survives carry-over swap)
            await sendSignals[2].Task.WaitAsync(cancellationToken);

            // Complete the retry response
            tcs3.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));

            // Both partitions should be acknowledged
            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackPartitions).Contains(0);
            await Assert.That(ackPartitions).Contains(1);

            // Verify all 3 sends occurred (batch A, batch B, batch A retry)
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_AlreadyCompletedResponse_ProcessedViaSynchronousFastPath(CancellationToken cancellationToken)
    {
        // Verifies the `if (responseTask.IsCompleted)` fast path in the send loop.
        // When the mock connection returns a Task that is already completed (via Task.FromResult),
        // the send loop should process the response synchronously without scheduling an
        // UnsafeOnCompleted callback. The batch should be acknowledged successfully.

        var successResponse = CreateSuccessResponse("test-topic", 0, baseOffset: 77);

        var connection = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(Task.FromResult(successResponse))
        };

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = CreateOptions();
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var acknowledged = new TaskCompletionSource<(long offset, int count)>();

        var sender = CreateSender(pool, options, accumulator, (tp, offset, _, count, ex) =>
        {
            if (ex is null)
                acknowledged.TrySetResult((offset, count));
        });

        try
        {
            var batch = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch);

            // The response is already complete, so acknowledgement should happen very quickly
            var result = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(result.offset).IsEqualTo(77);
            await Assert.That(result.count).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PipeliningEnabled_SendsSecondBatchBeforeFirstResponse(CancellationToken cancellationToken)
    {
        // With maxInFlight=2, after sending batch A (in-flight=1), the canPipeline guard
        // evaluates to true (sentThisIteration=true, carryOver empty, 1 < 2). The send loop
        // waits on the event channel instead of WaitForAnyResponseAsync. When batch B arrives
        // via a NewBatch event, the loop wakes and sends B immediately — before A's response
        // arrives. This verifies the pipelining optimization introduced in PR #704.

        var tcs1 = new TaskCompletionSource<ProduceResponse>();
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 2);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var ackOffsets = new List<long>();
        var allAcknowledged = new TaskCompletionSource();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is null)
            {
                lock (ackOffsets)
                {
                    ackOffsets.Add(offset);
                    if (ackOffsets.Count >= 2)
                        allAcknowledged.TrySetResult();
                }
            }
        });

        try
        {
            // Send batch A (partition 0) — in-flight count becomes 1
            var batchA = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batchA);
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Batch A's response is still pending. canPipeline = true because:
            // sentThisIteration=true, carryOver.Count=0, waitPendingCount(1) < _totalMaxInFlight(2).
            // The send loop is now waiting on the event channel (not WaitForAnyResponseAsync).

            // Enqueue batch B (partition 1) — this pushes a NewBatch event to the channel,
            // waking the send loop. It should send B immediately without waiting for A's response.
            var batchB = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batchB);

            // Wait for batch B to be sent — this proves pipelining worked: B was sent
            // while A's response was still pending.
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Both requests are now in-flight. Verify A's response hasn't arrived yet.
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);

            // Complete both responses
            tcs1.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 100));
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 200));

            await allAcknowledged.Task.WaitAsync(cancellationToken);

            await Assert.That(ackOffsets).Contains(100L);
            await Assert.That(ackOffsets).Contains(200L);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_HungResponseWithExpiredBatches_FreesCapacitySlot(CancellationToken cancellationToken)
    {
        // Regression test: when a response task never completes (hung connection),
        // HandleTimedOutRequests (v4) directly processes all batches from _pendingResponses
        // and clears the list — freeing the capacity slot without relying on connection
        // disposal. The response task is orphaned (nobody polls it).
        //
        // Scenario: maxInFlight=1, batch A is sent, response hangs. Request timeout
        // fires (1s). HandleTimedOutRequests processes batch A directly (delivery timeout
        // also exceeded → permanently failed), clears _pendingResponses. Second batch B
        // should then be sendable.

        var tcs1 = new TaskCompletionSource<ProduceResponse>(); // Simulates hung connection
        var tcs2 = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        responseQueue.Enqueue(tcs1);
        responseQueue.Enqueue(tcs2);

        var sendCount = 0;
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var idx = Interlocked.Increment(ref sendCount) - 1;
            if (idx < sendSignals.Length)
                sendSignals[idx].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        // Short request timeout (1s) so HandleTimedOutRequests fires quickly.
        // Delivery timeout also 1s so the batch is permanently failed rather than retried.
        var options = CreateOptions(maxInFlight: 1, deliveryTimeoutMs: 1_000, requestTimeoutMs: 1_000);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var batch1Failed = new TaskCompletionSource();
        var acknowledged = new TaskCompletionSource<long>();

        var sender = CreateSender(pool, options, accumulator, (_, offset, _, _, ex) =>
        {
            if (ex is not null)
                batch1Failed.TrySetResult();
            else
                acknowledged.TrySetResult(offset);
        });

        try
        {
            // Send first batch — response will hang
            var batch1 = CreateTestBatch(vtPool, "test-topic", 0);
            sender.Enqueue(batch1);
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            // Wait for HandleTimedOutRequests to process batch1 (request timeout 1s +
            // delivery timeout 1s exceeded → permanently failed). Uses deterministic
            // signal instead of Task.Delay to avoid flakiness on slow CI runners.
            await batch1Failed.Task.WaitAsync(cancellationToken);

            // Send second batch on different partition — should not hang
            // (HandleTimedOutRequests cleared _pendingResponses, freeing the capacity slot)
            var batch2 = CreateTestBatch(vtPool, "test-topic", 1);
            sender.Enqueue(batch2);

            // Second batch should be sent (send loop freed the capacity slot)
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            // Complete second response and verify acknowledgement
            tcs2.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 42));
            var offset = await acknowledged.Task.WaitAsync(cancellationToken);
            await Assert.That(offset).IsEqualTo(42);
        }
        finally
        {
            // tcs1 is orphaned (HandleTimedOutRequests doesn't await it), cancel to avoid
            // unobserved task exception during test cleanup.
            tcs1.TrySetCanceled(CancellationToken.None);
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    [Arguments(5, 100, 5)]
    [Arguments(20, 100, 20)]
    [Arguments(250, 100, 100)]
    [Arguments(20, 7, 7)]
    public async Task ComputeThrottledResponseWaitMs_BoundsEveryDeadline(
        int throttleDelayMs,
        int batchDeadlineMs,
        int expectedWaitMs)
    {
        var waitMs = BrokerSender.ComputeThrottledResponseWaitMs(
            throttleDelayMs,
            batchDeadlineMs);

        await Assert.That(waitMs).IsEqualTo(expectedWaitMs);
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ThrottleObservedDuringConnectionCapacityWait_DelaysRemainingBucket(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 250;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var thirdResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, secondResponse, thirdResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(),
            new TaskCompletionSource(),
            new TaskCompletionSource()
        };
        var sendCount = 0;
        var scripted0 = new ScriptedProduceResponses(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        _scriptedResponses.Add(scripted0);
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var connection0 = new TestKafkaConnection
        {
            SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted0.Dequeue())
        };
        var connection1 = new TestKafkaConnection();
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection0);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask<IKafkaConnection>(
                (int)callInfo[1]! == 0 ? connection0 : connection1));

        var options = CreateOptions(
            maxInFlight: 2,
            connectionsPerBroker: 2,
            enableAdaptiveConnections: false);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleWaitStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThrottleWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timestamp = 0L;
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 3)
                    acknowledgements.TrySetResult();
            },
            getTimestamp: () => Volatile.Read(ref timestamp),
            delayForThrottle: (delayMs, token) =>
            {
                throttleWaitStarted.TrySetResult(delayMs);
                return new ValueTask(releaseThrottleWait.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 2));
            await sendSignals[1].Task.WaitAsync(cancellationToken);

            var delayedBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 4);
            delayedBatch.EnableDeliveryDiagnostics();
            sender.Enqueue(delayedBatch);
            await WaitUntilAsync(() => delayedBatch.DiagTrace.Contains('S'), cancellationToken);

            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 2, baseOffset: 11));

            var firstProgress = await Task.WhenAny(throttleWaitStarted.Task, sendSignals[2].Task)
                .WaitAsync(cancellationToken);
            await Assert.That(firstProgress).IsSameReferenceAs(throttleWaitStarted.Task);
            await Assert.That(await throttleWaitStarted.Task).IsEqualTo(throttleTimeMs);
            await Assert.That(sendSignals[2].Task.IsCompleted).IsFalse();

            Volatile.Write(ref timestamp, Stopwatch.Frequency);
            releaseThrottleWait.SetResult();

            await sendSignals[2].Task.WaitAsync(cancellationToken);
            thirdResponse.SetResult(CreateSuccessResponse("test-topic", 4, baseOffset: 12));
            await acknowledgements.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_PositiveBrokerThrottle_DelaysNextRequestWithoutDeliveryErrors(
        CancellationToken cancellationToken)
    {
        const int throttleTimeMs = 250;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleWaitStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseThrottleWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedThrottleTimes = new List<int>();
        var timestamp = 0L;
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 2)
                    acknowledgements.TrySetResult();
            },
            onBrokerThrottle: observedThrottleTimes.Add,
            getTimestamp: () => Volatile.Read(ref timestamp),
            delayForThrottle: (delayMs, token) =>
            {
                throttleWaitStarted.TrySetResult(delayMs);
                return new ValueTask(releaseThrottleWait.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));

            var requestedDelayMs = await throttleWaitStarted.Task.WaitAsync(cancellationToken);
            await Assert.That(requestedDelayMs).IsEqualTo(throttleTimeMs);
            await Assert.That(sendSignals[1].Task.IsCompleted).IsFalse();

            Volatile.Write(ref timestamp, Stopwatch.Frequency);
            releaseThrottleWait.SetResult();

            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 11));
            await acknowledgements.Task.WaitAsync(cancellationToken);

            await Assert.That(observedThrottleTimes).IsEquivalentTo([throttleTimeMs, 0]);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_ZeroBrokerThrottle_DoesNotDelayNextRequest(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions(maxInFlight: 1);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var throttleDelayCalled = false;
        var observedThrottleTimes = new List<int>();
        var acknowledgements = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgementCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, error) =>
            {
                if (error is null && Interlocked.Increment(ref acknowledgementCount) == 2)
                    acknowledgements.TrySetResult();
            },
            onBrokerThrottle: observedThrottleTimes.Add,
            delayForThrottle: (_, _) =>
            {
                throttleDelayCalled = true;
                return ValueTask.CompletedTask;
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1));
            firstResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 20));

            await sendSignals[1].Task.WaitAsync(cancellationToken);
            secondResponse.SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 21));
            await acknowledgements.Task.WaitAsync(cancellationToken);

            await Assert.That(throttleDelayCalled).IsFalse();
            await Assert.That(observedThrottleTimes).IsEquivalentTo([0, 0]);
            await Assert.That(Volatile.Read(ref sendCount)).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task SendLoop_InjectedClockControlsThrottleWakeupAndDeliveryExpiry(
        CancellationToken cancellationToken)
    {
        const int deliveryTimeoutMs = 10_000;
        const int throttleTimeMs = 20_000;

        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var secondResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>([firstResponse, secondResponse]);
        var sendSignals = new[] { new TaskCompletionSource(), new TaskCompletionSource() };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        var options = CreateOptions(maxInFlight: 1, deliveryTimeoutMs: deliveryTimeoutMs);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var expiringBatch = CreateTestBatch(valueTaskSourcePool, "test-topic", 1);
        var fakeTimestamp = expiringBatch.StopwatchCreatedTicks + options.DeliveryTimeoutTicks / 2;
        var firstDelayRequested = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unexpectedSecondDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDelayGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveryFailure = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var delayCount = 0;

        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (topicPartition, _, _, _, error) =>
            {
                if (topicPartition.Partition == 1 && error is not null)
                    deliveryFailure.TrySetResult(error);
            },
            getTimestamp: () => Volatile.Read(ref fakeTimestamp),
            delayForThrottle: (delayMs, token) =>
            {
                if (Interlocked.Increment(ref delayCount) == 1)
                {
                    firstDelayRequested.TrySetResult(delayMs);
                    Volatile.Write(
                        ref fakeTimestamp,
                        expiringBatch.StopwatchCreatedTicks + options.DeliveryTimeoutTicks);
                    return ValueTask.CompletedTask;
                }

                unexpectedSecondDelay.TrySetResult();
                return new ValueTask(secondDelayGate.Task.WaitAsync(token));
            });

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            sender.Enqueue(expiringBatch);
            firstResponse.SetResult(CreateSuccessResponse(
                "test-topic", 0, baseOffset: 10, throttleTimeMs));

            await Assert.That(await firstDelayRequested.Task.WaitAsync(cancellationToken))
                .IsEqualTo(deliveryTimeoutMs / 2);

            var progress = await Task.WhenAny(deliveryFailure.Task, unexpectedSecondDelay.Task)
                .WaitAsync(cancellationToken);
            await Assert.That(progress).IsSameReferenceAs(deliveryFailure.Task);
            await Assert.That(await deliveryFailure.Task).IsTypeOf<KafkaTimeoutException>();
            await Assert.That(sendSignals[1].Task.IsCompleted).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task Constructor_AppliesCapWithoutInflatingCongestionWindow()
    {
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        var (pool, _) = CreateMockConnection(responseQueue);
        var options = CreateOptions(unackedByteBudgetCapOverride: 4_096);
        var accumulator = new RecordAccumulator(options);
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 100, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { }, unackedBudget: budget);

        try
        {
            // Raising the safety cap must not manufacture congestion-window headroom.
            await Assert.That(budget.BudgetBytes).IsEqualTo(100);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task ProcessCompletedResponses_FeedsAckedBytesIntoUnackedBudget(
        CancellationToken cancellationToken)
    {
        const int batchCount = 1;
        const int dataSize = 5_000;

        var responses = Enumerable.Range(0, batchCount)
            .Select(_ => new TaskCompletionSource<ProduceResponse>())
            .ToArray();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(responses);
        var sendSignals = Enumerable.Range(0, batchCount)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);

        // One successful request must feed logical bytes and request RTT into the budget.
        var options = CreateOptions(maxInFlight: 1, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.000001, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            for (var i = 0; i < batchCount; i++)
            {
                var batch = CreateTestBatch(
                    valueTaskSourcePool,
                    "test-topic",
                    0,
                    dataSize: dataSize);
                batch.SetEncodedSize(500);
                sender.Enqueue(batch);
            }

            for (var i = 0; i < batchCount; i++)
            {
                await sendSignals[i].Task.WaitAsync(cancellationToken);
                responses[i].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: i * 10));
            }

            await WaitUntilAsync(() => budget.MinimumRttMicros > 0, cancellationToken);
            var requestSizes = budget.CopyRequestSizeHistogram();
            await Assert.That(requestSizes[4]).IsEqualTo(1);
            await Assert.That(requestSizes.Sum()).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task RttAnchor_PrecedesWrite_SynchronousResponseCannotYieldMicrosecondRtt(
        CancellationToken cancellationToken)
    {
        // The mock write takes ~20ms and hands back an already-completed response — the
        // pathological localhost shape where the response arrives before the write await
        // resumes. An RTT anchor stamped after that await measured poll-loop latency here
        // (microseconds), disabling the budget's RTT safety floor (#1941). The pre-write
        // anchor includes the write time, so the observed minimum cannot undercut it.
        var (pool, connection) = CreateMockConnection(new Queue<TaskCompletionSource<ProduceResponse>>());
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var responseSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendProducePipelinedAfterWrite = async () =>
        {
            await Task.Delay(20);
            responseSent.TrySetResult();
            return Task.FromResult(CreateSuccessResponse("test-topic", 0, baseOffset: 0));
        };

        var options = CreateOptions(maxInFlight: 1, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));

            await responseSent.Task.WaitAsync(cancellationToken);
            await WaitUntilAsync(() => budget.MinimumRttMicros > 0, cancellationToken);

            // Half the injected write delay keeps this robust on slow runners while still
            // catching a post-write anchor, which reads single-digit microseconds.
            await Assert.That(budget.MinimumRttMicros).IsGreaterThanOrEqualTo(10_000);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task DeliverySnapshot_SecondPipelinedRequest_IsNotAppLimited(
        CancellationToken cancellationToken)
    {
        var responses = new[]
        {
            new TaskCompletionSource<ProduceResponse>(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource<ProduceResponse>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var (pool, _) = CreateMockConnection(
            new Queue<TaskCompletionSource<ProduceResponse>>(responses));
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 2, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await WaitUntilAsync(() => GetPendingResponseCount(sender) == 1, cancellationToken);

            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 1, dataSize: 5_000));
            await WaitUntilAsync(() => GetPendingResponseCount(sender) == 2, cancellationToken);

            await Assert.That(GetDeliverySnapshot(sender, 0).AppLimited).IsTrue();
            await Assert.That(GetDeliverySnapshot(sender, 1).AppLimited).IsFalse();
            await Assert.That(GetDeliverySnapshot(sender, 0).OldestBatchTimestamp).IsGreaterThan(0);
            await Assert.That(GetDeliverySnapshot(sender, 1).OldestBatchTimestamp).IsGreaterThan(0);

            responses[0].SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 0));
            responses[1].SetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 1));
        }
        finally
        {
            responses[0].TrySetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 0));
            responses[1].TrySetResult(CreateSuccessResponse("test-topic", 1, baseOffset: 1));
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    private static int GetPendingResponseCount(BrokerSender sender) =>
        (int)typeof(BrokerSender).GetField(
            "_totalPendingResponseCount",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;

    private static BrokerUnackedByteBudget.DeliverySnapshot GetDeliverySnapshot(
        BrokerSender sender,
        int index)
    {
        var pendingByConnection = (Array)typeof(BrokerSender).GetField(
            "_pendingResponsesByConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;
        var pending = pendingByConnection.GetValue(0)!;
        var item = pending.GetType().GetProperty("Item")!.GetValue(pending, [index])!;
        return (BrokerUnackedByteBudget.DeliverySnapshot)item.GetType()
            .GetProperty("DeliverySnapshotAtSend")!.GetValue(item)!;
    }

    [Test]
    [Timeout(120_000)]
    public async Task FaultedResponses_DoNotFeedUnackedBudgetDrainRate(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, retryResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var sendCount = 0;

        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 1, unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            firstResponse.SetException(new IOException("connection reset"));

            // The retry send proves the faulted response was fully processed. Faulted
            // responses must not enter controller samples or diagnostics.
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(0);
            await Assert.That(budget.CopyRequestSizeHistogram().Sum()).IsEqualTo(0);

            retryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task PartitionErrors_DoNotFeedUnackedBudgetDrainRate(
        CancellationToken cancellationToken)
    {
        var firstResponse = new TaskCompletionSource<ProduceResponse>();
        var retryResponse = new TaskCompletionSource<ProduceResponse>();
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>(
            [firstResponse, retryResponse]);
        var sendSignals = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var sendCount = 0;
        var (pool, _) = CreateMockConnection(responseQueue, onSend: () =>
        {
            var index = Interlocked.Increment(ref sendCount) - 1;
            sendSignals[index].TrySetResult();
        });
        cancellationToken = GuardUnscriptedSends(cancellationToken);
        var options = CreateOptions(maxInFlight: 1, retryBackoffMs: 0,
            unackedByteBudgetCapOverride: 1_000_000);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", 0, dataSize: 5_000));
            await sendSignals[0].Task.WaitAsync(cancellationToken);

            firstResponse.SetResult(CreateErrorResponse("test-topic", 0, ErrorCode.RequestTimedOut));

            // Retry proves the partition error was processed. It must not establish a
            // successful goodput sample from the failed request.
            await sendSignals[1].Task.WaitAsync(cancellationToken);
            await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(0);
            await Assert.That(budget.CopyRequestSizeHistogram().Sum()).IsEqualTo(0);

            retryResponse.SetResult(CreateSuccessResponse("test-topic", 0, baseOffset: 10));
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task AdmissionBlockPressure_TriggersScaleUp_WithoutBufferUtilization(
        CancellationToken cancellationToken)
    {
        const int partitionCount = 4;

        var options = CreateOptions(maxInFlight: 1,
            unackedByteBudgetCapOverride: 1_000_000, scaleCooldownMsOverride: 0);
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var connection = new TestKafkaConnection();
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(
            Task.FromResult(CreateSuccessResponseForPartitions("test-topic", partitionCount)));

        var (pool, scaleRequested) = CreateScaleTrackingPool(connection);

        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1);
        var sender = CreateSender(pool, options, accumulator, (_, _, _, _, _) => { },
            unackedBudget: budget);

        try
        {
            // Simulate a workload throttled by the admission gate: buffer utilization stays
            // near zero, but blocked admissions accumulate. This alone must trigger scale-up.
            for (var i = 0; i < 200; i++)
                budget.RecordAdmissionBlock();

            // Keep the send loop iterating so it reaches the scale check.
            for (var i = 0; i < 32; i++)
                sender.Enqueue(CreateTestBatch(valueTaskSourcePool, "test-topic", i % partitionCount));

            var targetCount = await scaleRequested.Task.WaitAsync(cancellationToken);
            await Assert.That(targetCount).IsGreaterThan(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }
}
