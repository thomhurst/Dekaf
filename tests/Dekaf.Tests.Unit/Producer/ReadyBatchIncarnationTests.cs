using System.Diagnostics;
using System.Reflection;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Regression tests for issue #1570: pooled ReadyBatch objects are recycled while stale
/// references still sit in retry queues. The old staleness check (IsReturnedToPool alone)
/// is unsound because the flag resets when the object is re-rented — a stale holder then
/// treats a batch owned by another partition as its own, which surfaced as PRODUCE framing
/// guard failures (declared/emitted length mismatch) and message loss in stress runs.
/// IsCurrentIncarnation combines the flag with a monotonic generation stamp to close this.
/// </summary>
public sealed class ReadyBatchIncarnationTests
{
    private static readonly Type PendingResponseType = typeof(BrokerSender).GetNestedType(
        "PendingResponse",
        BindingFlags.NonPublic)!;

    private static readonly Type ProduceRequestScratchType = typeof(BrokerSender).GetNestedType(
        "ProduceRequestScratch",
        BindingFlags.NonPublic)!;

    private static int GetReadyBatchInt32(ReadyBatch batch, string fieldName)
        => (int)typeof(ReadyBatch)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(batch)!;

    private static ReadyBatch CreateInitializedBatch(string topic = "t", int partition = 0)
    {
        var batch = new ReadyBatch();
        InitializeBatch(batch, topic, partition);
        return batch;
    }

    private static void InitializeBatch(ReadyBatch batch, string topic = "t", int partition = 0)
    {
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 100);
    }

    [Test]
    public async Task Initialize_IncrementsGeneration()
    {
        var batch = CreateInitializedBatch();
        var firstGeneration = batch.Generation;

        InitializeBatch(batch, "other-topic", 1);

        await Assert.That(batch.Generation).IsNotEqualTo(firstGeneration);
    }

    [Test]
    public async Task IsCurrentIncarnation_LiveBatch_ReturnsTrue()
    {
        var batch = CreateInitializedBatch();

        await Assert.That(batch.IsCurrentIncarnation(batch.Generation)).IsTrue();
    }

    [Test]
    public async Task Cleanup_WithActiveResourcePin_DefersPooledResourceReturnUntilPinRelease()
    {
        var batch = new ReadyBatch();
        var recordBatch = RecordBatch.RentFromPool();
        recordBatch.Records = Array.Empty<Record>();
        var arena = BatchArena.RentOrCreate(128);
        batch.Initialize(
            new TopicPartition("test-topic", 0),
            recordBatch,
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 0,
            dataSize: 100,
            arena: arena);
        var generation = batch.Generation;

        await Assert.That(batch.TryAcquireResourcePin(generation)).IsTrue();

        batch.CompleteSend(0, DateTimeOffset.UtcNow);

        await Assert.That(GetReadyBatchInt32(batch, "_cleanedUp")).IsEqualTo(1);
        await Assert.That(GetReadyBatchInt32(batch, "_resourcesCleanedUp")).IsEqualTo(0);

        batch.ReleaseResourcePin();

        await Assert.That(GetReadyBatchInt32(batch, "_resourcesCleanedUp")).IsEqualTo(2);
    }

    [Test]
    public async Task TryAcquireResourcePin_AfterCleanupRequested_ReturnsFalse()
    {
        var batch = CreateInitializedBatch();
        var generation = batch.Generation;

        batch.CompleteSend(0, DateTimeOffset.UtcNow);

        await Assert.That(batch.TryAcquireResourcePin(generation)).IsFalse();
    }

    [Test]
    public async Task IsCurrentIncarnation_ReturnedToPool_ReturnsFalse()
    {
        var batch = CreateInitializedBatch();
        var capturedGeneration = batch.Generation;

        // Simulate ReturnReadyBatch marking the batch as returned (still in the pool,
        // not yet re-rented). The generation is unchanged, so only the flag catches this.
        Interlocked.Exchange(ref batch._returnedToPool, 1);

        await Assert.That(batch.IsCurrentIncarnation(capturedGeneration)).IsFalse();
    }

    [Test]
    public async Task IsCurrentIncarnation_RecycledAndReRented_ReturnsFalse()
    {
        // The exact sequence from #1570: a stale reference (e.g. in _sendFailedRetries)
        // captured the generation, then the batch completed, went back to the pool, and
        // was re-rented for a different partition. Re-initialization resets
        // _returnedToPool to 0, so the flag check alone passes — only the generation
        // comparison detects that this is a different incarnation.
        var batch = CreateInitializedBatch("original-topic", 0);
        var staleGeneration = batch.Generation;

        Interlocked.Exchange(ref batch._returnedToPool, 1); // returned to pool
        InitializeBatch(batch, "new-owner-topic", 3);       // re-rented by a new owner

        await Assert.That(batch.IsReturnedToPool).IsFalse();       // flag check alone would pass
        await Assert.That(batch.IsCurrentIncarnation(staleGeneration)).IsFalse(); // generation catches it
        await Assert.That(batch.IsCurrentIncarnation(batch.Generation)).IsTrue(); // new owner unaffected
    }

    [Test]
    public async Task Initialize_GenerationIncrementVisibleBeforeFlagClear()
    {
        // Initialize() must bump the generation BEFORE clearing _returnedToPool.
        // IsCurrentIncarnation reads the flag first and the generation second, so this
        // write order guarantees a reader that sees the cleared flag also sees the new
        // generation. This test pins the observable end state; the ordering itself is
        // enforced by the write sequence in Initialize() (see comment there).
        var batch = CreateInitializedBatch();
        var staleGeneration = batch.Generation;

        Interlocked.Exchange(ref batch._returnedToPool, 1);
        InitializeBatch(batch);

        await Assert.That(batch.IsReturnedToPool).IsFalse();
        await Assert.That(batch.Generation).IsNotEqualTo(staleGeneration);
    }

    [Test]
    public async Task PendingResponse_IsSameIncarnation_ReturnedToPool_ReturnsFalse()
    {
        var batch = CreateInitializedBatch();
        var capturedGeneration = batch.Generation;
        var batches = new[] { batch };
        var generations = new[] { capturedGeneration };
        var responseTask = new PipelinedResponse<ProduceResponse>(
            Task.FromResult(new ProduceResponse()));
        var timestamp = Stopwatch.GetTimestamp();
        var pending = Activator.CreateInstance(
            PendingResponseType,
            responseTask, batches, generations, null, (short)12, 1,
            (long)batch.EncodedSize, (long)batch.DataSize, timestamp,
            default(BrokerUnackedByteBudget.DeliverySnapshot))!;
        var isSameIncarnation = PendingResponseType.GetMethod("IsSameIncarnation")!;

        Interlocked.Exchange(ref batch._returnedToPool, 1);

        var result = (bool)isSameIncarnation.Invoke(pending, [0])!;

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ProduceRequestScratch_BuildV13_SortsBatchesAndResolvesTopicIds()
    {
        var laterTopicId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var earlierTopicId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        var laterTopicBatch = CreateInitializedBatch("z-topic", 0);
        InitializeBatch(laterTopicBatch, "z-topic", 0);
        var earlierTopicBatch = CreateInitializedBatch("a-topic", 1);
        var batches = new[] { laterTopicBatch, earlierTopicBatch };
        var generations = new[] { laterTopicBatch.Generation, earlierTopicBatch.Generation };
        var scratch = Activator.CreateInstance(
            ProduceRequestScratchType,
            new ProducerOptions { BootstrapServers = ["localhost:9092"] },
            new CompressionCodecRegistry(),
            4)!;
        var build = ProduceRequestScratchType.GetMethod("Build")!;
        await using var metadataManager = new MetadataManager(
            Substitute.For<IConnectionPool>(),
            ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [],
            Topics =
            [
                CreateTopicMetadata("z-topic", laterTopicId),
                CreateTopicMetadata("a-topic", earlierTopicId)
            ]
        });
        var topicIds = new Guid[2];

        var request = (ProduceRequest)build.Invoke(
            scratch,
            [batches, generations, topicIds, 2, (short)13, metadataManager])!;

        await Assert.That(batches[0]).IsSameReferenceAs(earlierTopicBatch);
        await Assert.That(generations[0]).IsEqualTo(earlierTopicBatch.Generation);
        await Assert.That(batches[1]).IsSameReferenceAs(laterTopicBatch);
        await Assert.That(generations[1]).IsEqualTo(laterTopicBatch.Generation);
        await Assert.That(topicIds).IsEquivalentTo([earlierTopicId, laterTopicId]);
        await Assert.That(request.GetTopicEntry(0).Name).IsEqualTo("a-topic");
        await Assert.That(request.GetTopicEntry(0).TopicId).IsEqualTo(earlierTopicId);
        await Assert.That(request.GetTopicEntry(1).Name).IsEqualTo("z-topic");
        await Assert.That(request.GetTopicEntry(1).TopicId).IsEqualTo(laterTopicId);
    }

    private static TopicMetadata CreateTopicMetadata(string name, Guid topicId) => new()
    {
        ErrorCode = ErrorCode.None,
        Name = name,
        TopicId = topicId,
        Partitions =
        [
            new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = 0,
                LeaderId = 1,
                ReplicaNodes = [1],
                IsrNodes = [1]
            }
        ]
    };
}
