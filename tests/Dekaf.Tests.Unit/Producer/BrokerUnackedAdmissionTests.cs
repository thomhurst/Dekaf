using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the per-broker logical-byte admission window: reserving before append,
/// transferring ownership at seal/reroute, terminal release, and escape hatches.
/// </summary>
public sealed class BrokerUnackedAdmissionTests
{
    private const int LeaderNodeId = 1;

    private static ProducerOptions CreateOptions(
        long? capOverride,
        int deliveryLatencyTargetMs = 10,
        int maxBlockMs = 60_000,
        int batchSize = 50,
        int connectionsPerBroker = 1,
        Acks acks = Acks.Leader) => new()
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = batchSize,
            LingerMs = 60_000, // No linger-driven seals: rotation on full batches only.
            MaxBlockMs = maxBlockMs,
            DeliveryLatencyTargetMs = deliveryLatencyTargetMs,
            UnackedByteBudgetCapOverride = capOverride,
            ConnectionsPerBroker = connectionsPerBroker,
            Acks = acks
        };

    [Test]
    public async Task ColdStartAdmission_UsesOneBatchPerConfiguredConnection()
    {
        const int batchSize = 1_024;
        const int connectionsPerBroker = 3;
        var options = CreateOptions(
            capOverride: null,
            batchSize: batchSize,
            connectionsPerBroker: connectionsPerBroker);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) => LeaderNodeId);

        var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;

        await Assert.That(budget.BudgetBytes).IsEqualTo(batchSize * connectionsPerBroker);
    }

    [Test]
    public async Task ColdStartAdmission_ExplicitCapOverrideKeepsControllerWindow()
    {
        const int capOverride = 10_000;
        var options = CreateOptions(
            capOverride,
            batchSize: 1_024,
            connectionsPerBroker: 3);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) => LeaderNodeId);

        var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;

        await Assert.That(budget.BudgetBytes).IsEqualTo(capOverride);
    }

    [Test]
    public async Task ColdStartAdmission_AcksNoneKeepsControllerWindow()
    {
        const int batchSize = 1_024;
        var options = CreateOptions(
            capOverride: null,
            batchSize: batchSize,
            acks: Acks.None);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) => LeaderNodeId);

        var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;

        await Assert.That(budget.BudgetBytes).IsEqualTo(batchSize * 16);
    }

    /// <summary>
    /// Appends empty-payload records until at least one batch has been sealed (batch rotation
    /// with the small BatchSize). When <paramref name="stopOnceCharged"/> is set (tiny-cap
    /// tests), the loop stops as soon as the broker budget is charged — one more append would
    /// be gated and never complete.
    /// </summary>
    private static async Task AppendUntilSealedAsync(
        RecordAccumulator accumulator, string topic, BrokerUnackedByteBudget? stopOnceCharged = null)
    {
        for (var i = 0; i < 10; i++)
        {
            if (stopOnceCharged?.UnackedBytes > 0)
            {
                await SealAllAsync(accumulator);
                return;
            }

            var appended = await accumulator.AppendAsync(
                topic, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, CancellationToken.None);
            if (!appended)
                throw new InvalidOperationException("Append failed before any batch sealed.");
        }

        if (stopOnceCharged is { UnackedBytes: 0 })
            throw new InvalidOperationException("No batch sealed after 10 appends.");
    }

    private static List<ReadyBatch> DrainSealedBatches(RecordAccumulator accumulator, MetadataManager metadataManager)
    {
        // Drain takes at most one batch per partition per pass (Java parity) — loop until dry.
        var drained = new List<ReadyBatch>();
        while (true)
        {
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            accumulator.Drain(metadataManager, [LeaderNodeId], int.MaxValue, drainResult, new Stack<List<ReadyBatch>>());
            if (!drainResult.TryGetValue(LeaderNodeId, out var batches) || batches.Count == 0)
                return drained;
            drained.AddRange(batches);
        }
    }

    private static void ExitAndReturn(RecordAccumulator accumulator, ReadyBatch batch)
    {
        accumulator.OnBatchExitsPipeline(batch);
        batch.CompleteSend(0, DateTimeOffset.UtcNow);
        accumulator.ReturnReadyBatch(batch);
    }

    private static async ValueTask SealAllAsync(RecordAccumulator accumulator)
    {
        var method = typeof(RecordAccumulator).GetMethod(
            "SealBatchesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var seal = (ValueTask)method!.Invoke(accumulator, [true, CancellationToken.None])!;
        await seal;
    }

    [Test]
    public async Task Seal_ChargesLeaderBudget_AndBatchExitReleases()
    {
        // Keep enough initial controller credit to cover multiple sealed batches so this test
        // protects cumulative accounting instead of stopping after the first cold-start seal.
        var options = CreateOptions(capOverride: 10_000);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, LeaderNodeId);

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic");
            await SealAllAsync(accumulator);

            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId);
            await Assert.That(budget).IsNotNull();
            await Assert.That(budget!.UnackedBytes).IsGreaterThan(0);

            var batches = DrainSealedBatches(accumulator, metadataManager);
            await Assert.That(batches.Count).IsGreaterThanOrEqualTo(2);

            var chargedBytes = batches.Sum(b => (long)b.DataSize);
            await Assert.That(budget.UnackedBytes).IsEqualTo(chargedBytes);

            foreach (var batch in batches)
                ExitAndReturn(accumulator, batch);

            await Assert.That(budget.UnackedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task FastPathAppends_AreBlocked_WhileBrokerOverBudget()
    {
        // Cap of 1 byte: the first sealed batch puts the broker over budget.
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await AppendUntilSealedAsync(accumulator, "test-topic", stopOnceCharged: budget);
            await Assert.That(budget.IsOverBudget()).IsTrue();

            var completion = pool.Rent();
            var accepted = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, completion);

            await Assert.That(accepted).IsFalse();
            await Assert.That(budget.AdmissionBlockEvents).IsGreaterThan(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task AsyncAppend_Unblocks_WhenChargedBatchExits(CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, LeaderNodeId);

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await AppendUntilSealedAsync(accumulator, "test-topic", stopOnceCharged: budget);
            await Assert.That(budget.IsOverBudget()).IsTrue();

            // Gated append parks in the pending-append queue instead of completing.
            var gatedAppend = accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);
            await Assert.That(gatedAppend.IsCompleted).IsFalse();

            // Acking (exiting) the charged batches reopens the gate and serves the queue.
            foreach (var batch in DrainSealedBatches(accumulator, metadataManager))
                ExitAndReturn(accumulator, batch);

            var appended = await gatedAppend;
            await Assert.That(appended).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    [Timeout(10_000)]
    public async Task AsyncAppend_Unblocks_WhenWindowBytesAreReleased(CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 2_000_000);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) => LeaderNodeId);
        var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
        var nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        budget.OnAcked(
            ackedBytes: 1_000_000,
            budget.SnapshotDelivery(
                nowTicks - System.Diagnostics.Stopwatch.Frequency / 100, appLimited: true),
            nowTicks);
        budget.CompleteAckedPass(nowTicks);
        budget.Charge(1_200_000);

        var gatedAppend = accumulator.AppendAsync(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);

        await Assert.That(gatedAppend.IsCompleted).IsFalse();
        budget.Release(1_200_000);
        accumulator.DrainPendingAppends();
        await Assert.That(await gatedAppend).IsTrue();
        await Assert.That(budget.UnackedBytes).IsGreaterThan(0);
    }

    [Test]
    [Timeout(10_000)]
    public async Task AsyncAppend_SiblingUnblocks_WhenFirstWaiterIsCancelled(
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 2_000_000);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) => LeaderNodeId);
        using var firstCancellation = new CancellationTokenSource();
        var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
        var nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        budget.OnAcked(
            ackedBytes: 1_000_000,
            budget.SnapshotDelivery(
                nowTicks - System.Diagnostics.Stopwatch.Frequency, appLimited: true),
            nowTicks);
        budget.CompleteAckedPass(nowTicks);
        budget.Charge(1_200_000);

        var firstAppend = accumulator.AppendAsync(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null, PooledMemory.Null, null, 0, null, null, firstCancellation.Token);
        var siblingAppend = accumulator.AppendAsync(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);

        await Assert.That(firstAppend.IsCompleted).IsFalse();
        await Assert.That(siblingAppend.IsCompleted).IsFalse();
        firstCancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await firstAppend);
        budget.Release(1_200_000);
        accumulator.DrainPendingAppends();
        await Assert.That(await siblingAppend).IsTrue();
        await Assert.That(budget.UnackedBytes).IsGreaterThan(0);
    }

    [Test]
    [Timeout(30_000)]
    public async Task QueuedAppend_PreventsLaterFastPathBypass(CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, LeaderNodeId);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await AppendUntilSealedAsync(accumulator, "test-topic", stopOnceCharged: budget);

            var first = accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);
            await Assert.That(first.IsCompleted).IsFalse();

            // Existing partition backlog must not hide continued gate pressure from
            // adaptive scale-down. Every blocked admission records another event.
            var blockEvents = budget.AdmissionBlockEvents;
            var blockedCompletion = pool.Rent();
            var admittedWhileBlocked = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, blockedCompletion);
            await Assert.That(admittedWhileBlocked).IsFalse();
            await Assert.That(budget.AdmissionBlockEvents).IsGreaterThan(blockEvents);
            pool.Return(blockedCompletion);

            // Model the race where an ack reopens the gate before its drain call wins.
            // A later hot-path append must not leapfrog the already queued record.
            var chargedBytes = budget.UnackedBytes;
            budget.Release(chargedBytes);
            var completion = pool.Rent();
            var bypassed = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, completion);
            await Assert.That(bypassed).IsFalse();
            pool.Return(completion);

            // Restore the real batch charge, then exit it through production cleanup. Its
            // release drains the original append without double-releasing the budget.
            budget.Charge(chargedBytes);
            foreach (var batch in DrainSealedBatches(accumulator, metadataManager))
                ExitAndReturn(accumulator, batch);

            await Assert.That(await first).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task BlockedBroker_DoesNotStallHealthyBroker(CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options,
            resolveLeaderId: (_, partition) => partition == 0 ? 1 : 2);
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, LeaderNodeId);

        try
        {
            var blockedBudget = accumulator.GetBrokerUnackedBudget(1)!;
            await AppendUntilSealedAsync(accumulator, "test-topic", stopOnceCharged: blockedBudget);

            var blockedAppend = accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);
            await Assert.That(blockedAppend.IsCompleted).IsFalse();

            var healthyAppend = accumulator.AppendAsync(
                "test-topic", 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cancellationToken);
            await Assert.That(await healthyAppend).IsTrue();
            await Assert.That(blockedAppend.IsCompleted).IsFalse();

            foreach (var batch in DrainSealedBatches(accumulator, metadataManager))
                ExitAndReturn(accumulator, batch);
            await Assert.That(await blockedAppend).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task AsyncAppend_TimesOutWithMaxBlock_WhenGateStaysClosed()
    {
        // Generous MaxBlockMs for CI thread-pool starvation, same rationale as MaxBlockMsTests.
        var options = CreateOptions(capOverride: 1, maxBlockMs: 2_000);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic",
                stopOnceCharged: accumulator.GetBrokerUnackedBudget(LeaderNodeId));

            var ex = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
                await accumulator.AppendAsync(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PooledMemory.Null, PooledMemory.Null, null, 0, null, null, CancellationToken.None));

            await Assert.That(ex!.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task AsyncAppend_HonorsCancellation_WhileGated(CancellationToken cancellationToken)
    {
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic",
                stopOnceCharged: accumulator.GetBrokerUnackedBudget(LeaderNodeId));

            var gatedAppend = accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, cts.Token);
            await Assert.That(gatedAppend.IsCompleted).IsFalse();

            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await gatedAppend);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task Gate_IsDisabled_WhenDeliveryLatencyTargetIsZero()
    {
        var options = CreateOptions(capOverride: 1, deliveryLatencyTargetMs: 0);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic");

            await Assert.That(accumulator.GetBrokerUnackedBudget(LeaderNodeId)).IsNull();

            var completion = pool.Rent();
            var accepted = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, completion);
            await Assert.That(accepted).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Gate_NeverBinds_WhenLeaderIsUnknown()
    {
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => -1);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic");

            // Sealed batches were not charged: the leader could not be resolved.
            var budget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            await Assert.That(budget.UnackedBytes).IsEqualTo(0);

            var completion = pool.Rent();
            var accepted = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, completion);
            await Assert.That(accepted).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Appends_ReuseCachedLeaderBudget_UntilRoutingBoundary()
    {
        var options = CreateOptions(capOverride: null, batchSize: 16_384);
        var resolveCount = 0;
        var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: (_, _) =>
            {
                Interlocked.Increment(ref resolveCount);
                return LeaderNodeId;
            });

        try
        {
            for (var i = 0; i < 10; i++)
            {
                var appended = await accumulator.AppendAsync(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PooledMemory.Null, PooledMemory.Null, null, 0, null, null, CancellationToken.None);
                await Assert.That(appended).IsTrue();
            }

            await Assert.That(resolveCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task LeaderChange_ChargesSubsequentSeals_ToNewBrokerBudget()
    {
        var options = CreateOptions(capOverride: 10_000);
        var currentLeader = 1;
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => currentLeader);

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic");
            await SealAllAsync(accumulator);
            var broker1 = accumulator.GetBrokerUnackedBudget(1)!;
            var broker1BytesAfterFirstSeals = broker1.UnackedBytes;
            await Assert.That(broker1BytesAfterFirstSeals).IsGreaterThan(0);

            currentLeader = 2;
            await AppendUntilSealedAsync(accumulator, "test-topic");
            await SealAllAsync(accumulator);

            // New seals charge broker 2; broker 1 keeps only its pre-move charges.
            var broker2 = accumulator.GetBrokerUnackedBudget(2)!;
            await Assert.That(broker2.UnackedBytes).IsGreaterThan(0);
            await Assert.That(broker1.UnackedBytes).IsEqualTo(broker1BytesAfterFirstSeals);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task LeaderChange_BeforeFlush_ChargesNewBrokerBudget()
    {
        var options = CreateOptions(capOverride: null, batchSize: 16_384);
        var currentLeader = 1;
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => currentLeader);

        try
        {
            var appended = await accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, CancellationToken.None);
            await Assert.That(appended).IsTrue();

            var broker1 = accumulator.GetBrokerUnackedBudget(1)!;
            await Assert.That(broker1.UnackedBytes).IsGreaterThan(0);

            currentLeader = 2;
            await SealAllAsync(accumulator);

            var broker2 = accumulator.GetBrokerUnackedBudget(2)!;
            await Assert.That(broker1.UnackedBytes).IsEqualTo(0);
            await Assert.That(broker2.UnackedBytes).IsGreaterThan(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task LeaderChange_BeforeDrain_TransfersChargeToSelectedBroker()
    {
        var options = CreateOptions(capOverride: null, batchSize: 16_384);
        var currentLeader = 1;
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => currentLeader);
        var originalMetadata = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, nodeId: 1);
        var newMetadata = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, nodeId: 2);

        try
        {
            var appended = await accumulator.AppendAsync(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, null, null, CancellationToken.None);
            await Assert.That(appended).IsTrue();
            await SealAllAsync(accumulator);

            var broker1 = accumulator.GetBrokerUnackedBudget(1)!;
            var batchBytes = broker1.UnackedBytes;
            await Assert.That(batchBytes).IsGreaterThan(0);

            var readyNodes = new HashSet<int>();
            accumulator.Ready(originalMetadata, readyNodes);
            await Assert.That(readyNodes).Contains(1);

            currentLeader = 2;
            var drainResult = new Dictionary<int, List<ReadyBatch>>();
            accumulator.Drain(newMetadata, readyNodes, int.MaxValue, drainResult, new Stack<List<ReadyBatch>>());
            await Assert.That(drainResult).IsEmpty();

            readyNodes.Clear();
            accumulator.Ready(newMetadata, readyNodes);
            accumulator.Drain(newMetadata, readyNodes, int.MaxValue, drainResult, new Stack<List<ReadyBatch>>());

            var batch = drainResult[2].Single();
            var broker2 = accumulator.GetBrokerUnackedBudget(2)!;
            await Assert.That(broker1.UnackedBytes).IsEqualTo(0);
            await Assert.That(broker2.UnackedBytes).IsEqualTo(batchBytes);

            ExitAndReturn(accumulator, batch);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await originalMetadata.DisposeAsync();
            await newMetadata.DisposeAsync();
        }
    }

    [Test]
    public async Task Reroute_TransfersExistingBatchCharge_ToNewBrokerBudget()
    {
        var options = CreateOptions(capOverride: 10_000);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => LeaderNodeId);
        var metadataManager = AccumulatorTestHelpers.CreateMetadataManager("test-topic", 1, LeaderNodeId);

        try
        {
            await AppendUntilSealedAsync(accumulator, "test-topic");
            await SealAllAsync(accumulator);
            var batches = DrainSealedBatches(accumulator, metadataManager);
            var batch = batches[0];
            var originalBudget = accumulator.GetBrokerUnackedBudget(LeaderNodeId)!;
            var newBudget = accumulator.GetBrokerUnackedBudget(2)!;
            var batchBytes = batch.DataSize;
            var originalBytes = originalBudget.UnackedBytes;

            accumulator.ReattributeUnackedBudget(batch, brokerId: 2);

            await Assert.That(originalBudget.UnackedBytes).IsEqualTo(originalBytes - batchBytes);
            await Assert.That(newBudget.UnackedBytes).IsEqualTo(batchBytes);

            foreach (var drainedBatch in batches)
                ExitAndReturn(accumulator, drainedBatch);

            await Assert.That(originalBudget.UnackedBytes).IsEqualTo(0);
            await Assert.That(newBudget.UnackedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task LeaderChange_DoesNotBlockOnPreviousLeaderBudget()
    {
        var currentLeader = 1;
        var options = CreateOptions(capOverride: 1);
        var accumulator = new RecordAccumulator(options, resolveLeaderId: (_, _) => currentLeader);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var oldBudget = accumulator.GetBrokerUnackedBudget(1)!;
            await AppendUntilSealedAsync(accumulator, "test-topic", stopOnceCharged: oldBudget);
            await Assert.That(oldBudget.IsOverBudget()).IsTrue();

            currentLeader = 2;
            var completion = pool.Rent();
            var accepted = accumulator.TryAppendWithCompletion(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null, PooledMemory.Null, null, 0, completion);

            await Assert.That(accepted).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }
}
