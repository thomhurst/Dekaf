using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the app-limited linger bypass (#2510): a serially-awaited produce that is
/// provably the only demand in the producer seals and dispatches immediately instead of
/// waiting the awaited linger window (min(2ms, LingerMs/2)). Every guard condition gets a
/// negative test proving batching is untouched the moment any concurrent demand exists.
/// </summary>
public class AppLimitedLingerBypassTests
{
    private const string Topic = "bypass-topic";

    private static ProducerOptions CreateOptions(int lingerMs = 5_000) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "test-producer",
        BufferMemory = ulong.MaxValue,
        BatchSize = 1_000_000,
        LingerMs = lingerMs,
        EnableIdempotence = false
    };

    private static bool AppendAwaited(
        RecordAccumulator accumulator,
        ValueTaskSourcePool<RecordMetadata> pool,
        int partition = 0)
        => AccumulatorTestHelpers.AppendAwaitedNullRecord(accumulator, pool, Topic, partition);

    [Test]
    public async Task SoleAwaitedProduce_SealsImmediately_WithoutWaitingLinger()
    {
        // A huge linger makes the outcome unambiguous: only the bypass can seal this batch
        // within the lifetime of the test.
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 5_000));
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var appended = AppendAwaited(accumulator, pool);

            await Assert.That(appended).IsTrue();
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FireAndForgetAppend_DoesNotSealImmediately()
    {
        var accumulator = new RecordAccumulator(CreateOptions());

        try
        {
            var appended = await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic);

            await Assert.That(appended).IsTrue();
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task BulkProduceScope_SuppressesImmediateSeal()
    {
        var accumulator = new RecordAccumulator(CreateOptions());
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            using (accumulator.EnterBulkProduceScope())
            {
                AppendAwaited(accumulator, pool);
                await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
            }

            // Once the bulk scope exits, a fresh sole awaited produce would bypass again,
            // but the batch left open by the bulk append keeps the unsealed count at 1,
            // so a second awaited append to the same partition coalesces instead.
            AppendAwaited(accumulator, pool);
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentUnsealedAwaitedBatch_SuppressesImmediateSeal()
    {
        var accumulator = new RecordAccumulator(CreateOptions());
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            // Leave an unsealed awaited batch on partition 0 (bulk scope keeps it open).
            using (accumulator.EnterBulkProduceScope())
            {
                AppendAwaited(accumulator, pool, partition: 0);
            }

            // A second awaited produce on partition 1 is NOT app-limited: another awaited
            // batch exists that a produce request could carry alongside this one.
            AppendAwaited(accumulator, pool, partition: 1);

            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(2);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task InFlightBatch_SuppressesImmediateSeal()
    {
        var accumulator = new RecordAccumulator(CreateOptions());
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            // First sole awaited produce bypasses and enters the pipeline (in-flight = 1).
            AppendAwaited(accumulator, pool, partition: 0);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1);

            // With a batch still in flight the producer is not app-limited (nothing has
            // awaited that delivery yet), so the next awaited append must keep batching.
            AppendAwaited(accumulator, pool, partition: 1);

            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    /// <summary>
    /// End-to-end regression for the stress lane shape (#2510): serial awaited transactional
    /// produce at LingerMs=5 must seal at append on every cycle — including cycles where the
    /// previous batch was delivered and retired the way BrokerSender does. On failure the
    /// message dumps every gate input so the blocking condition is visible.
    /// Not parallel: full-producer tests emit thread-pool continuations that can land on
    /// the allocation gate's measuring thread (see TransactionalProduceAllocationTests).
    /// </summary>
    [Test, NotInParallel]
    public async Task TransactionalSerialProduce_AppLimited_SealsAtAppend()
    {
        await using var producer = new KafkaProducer<string, string>(
            CreateTransactionalOptions(),
            Serializers.String,
            Serializers.String);
        await producer.StopSenderLoopsForTestingAsync();
        SeedProducerMetadata(producer);
        InitializeTransactionalState(producer);

        var transaction = producer.BeginTransaction();
        var accumulator = producer.RecordAccumulator;
        var topicPartition = new TopicPartition(TxnTopic, 0);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var produce = transaction.ProduceAsync(TxnTopic, "key", "value", CancellationToken.None);

            if (accumulator.UnsealedBatchCount != 0)
            {
                var pendingAwaited = AccumulatorTestHelpers.GetPrivateField<int>(accumulator, "_pendingAwaitedProduceCount");
                var bulkScope = AccumulatorTestHelpers.GetPrivateField<int>(accumulator, "_bulkProduceScopeCount");
                Assert.Fail(
                    $"cycle {cycle}: batch not sealed at append — unsealed={accumulator.UnsealedBatchCount}, " +
                    $"inFlight={accumulator.InFlightBatchCount}, pendingAwaited={pendingAwaited}, bulkScope={bulkScope}");
            }

            await Assert.That(accumulator.TryDrainBatch(topicPartition, out var batch)).IsTrue();
            LeakGateHarness.RetireBatch(accumulator, batch!, offset: cycle * 10);
            var metadata = await produce;
            await Assert.That(metadata.Partition).IsEqualTo(0);
        }

        // Never committed (no broker); return to Ready so DisposeAsync skips the coordinator abort.
        producer._transactionState = TransactionState.Ready;
    }

    private const string TxnTopic = "bypass-txn-topic";

    private static ProducerOptions CreateTransactionalOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "bypass-txn",
        TransactionalId = "bypass-txn",
        EnableIdempotence = true,
        Acks = Acks.All,
        BufferMemory = 32UL * 1024 * 1024,
        BatchSize = 1_048_576,
        LingerMs = 5,
        RequestTimeoutMs = 500,
        DeliveryTimeoutMs = 1_000,
        CloseTimeoutMs = 1_000,
        // The test host installs a process-wide sample-everything ActivityListener; the stress
        // lane this mirrors runs without listeners — pin that branch.
        SkipProduceActivityListenerCheckForTesting = true,
    };

    private static void InitializeTransactionalState(KafkaProducer<string, string> producer)
    {
        AccumulatorTestHelpers.SetPrivateField(producer, "_initialized", true);
        var metadataManager = AccumulatorTestHelpers.GetPrivateField<MetadataManager>(producer, "_metadataManager");
        metadataManager.ObserveClusterCapabilities(
            "bypass-txn-cluster",
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [],
                FinalizedFeaturesEpoch = 1,
                FinalizedFeatures =
                [
                    new FinalizedFeature("transaction.version", 2, 2)
                ]
            }));
        producer._transactionState = TransactionState.Ready;
    }

    private static void SeedProducerMetadata(KafkaProducer<string, string> producer)
    {
        var metadataManager = AccumulatorTestHelpers.GetPrivateField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 },
            ],
            ClusterId = "bypass-txn-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = TxnTopic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0],
                        },
                    ],
                },
            ],
        });
    }

    [Test]
    public async Task SecondRecordInBatch_DoesNotRetriggerSeal()
    {
        var accumulator = new RecordAccumulator(CreateOptions());
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            // Fire-and-forget record opens a batch; the awaited record that lands second is
            // not the sole record, so sealing it early would ship the FnF record early too.
            await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic);
            AppendAwaited(accumulator, pool);

            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    // ── SealedAsSoleDemand: set only by the bypass seal, cleared across pooled reuse ──
    //
    // BrokerSender skips its wave-coalesce spin for a single-batch wave carrying the flag,
    // so the flag must be a faithful record of the bypass proof: every other seal path
    // (zero-linger, flush, bulk scope) must leave it false, and neither pooled type
    // (PartitionBatch, ReadyBatch) may carry it into its next lifecycle.

    [Test]
    public async Task SoleAwaitedProduce_SealsBatchAsSoleDemand()
    {
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 5_000));
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            AppendAwaited(accumulator, pool);

            var batch = DrainSealedBatch(accumulator);
            await Assert.That(batch.SealedAsSoleDemand).IsTrue();
            LeakGateHarness.RetireBatch(accumulator, batch, offset: 0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ZeroLingerSoleAwaitedProduce_IsNotSealedAsSoleDemand()
    {
        // LingerMs == 0 seals on the zero-linger path, which never evaluates the app-limited
        // gate — the sender keeps its (75 µs) sibling wait for that shape.
        var accumulator = new RecordAccumulator(CreateOptions(lingerMs: 0));
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            AppendAwaited(accumulator, pool);
            await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(0);

            var batch = DrainSealedBatch(accumulator);
            await Assert.That(batch.SealedAsSoleDemand).IsFalse();
            LeakGateHarness.RetireBatch(accumulator, batch, offset: 0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushSealOfFireAndForgetBatch_IsNotSealedAsSoleDemand()
    {
        var accumulator = new RecordAccumulator(CreateOptions());

        try
        {
            await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic);
            await AccumulatorTestHelpers.SealAllAsync(accumulator);

            var batch = DrainSealedBatch(accumulator);
            await Assert.That(batch.SealedAsSoleDemand).IsFalse();
            LeakGateHarness.RetireBatch(accumulator, batch, offset: 0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task SoleDemandFlag_DoesNotSurvivePooledBatchReuse()
    {
        var accumulator = new RecordAccumulator(CreateOptions());
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            // Cycle 1: the bypass seals and flags the batch; retiring it returns both the
            // PartitionBatch and the ReadyBatch to their pools with the flag still set.
            AppendAwaited(accumulator, pool);
            var flagged = DrainSealedBatch(accumulator);
            await Assert.That(flagged.SealedAsSoleDemand).IsTrue();
            LeakGateHarness.RetireBatch(accumulator, flagged, offset: 0);

            // Cycle 2: a bulk-scope append re-rents the pooled batch and the flush sweep seals
            // it without any sole-demand proof — the recycled objects must read false.
            using (accumulator.EnterBulkProduceScope())
            {
                AppendAwaited(accumulator, pool);
                await Assert.That(accumulator.UnsealedBatchCount).IsEqualTo(1);
                await AccumulatorTestHelpers.SealAllAsync(accumulator);
            }

            var recycled = DrainSealedBatch(accumulator);
            await Assert.That(recycled.SealedAsSoleDemand).IsFalse();
            LeakGateHarness.RetireBatch(accumulator, recycled, offset: 1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    private static ReadyBatch DrainSealedBatch(RecordAccumulator accumulator)
    {
        if (!accumulator.TryDrainBatch(new TopicPartition(Topic, 0), out var batch))
            throw new InvalidOperationException("Expected a sealed batch in the partition deque.");

        return batch;
    }
}
