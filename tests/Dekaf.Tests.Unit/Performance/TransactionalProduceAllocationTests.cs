using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.Producer;

namespace Dekaf.Tests.Unit.Performance;

/// <summary>
/// Broker-free allocation regression gate for the transactional produce caller path (issue #2471).
/// </summary>
/// <remarks>
/// <para>The transactional EOS stress lane runs serial-awaited produces at ~1 message per batch,
/// so every per-batch cost lands per message. This gate drives the full caller-side cycle at that
/// exact shape — componentwise <c>ITransaction.ProduceAsync(topic, key, value)</c> → serialize →
/// arena append → linger seal → drain → sender-style retirement → pooled completion await — on a
/// single thread, and asserts the steady state allocates zero bytes per cycle after warmup.</para>
/// <para>Out of scope (needs a socket): BrokerSender's send loop, KafkaConnection write/receive,
/// and the EndTxn commit path. Those are pooled per-request paths measured by the stress lane's
/// Memory &amp; GC table and by <c>TransactionalProduceAllocationBenchmarks</c>.</para>
/// </remarks>
[NotInParallel]
public class TransactionalProduceAllocationTests
{
    private const string Topic = "txn-allocation-gate";
    private const int WarmupIterations = 128;
    private const int MeasuredIterations = 1_024;

    /// <summary>
    /// A produce cycle's steady state must allocate exactly zero bytes. The only tolerated
    /// nonzero cycles are the rare, bursty <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/>
    /// segment allocations from the accumulator's linger/ready partition queues — production
    /// enqueues those per sealed batch too, so the growth is a real (amortized ~40 B/batch)
    /// per-batch cost, but it surfaces as one segment per few hundred batches, not per cycle.
    /// Any per-message or deterministic per-batch allocation makes every cycle nonzero and
    /// fails the burst-count gate immediately.
    /// </summary>
    private const int MaxQueueSegmentGrowthBursts = 24;
    private const long MaxTotalAllocatedBytes = 256 * 1024;

    [Test]
    public async Task TransactionalProduceAsync_ComponentwiseAwaitedCycle_AllocatesZeroBytes()
    {
        await using var producer = new KafkaProducer<string, string>(
            CreateTransactionalProducerOptions(),
            Serializers.String,
            Serializers.String);
        await producer.StopSenderLoopsForTestingAsync();
        SeedProducerMetadata(producer);
        InitializeTransactionalState(producer);

        var transaction = producer.BeginTransaction();
        var accumulator = producer.RecordAccumulator;
        var topicPartition = new TopicPartition(Topic, 0);
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var offset = 0L;

        // The closure display class is allocated once here, before the measured window.
        var measurement = WarmAndMeasure(
            () => ProduceDrainAwaitOne(transaction, accumulator, topicPartition, offset++, cancellationToken),
            WarmupIterations,
            MeasuredIterations);

        // The transaction is never committed (there is no broker); return the producer to
        // Ready so DisposeAsync does not attempt a coordinator abort.
        producer._transactionState = TransactionState.Ready;

        await Assert.That(measurement.Checksum).IsEqualTo(WarmupIterations + MeasuredIterations);
        await Assert.That(measurement.NonZeroCycles).IsLessThanOrEqualTo(MaxQueueSegmentGrowthBursts);
        await Assert.That(measurement.AllocatedBytes).IsLessThanOrEqualTo(MaxTotalAllocatedBytes);
    }

    /// <summary>
    /// One serial-awaited transactional produce cycle at the EOS stress shape (1 message per
    /// batch): produce, seal via the linger sweep, drain, retire the batch the way BrokerSender
    /// does, then await the caller's completion. Everything runs on the measuring thread so the
    /// awaited ValueTask is already completed when observed (no thread-pool dispatch).
    /// </summary>
    private static int ProduceDrainAwaitOne(
        ITransaction<string, string> transaction,
        RecordAccumulator accumulator,
        TopicPartition topicPartition,
        long offset,
        CancellationToken cancellationToken)
    {
        var produce = transaction.ProduceAsync(Topic, "allocation-key", "allocation-value", cancellationToken);

        // LingerMs = 0: the sweep seals the just-appended batch immediately.
        var seal = accumulator.ExpireLingerAsync(CancellationToken.None);
        if (!seal.IsCompletedSuccessfully)
            return 0;
        seal.GetAwaiter().GetResult();

        if (!accumulator.TryDrainBatch(topicPartition, out var batch))
            return 0;

        LeakGateHarness.RetireBatch(accumulator, batch, offset);

        var metadata = produce.GetAwaiter().GetResult();
        return metadata.Partition == topicPartition.Partition ? 1 : 0;
    }

    private static ProducerOptions CreateTransactionalProducerOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "txn-allocation-gate",
        TransactionalId = "txn-allocation-gate",
        EnableIdempotence = true,
        Acks = Acks.All,
        BufferMemory = 32UL * 1024 * 1024,
        BatchSize = 1_048_576,
        LingerMs = 0,
        RequestTimeoutMs = 500,
        DeliveryTimeoutMs = 1_000,
        CloseTimeoutMs = 1_000,
        // The test host (TUnit) installs a process-wide sample-everything ActivityListener for
        // its OTel report, which would divert every produce onto the tracing path. The stress
        // lane this gate mirrors runs without listeners; pin that branch.
        SkipProduceActivityListenerCheckForTesting = true,
    };

    /// <summary>
    /// Puts the producer in the state InitTransactionsAsync would leave it in against a
    /// TV2 (KIP-890) broker, without a coordinator round trip. BeginTransaction derives
    /// <c>_currentTransactionUsesTV2</c> from the published finalized feature.
    /// </summary>
    private static void InitializeTransactionalState(KafkaProducer<string, string> producer)
    {
        AccumulatorTestHelpers.SetPrivateField(producer, "_initialized", true);
        var metadataManager = AccumulatorTestHelpers.GetPrivateField<MetadataManager>(producer, "_metadataManager");
        metadataManager.ObserveClusterCapabilities(
            "txn-allocation-gate-cluster",
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
                new BrokerMetadata
                {
                    NodeId = 0,
                    Host = "localhost",
                    Port = 9092,
                },
            ],
            ClusterId = "txn-allocation-gate-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
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

    private static AllocationMeasurement WarmAndMeasure(
        Func<int> operation,
        int warmupIterations,
        int measuredIterations)
    {
        var checksum = 0;
        for (var i = 0; i < warmupIterations; i++)
            checksum += operation();

        long allocatedBytes = 0;
        var nonZeroCycles = 0;
        for (var i = 0; i < measuredIterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            checksum += operation();
            var cycleBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            allocatedBytes += cycleBytes;
            if (cycleBytes != 0)
                nonZeroCycles++;
        }

        return new AllocationMeasurement(allocatedBytes, nonZeroCycles, checksum);
    }

    private readonly record struct AllocationMeasurement(long AllocatedBytes, int NonZeroCycles, int Checksum);
}
