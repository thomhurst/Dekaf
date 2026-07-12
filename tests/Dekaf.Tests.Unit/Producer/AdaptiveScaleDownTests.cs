using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Regression tests for adaptive connection scale-down and unexpected send-loop exit.
///
/// Covers the failure modes from stress run 28972770086:
/// - Scale-down previously shrank the per-connection arrays before fencing, crashing the
///   send loop with IndexOutOfRangeException whenever a known partition mapped to the
///   removed connection slot — the crash then permanently failed every queued and
///   in-flight batch with ObjectDisposedException (12,844 messages lost).
/// - Scale-down could silently drop a populated pending-response list, stranding batches
///   whose delivery tasks never completed (#1578).
/// - Batches surviving an unexpected send-loop exit are now redelivered to a replacement
///   sender instead of being permanently failed.
/// </summary>
public sealed class AdaptiveScaleDownTests
{
    private const string Topic = "test-topic";
    private const int PartitionCount = 8;

    private static ProducerOptions CreateOptions(
        bool idempotent,
        int deliveryTimeoutMs = 30_000,
        int maxInFlightRequests = 1,
        long? scaleCooldownMs = null,
        long? scaleDownSustainedMs = null) => new()
        {
            BootstrapServers = ["localhost:9092"],
            MaxInFlightRequestsPerConnection = maxInFlightRequests,
            Acks = Acks.All,
            EnableIdempotence = idempotent,
            DeliveryTimeoutMs = deliveryTimeoutMs,
            RetryBackoffMs = 100,
            RetryBackoffMaxMs = 1000,
            RequestTimeoutMs = 30_000,
            LingerMs = 0,
            ConnectionsPerBroker = 1,
            EnableAdaptiveConnections = true,
            MaxConnectionsPerBroker = 4,
            ScaleCooldownMsOverride = scaleCooldownMs,
            ScaleDownSustainedMsOverride = scaleDownSustainedMs
        };

    private static ReadyBatch CreateTestBatch(
        ValueTaskSourcePool<RecordMetadata> pool, int partition, int dataSize = 100)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = pool.Rent();

        batch.Initialize(
            new TopicPartition(Topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            completionSourcesCount: 1,
            dataSize: dataSize);

        batch.TrySetMemoryReleased(); // Skip accumulator memory tracking in tests
        return batch;
    }

    private static ProduceResponse CreateSuccessResponseForAllPartitions() =>
        new()
        {
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = Topic,
                    PartitionCount = PartitionCount,
                    PartitionResponses = Enumerable.Range(0, PartitionCount)
                        .Select(partition => new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = partition + 1
                        }).ToArray()
                }
            ]
        };

    private static BrokerSender CreateSender(
        IConnectionPool pool,
        ProducerOptions options,
        RecordAccumulator accumulator,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?>? onAcknowledgement,
        Action<ReadyBatch, int>? rerouteBatch = null,
        bool canPhysicallyShrinkConnections = true,
        TimeSpan? disposalDrainTimeout = null) =>
        new(
            brokerId: 1, pool,
            new MetadataManager(pool, options.BootstrapServers),
            accumulator, options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            tryEnsurePartitionsInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: rerouteBatch,
            onAcknowledgement: onAcknowledgement,
            logger: null,
            canPhysicallyShrinkConnections: canPhysicallyShrinkConnections,
            disposalDrainTimeout: disposalDrainTimeout);

    private static IKafkaConnection?[] GetPinnedConnections(BrokerSender sender)
        => (IKafkaConnection?[])typeof(BrokerSender).GetField(
            "_pinnedConnections",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;

    private static ValueTask<KafkaConnectionLease> GetConnectionLeaseAtIndexAsync(
        BrokerSender sender,
        int connectionIndex)
        => (ValueTask<KafkaConnectionLease>)typeof(BrokerSender).GetMethod(
            "GetConnectionLeaseAtIndexAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                sender,
                [connectionIndex, CancellationToken.None])!;

    private static async Task AssertDisposeWaitsForLeaseAsync(
        BrokerSender sender,
        TestKafkaConnection connection,
        IRetirableKafkaConnection retirableConnection,
        CancellationToken cancellationToken)
    {
        var disposalTask = sender.DisposeAsync().AsTask();
        try
        {
            await connection.LeaseCountObserved.Task.WaitAsync(cancellationToken);

            await Assert.That(disposalTask.IsCompleted).IsFalse();
            await Assert.That(connection.DisposeStarted.Task.IsCompleted).IsFalse();
            await Assert.That(Volatile.Read(ref connection.DisposeCalls)).IsEqualTo(0);
        }
        finally
        {
            retirableConnection.ReleaseLease();
            await disposalTask.WaitAsync(cancellationToken);
        }

        await Assert.That(Volatile.Read(ref connection.CompleteRetirementCalls)).IsEqualTo(1);
        await Assert.That(Volatile.Read(ref connection.DisposeCalls)).IsEqualTo(1);
    }

    /// <summary>
    /// Drives a full scale-up → scale-down cycle through the live send loop with an
    /// idempotent producer whose partitions span every connection slot, then keeps
    /// producing. Under the pre-fix code the scale-down apply crashed the send loop
    /// (fencing indexed the already-removed slot) and every subsequent batch failed
    /// with ObjectDisposedException — so this test asserts zero failed acknowledgements
    /// and that the removed connection is disposed only after the cycle completes.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task ScaleDown_UnderContinuousIdempotentTraffic_LosesNoBatches(CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: true, scaleCooldownMs: 25, scaleDownSustainedMs: 50);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();

        var connection = new TestKafkaConnection();
        connection.SendProducePipelinedAfterWrite = () =>
            new ValueTask<Task<ProduceResponse>>(Task.FromResult(CreateSuccessResponseForAllPartitions()));

        var removedConnection = new TestKafkaConnection();

        var scaleUpApplied = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var shrinkRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        pool.ScaleConnectionGroupAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var targetCount = (int)callInfo[1]!;
                scaleUpApplied.TrySetResult(targetCount);
                return new ValueTask<int>(targetCount);
            });
        pool.ShrinkConnectionGroupAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                shrinkRequested.TrySetResult();
                return new ValueTask<IKafkaConnection?>(removedConnection);
            });

        var enqueued = 0;
        var succeededAcks = 0;
        var failedAcks = 0;
        var sender = CreateSender(pool, options, accumulator, (_, _, _, count, ex) =>
        {
            if (ex is null)
                Interlocked.Add(ref succeededAcks, count);
            else
                Interlocked.Add(ref failedAcks, count);
        });
        try
        {
            // Wave 1: enough backlog across more partitions than connections to build
            // send-loop pressure and trigger a scale-up.
            for (var i = 0; i < 512; i++)
            {
                sender.Enqueue(CreateTestBatch(vtPool, i % PartitionCount));
                enqueued++;
            }

            await scaleUpApplied.Task.WaitAsync(cancellationToken);

            // Trickle: keep the loop iterating with near-zero buffer utilization so the
            // sustained-low-utilization scale-down window elapses and a shrink fires.
            while (!shrinkRequested.Task.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sender.Enqueue(CreateTestBatch(vtPool, enqueued % PartitionCount));
                enqueued++;
                await Task.Delay(5, cancellationToken);
            }

            // Wave 2: continued traffic through and after the scale-down apply. Under the
            // pre-fix code the send loop is already dead at this point and every one of
            // these fails with ObjectDisposedException.
            for (var i = 0; i < 128; i++)
            {
                sender.Enqueue(CreateTestBatch(vtPool, i % PartitionCount));
                enqueued++;
                if (i % 16 == 0)
                    await Task.Delay(1, cancellationToken);
            }

            // Every accepted batch must complete successfully.
            while (Volatile.Read(ref succeededAcks) < enqueued && Volatile.Read(ref failedAcks) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            await Assert.That(Volatile.Read(ref failedAcks)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref succeededAcks)).IsEqualTo(enqueued);
            await Assert.That(sender.IsAlive).IsTrue();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }

        // The removed connection must be disposed exactly once (drain path or sender disposal),
        // never leaked.
        await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(1);
    }

    [Test]
    [Timeout(30_000)]
    public async Task RedeliverOrFailOnLoopExit_Shutdown_FailsWithObjectDisposed(CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var pool = Substitute.For<IConnectionPool>();

        Exception? ackException = null;
        var sender = CreateSender(pool, options, accumulator,
            (_, _, _, _, ex) => ackException = ex);

        try
        {
            var batch = CreateTestBatch(vtPool, partition: 0);
            sender.RedeliverOrFailOnLoopExit(batch, redeliver: false,
                new ObjectDisposedException(nameof(BrokerSender)));

            await Assert.That(ackException).IsTypeOf<ObjectDisposedException>();
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
    public async Task RedeliverOrFailOnLoopExit_UnexpectedExit_ReroutesBatchForRedelivery(CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var pool = Substitute.For<IConnectionPool>();

        Exception? ackException = null;
        ReadyBatch? rerouted = null;
        var sender = CreateSender(pool, options, accumulator,
            (_, _, _, _, ex) => ackException = ex,
            rerouteBatch: (b, _) => rerouted = b);

        try
        {
            var batch = CreateTestBatch(vtPool, partition: 0);
            var topicPartition = batch.TopicPartition;
            const long retryNotBefore = 123;
            batch.RetryNotBefore = retryNotBefore;
            sender.RedeliverOrFailOnLoopExit(batch, redeliver: true,
                new ObjectDisposedException(nameof(BrokerSender)));

            await Assert.That(rerouted).IsSameReferenceAs(batch);
            await Assert.That(ackException).IsNull();
            await Assert.That(batch.IsRetry).IsTrue();
            await Assert.That(batch.RetryNotBefore).IsEqualTo(retryNotBefore);
            await Assert.That(batch.DiagTrace).Contains("Y");
            await Assert.That(accumulator.IsMuted(topicPartition)).IsTrue();

            // The batch is still live — hand it back for cleanup so pooled sources
            // aren't leaked by the test.
            sender.RedeliverOrFailOnLoopExit(batch, redeliver: false,
                new ObjectDisposedException(nameof(BrokerSender)));
            await Assert.That(accumulator.IsMuted(topicPartition)).IsFalse();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }

    [Test]
    public async Task SharedInfrastructure_DoesNotShrinkConnectionGroup()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: null,
            canPhysicallyShrinkConnections: false);

        try
        {
            // This test drives MaybeScaleConnections directly. Stop and join the live
            // loop first so it cannot remain a second writer after passing its scaling gate.
            sender.RequestCancellation();
            await GetField<Task>(sender, "_sendLoopTask");
            SetField(sender, "_connectionCount", 2);

            // First pass starts low-utilization tracking; zero sustained window makes
            // the second pass eligible to shrink if shared-pool protection is absent.
            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            var connectionCount = GetField<int>(sender, "_connectionCount");

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(connectionCount).IsEqualTo(1);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_SaturatedInFlightCapacity_DoesNotShrink()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);

        try
        {
            SetField(sender, "_connectionCount", 2);
            SetField(sender, "_totalMaxInFlight", 2);
            SetField(sender, "_totalPendingResponseCount", 1);

            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(2);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_NonIdempotentPendingRequestOnRetainedConnection_DoesNotRemap()
    {
        var options = CreateOptions(
            idempotent: false,
            maxInFlightRequests: 100,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);

        try
        {
            SetField(sender, "_connectionCount", 2);
            SetField(sender, "_totalMaxInFlight", 200);
            SetField(sender, "_totalPendingResponseCount", 1);

            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(2)
                .Because("retained connection slots also remap partitions when routing width shrinks");
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_MutedPartitionWithQueuedBatch_DoesNotShrink()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var topicPartition = new TopicPartition(Topic, 0);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            SetField(sender, "_connectionCount", 4);
            SetField(sender, "_totalMaxInFlight", 4);
            SetField(sender, "_totalPendingResponseCount", 1);

            var partitionQueueBytes = GetField<ConcurrentDictionary<TopicPartition, long>>(
                accumulator,
                "_partitionQueueBytes");
            partitionQueueBytes[topicPartition] = 100;
            accumulator.Reenqueue(CreateTestBatch(valueTaskSourcePool, partition: 0), 0);

            typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sender, [topicPartition]);

            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(4);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_MutedPartitionWithOnlyInFlightBatch_DoesNotShrink()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var topicPartition = new TopicPartition(Topic, 0);

        try
        {
            SetField(sender, "_connectionCount", 4);
            SetField(sender, "_totalMaxInFlight", 4);
            SetField(sender, "_totalPendingResponseCount", 1);

            var partitionQueueBytes = GetField<ConcurrentDictionary<TopicPartition, long>>(
                accumulator,
                "_partitionQueueBytes");
            partitionQueueBytes[topicPartition] = 100;

            typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sender, [topicPartition]);

            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(4);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    public async Task ScaleDown_MuteOnSend_ActiveLoadDoesNotShrink(int activePartitionCount)
    {
        var options = CreateOptions(
            idempotent: false,
            maxInFlightRequests: 100,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);

        try
        {
            SetField(sender, "_connectionCount", 3);
            SetField(sender, "_totalMaxInFlight", 300);
            SetField(sender, "_totalPendingResponseCount", 1);
            var mutePartition = typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var partition = 0; partition < activePartitionCount; partition++)
                mutePartition.Invoke(sender, [new TopicPartition(Topic, partition)]);

            InvokeMaybeScaleConnections(sender);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_LoadBearingMutedActivityBetweenChecks_SurvivesMomentaryClear()
    {
        var options = CreateOptions(
            idempotent: false,
            maxInFlightRequests: 100,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 60_000);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);

        try
        {
            SetField(sender, "_connectionCount", 3);
            SetField(sender, "_totalMaxInFlight", 300);
            SetField(sender, "_totalPendingResponseCount", 1);
            SetField(sender, "_lowUtilizationStartTicks", 1L);
            var mutePartition = typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var partition = 0; partition < 3; partition++)
                mutePartition.Invoke(sender, [new TopicPartition(Topic, partition)]);

            // Simulate all partitions clearing between scale checks without calling
            // UnmutePartition, whose wake event lets the live sender loop race this
            // test's direct MaybeScaleConnections invocation.
            GetField<ConcurrentDictionary<TopicPartition, byte>>(sender, "_mutedPartitions").Clear();
            SetField(sender, "_mutedPartitionCount", 0);
            SetField(sender, "_totalPendingResponseCount", 0);
            SetField(sender, "_lowUtilizationStartTicks", 1L);
            InvokeMaybeScaleConnections(sender);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(3);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_MutedPartitionWithCarryOverBatch_DoesNotShrink()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var topicPartition = new TopicPartition(Topic, 0);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var carryOver = CreateCarryOver(CreateTestBatch(valueTaskSourcePool, partition: 0));

        try
        {
            SetField(sender, "_connectionCount", 4);
            SetField(sender, "_totalMaxInFlight", 4);
            SetField(sender, "_totalPendingResponseCount", 1);

            typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sender, [topicPartition]);

            InvokeMaybeScaleConnections(sender, carryOver);
            InvokeMaybeScaleConnections(sender, carryOver);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(4);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task ScaleDown_MutedPartitionWithUnreadChannelTail_DoesNotShrink()
    {
        var options = CreateOptions(
            idempotent: false,
            scaleCooldownMs: 0,
            scaleDownSustainedMs: 0);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var topicPartition = new TopicPartition(Topic, 0);

        try
        {
            SetField(sender, "_connectionCount", 4);
            SetField(sender, "_totalMaxInFlight", 4);
            SetField(sender, "_totalPendingResponseCount", 1);

            typeof(BrokerSender).GetMethod(
                "MutePartition",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(sender, [topicPartition]);

            InvokeMaybeScaleConnections(sender, hasUnreadEvent: true);
            InvokeMaybeScaleConnections(sender, hasUnreadEvent: true);

            await pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
            await Assert.That(GetField<int>(sender, "_connectionCount")).IsEqualTo(4);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    private static object CreateCarryOver(ReadyBatch? batch = null)
    {
        var carryOverType = typeof(BrokerSender).GetNestedType(
            "PartitionCarryOver",
            BindingFlags.NonPublic)!;
        var carryOver = Activator.CreateInstance(carryOverType)!;
        if (batch is null)
            return carryOver;

        var batchReferenceType = typeof(BrokerSender).GetNestedType(
            "BatchReference",
            BindingFlags.NonPublic)!;
        var batchReference = Activator.CreateInstance(
            batchReferenceType,
            [batch, batch.Generation])!;
        carryOverType.GetMethod("Add")!.Invoke(carryOver, [batchReference]);
        return carryOver;
    }

    private static void InvokeMaybeScaleConnections(
        BrokerSender sender,
        object? carryOver = null,
        bool hasUnreadEvent = false) =>
        typeof(BrokerSender).GetMethod(
            "MaybeScaleConnections",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sender, [carryOver ?? CreateCarryOver(), hasUnreadEvent]);

    private static T GetField<T>(object instance, string fieldName) =>
        (T)instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField<T>(object instance, string fieldName, T value) =>
        instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    [Test]
    [Arguments(false, false, true)]
    [Arguments(true, true, true)]
    [Arguments(true, false, false)]
    public async Task SingleSlot_ReconnectsThroughExpectedPoolPath(
        bool canPhysicallyShrinkConnections,
        bool scaleBeforeReconnect,
        bool expectsIndexedConnection)
    {
        var options = CreateOptions(idempotent: true);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var disconnectedConnection = Substitute.For<IKafkaConnection>();
        disconnectedConnection.IsConnected.Returns(false);
        var unindexedConnection = Substitute.For<IKafkaConnection>();
        var indexedConnection = Substitute.For<IKafkaConnection>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(unindexedConnection);
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(indexedConnection);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: null,
            canPhysicallyShrinkConnections: canPhysicallyShrinkConnections);

        try
        {
            if (scaleBeforeReconnect)
            {
                typeof(BrokerSender).GetMethod(
                    "ApplyScaleUp",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(sender, [2]);
                typeof(BrokerSender).GetField(
                    "_connectionCount",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(sender, 1);
            }

            var pinnedConnections = GetPinnedConnections(sender);
            pinnedConnections[0] = disconnectedConnection;

            using var connectionLease = await GetConnectionLeaseAtIndexAsync(sender, 0);
            var connection = connectionLease.Connection;

            await Assert.That(connection).IsSameReferenceAs(
                expectsIndexedConnection ? indexedConnection : unindexedConnection);

            if (expectsIndexedConnection)
            {
                await pool.DidNotReceive().GetConnectionAsync(
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>());
            }
            else
            {
                await pool.DidNotReceive().GetConnectionByIndexAsync(
                    Arg.Any<int>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>());
            }
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task GetConnectionLeaseAtIndexAsync_PinnedConnection_HoldsLeaseUntilDisposed()
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var connection = new TestKafkaConnection();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: null,
            canPhysicallyShrinkConnections: false);

        try
        {
            var pinnedConnections = GetPinnedConnections(sender);
            pinnedConnections[0] = connection;

            using (var connectionLease = await GetConnectionLeaseAtIndexAsync(sender, 0))
            {
                await Assert.That(connectionLease.Connection).IsSameReferenceAs(connection);
                await Assert.That(((IRetirableKafkaConnection)connection).LeaseCount).IsEqualTo(1);
            }

            await Assert.That(((IRetirableKafkaConnection)connection).LeaseCount).IsEqualTo(0);
            await pool.DidNotReceive().GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    [Test]
    public async Task GetConnectionLeaseAtIndexAsync_RetiredPinnedConnection_UsesCurrentPoolSlot()
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var retiredConnection = new TestKafkaConnection();
        var replacementConnection = new TestKafkaConnection();
        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(replacementConnection);
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: null,
            canPhysicallyShrinkConnections: false);

        try
        {
            var pinnedConnections = GetPinnedConnections(sender);
            pinnedConnections[0] = retiredConnection;
            ((IRetirableKafkaConnection)retiredConnection).BeginRetirement();

            using (var connectionLease = await GetConnectionLeaseAtIndexAsync(sender, 0))
            {
                await Assert.That(connectionLease.Connection).IsSameReferenceAs(replacementConnection);
                await Assert.That(((IRetirableKafkaConnection)replacementConnection).LeaseCount).IsEqualTo(1);
            }

            await Assert.That(pinnedConnections[0]).IsSameReferenceAs(replacementConnection);
            await Assert.That(((IRetirableKafkaConnection)retiredConnection).LeaseCount).IsEqualTo(0);
            await Assert.That(((IRetirableKafkaConnection)replacementConnection).LeaseCount).IsEqualTo(0);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task DisposeAsync_LateShrinkCompletion_DisposesRemovedConnection(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var shrinkCompletion = new TaskCompletionSource<IKafkaConnection?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var removedConnection = new TestKafkaConnection();

        typeof(BrokerSender).GetField(
            "_pendingShrinkTask",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, shrinkCompletion.Task);

        await sender.DisposeAsync();

        shrinkCompletion.SetResult(removedConnection);
        while (Volatile.Read(ref removedConnection.DisposeCalls) == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(1);
    }

    [Test]
    [NotInParallel]
    [Timeout(10_000)]
    public async Task DisposeAsync_CompletedShrinkResult_WaitsForLeasedConnection(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        typeof(BrokerSender).GetField(
            "_pendingShrinkTask",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, Task.FromResult<IKafkaConnection?>(removedConnection));

        await AssertDisposeWaitsForLeaseAsync(
            sender,
            removedConnection,
            retirableConnection,
            cancellationToken);
    }

    [Test]
    [NotInParallel]
    [Arguments("_drainingConnection")]
    [Arguments("_retiringConnection")]
    [Timeout(10_000)]
    public async Task DisposeAsync_ParkedConnection_WaitsForLeasedConnection(
        string connectionFieldName,
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        typeof(BrokerSender).GetField(
            connectionFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, removedConnection);

        await AssertDisposeWaitsForLeaseAsync(
            sender,
            removedConnection,
            retirableConnection,
            cancellationToken);
    }

    [Test]
    [NotInParallel]
    [Timeout(10_000)]
    public async Task DisposeAsync_InFlightRetirementDrain_WaitsForLeasedConnection(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        typeof(BrokerSender).GetField(
            "_drainingConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, removedConnection);

        typeof(BrokerSender).GetMethod(
            "MaybeDrainAndDisposeConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sender, null);

        await AssertDisposeWaitsForLeaseAsync(
            sender,
            removedConnection,
            retirableConnection,
            cancellationToken);
    }

    [Test]
    [NotInParallel]
    [Timeout(15_000)]
    public async Task DisposeAsync_SharedDrainTimeout_ContinuesWaitingForLease(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(
            pool,
            options,
            accumulator,
            onAcknowledgement: null,
            canPhysicallyShrinkConnections: false,
            disposalDrainTimeout: TimeSpan.FromMilliseconds(25));
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        typeof(BrokerSender).GetField(
            "_drainingConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, removedConnection);

        try
        {
            await sender.DisposeAsync().AsTask().WaitAsync(cancellationToken);

            await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(0);
            await Assert.That(Volatile.Read(ref removedConnection.CompleteRetirementCalls)).IsEqualTo(0);
        }
        finally
        {
            retirableConnection.ReleaseLease();
            await removedConnection.DisposeStarted.Task.WaitAsync(cancellationToken);
        }

        await Assert.That(Volatile.Read(ref removedConnection.CompleteRetirementCalls)).IsEqualTo(1);
        await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(1);
    }

    [Test]
    [NotInParallel]
    [Timeout(15_000)]
    public async Task DisposeAsync_OwnedDrain_WaitsForLeaseWithoutTimeout(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        await using var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        typeof(BrokerSender).GetField(
            "_drainingConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, removedConnection);

        var dispose = sender.DisposeAsync().AsTask();
        try
        {
            await removedConnection.LeaseCountObserved.Task.WaitAsync(cancellationToken);
            await Assert.That(dispose.IsCompleted).IsFalse();
            await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(0);
        }
        finally
        {
            retirableConnection.ReleaseLease();
            await dispose.WaitAsync(cancellationToken);
        }

        await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(1);
    }

    [Test]
    [Timeout(10_000)]
    public async Task ApplyScaleDown_CancelledSender_KeepsRetirementDrainAlive(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);
        var removedConnection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)removedConnection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        retirableConnection.BeginRetirement();

        var lifetimeCts = (CancellationTokenSource)typeof(BrokerSender).GetField(
            "_cts",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sender)!;
        lifetimeCts.Cancel();

        typeof(BrokerSender).GetMethod(
            "ApplyScaleDown",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sender, [removedConnection]);

        try
        {
            await removedConnection.LeaseCountObserved.Task.WaitAsync(cancellationToken);
            await Assert.That(removedConnection.DisposeStarted.Task.IsCompleted).IsFalse();
            await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(0);
        }
        finally
        {
            retirableConnection.ReleaseLease();
            await removedConnection.DisposeStarted.Task.WaitAsync(cancellationToken);
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }

        await Assert.That(Volatile.Read(ref removedConnection.CompleteRetirementCalls)).IsEqualTo(1);
        await Assert.That(Volatile.Read(ref removedConnection.DisposeCalls)).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyScaleDown_EmptyRemovedSlot_DisposesConnectionImmediately()
    {
        var options = CreateOptions(idempotent: false);
        var accumulator = new RecordAccumulator(options);
        var pool = Substitute.For<IConnectionPool>();
        var disposeThreadId = 0;
        var removedConnection = Substitute.For<IKafkaConnection>();
        removedConnection.DisposeAsync().Returns(_ =>
        {
            Interlocked.CompareExchange(
                ref disposeThreadId,
                Environment.CurrentManagedThreadId,
                comparand: 0);
            return ValueTask.CompletedTask;
        });
        var sender = CreateSender(pool, options, accumulator, onAcknowledgement: null);

        try
        {
            var applyScaleDown = typeof(BrokerSender).GetMethod(
                "ApplyScaleDown",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            var applyThreadId = Environment.CurrentManagedThreadId;
            applyScaleDown.Invoke(sender, [removedConnection]);

            await Assert.That(Volatile.Read(ref disposeThreadId)).IsEqualTo(applyThreadId);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task RedeliverOrFailOnLoopExit_PastDeliveryDeadline_FailsWithTimeout(CancellationToken cancellationToken)
    {
        var options = CreateOptions(idempotent: false, deliveryTimeoutMs: 1);
        var accumulator = new RecordAccumulator(options);
        var vtPool = new ValueTaskSourcePool<RecordMetadata>();
        var pool = Substitute.For<IConnectionPool>();

        Exception? ackException = null;
        var reroutedCount = 0;
        var sender = CreateSender(pool, options, accumulator,
            (_, _, _, _, ex) => ackException = ex,
            rerouteBatch: (_, _) => reroutedCount++);

        try
        {
            var batch = CreateTestBatch(vtPool, partition: 0);
            await Task.Delay(20, cancellationToken); // Let the 1ms delivery deadline pass

            sender.RedeliverOrFailOnLoopExit(batch, redeliver: true,
                new ObjectDisposedException(nameof(BrokerSender)));

            await Assert.That(reroutedCount).IsEqualTo(0);
            await Assert.That(ackException).IsTypeOf<KafkaTimeoutException>();
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await vtPool.DisposeAsync();
        }
    }
}
