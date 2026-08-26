using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel]
public sealed class ConsumerLagTests
{
    private const int RetainedWatermarkLimit = 256;
    private static readonly TopicPartition Partition = new("lag-topic", 0);

    [Test]
    public async Task GetCurrentLag_ReturnsNullUntilPositionAndWatermarkAreAvailable()
    {
        await using var consumer = CreateConsumer();

        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(15);
    }

    [Test]
    public async Task GetCurrentLag_UsesCachedWatermarkAcrossSeekAndClampsAtZero()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, 20));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(5);

        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, 30));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(0);

        consumer.SeekToEnd(Partition);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task GetCurrentLag_ReturnsNullAfterUnassignmentWhileWatermarksRemainCached()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.IncrementalUnassign([Partition]);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 25));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task UnassignedWatermarks_RetainOnlyNewest256Snapshots()
    {
        await using var consumer = CreateConsumer();

        RetainWatermarksForPartitions(consumer, firstPartition: 0, RetainedWatermarkLimit + 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsNull();
        await Assert.That(consumer.GetWatermarkOffsets(new TopicPartition(Partition.Topic, 1)))
            .IsEqualTo(new WatermarkOffsets(1, 101));
        await Assert.That(consumer.GetWatermarkOffsets(
                new TopicPartition(Partition.Topic, RetainedWatermarkLimit)))
            .IsEqualTo(new WatermarkOffsets(RetainedWatermarkLimit, RetainedWatermarkLimit + 100));
        await Assert.That(GetWatermarkCacheCount(consumer)).IsEqualTo(RetainedWatermarkLimit);
    }

    [Test]
    public async Task UnassignedWatermarks_ExpiredTicketDoesNotRemoveReassignedEntry()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));
        consumer.IncrementalUnassign([Partition]);

        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 20)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(10, 50));

        RetainWatermarksForPartitions(consumer, firstPartition: 1, RetainedWatermarkLimit);

        await Assert.That(consumer.GetWatermarkOffsets(Partition))
            .IsEqualTo(new WatermarkOffsets(10, 50));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(30);
        await Assert.That(GetWatermarkCacheCount(consumer)).IsEqualTo(RetainedWatermarkLimit + 1);
    }

    [Test]
    public async Task UnassignedWatermarks_ExpiredTicketDoesNotRemoveRefreshedEntry()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));
        consumer.IncrementalUnassign([Partition]);
        SetQueriedCachedWatermarks(consumer, Partition, new WatermarkOffsets(10, 50), updateSequence: 1);

        RetainWatermarksForPartitions(consumer, firstPartition: 1, RetainedWatermarkLimit - 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition))
            .IsEqualTo(new WatermarkOffsets(10, 50));
        await Assert.That(GetWatermarkCacheCount(consumer)).IsEqualTo(RetainedWatermarkLimit);

        RetainWatermarksForPartitions(consumer, firstPartition: RetainedWatermarkLimit, count: 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsNull();
        await Assert.That(GetWatermarkCacheCount(consumer)).IsEqualTo(RetainedWatermarkLimit);
    }

    [Test]
    public async Task UnassignedWatermarks_OlderQueryCannotOverwriteNewerSnapshot()
    {
        await using var consumer = CreateConsumer();

        SetQueriedCachedWatermarks(consumer, Partition, new WatermarkOffsets(10, 50), updateSequence: 2);
        SetQueriedCachedWatermarks(consumer, Partition, new WatermarkOffsets(0, 25), updateSequence: 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition))
            .IsEqualTo(new WatermarkOffsets(10, 50));
    }

    [Test]
    public async Task UnassignedWatermarks_OlderQueryCannotReplaceDivergentSnapshot()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(25),
            watermarkUpdateSequence: 5);
        UpdateCachedLagEndOffset(consumer, lagEndOffset: 30, watermarkUpdateSequence: 10);
        consumer.IncrementalUnassign([Partition]);

        SetQueriedCachedWatermarks(consumer, Partition, new WatermarkOffsets(10, 50), updateSequence: 7);

        await Assert.That(consumer.GetWatermarkOffsets(Partition))
            .IsEqualTo(new WatermarkOffsets(0, 25));
        await Assert.That(ReadCachedLagEndOffset(consumer)).IsEqualTo(30);
    }

    [Test]
    public async Task UnassignAndAssign_PublicApisClearPartitionState()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));
        consumer.Pause(Partition);

        consumer.Unassign();
        consumer.Assign(Partition);

        await Assert.That(consumer.Assignment).Contains(Partition);
        await Assert.That(consumer.GetPosition(Partition)).IsNull();
        await Assert.That(consumer.Paused).DoesNotContain(Partition);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsNull();
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task GetCurrentLag_RejectsFetchWatermarkFromPreviousAssignment()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var previousFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.IncrementalUnassign([Partition]);
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 20)]);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            previousFetchBufferEpoch);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsNull();
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        UpdateWatermarksFromFetchResponse(consumer, CreateFetchResponse(30));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(10);
    }

    [Test]
    public async Task GetCurrentLag_UnrelatedIncrementalChangesPreserveCachedWatermark()
    {
        var otherPartition = new TopicPartition(Partition.Topic, 1);
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.IncrementalAssign([
            new TopicPartitionOffset(otherPartition.Topic, otherPartition.Partition, 20)
        ]);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 25));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(15);

        consumer.IncrementalUnassign([otherPartition]);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 25));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(15);
    }

    [Test]
    public async Task UpdateWatermarks_RemovalPublishedBeforeCleanupRejectsStaleWriter()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var previousFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        InvalidateFetchesForPartitions(consumer, [Partition]);
        GetAssignment(consumer).Remove(Partition);
        PublishAssignmentSnapshot(consumer);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            previousFetchBufferEpoch);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 25));
        await Assert.That(GetWatermarkCacheCount(consumer)).IsEqualTo(1);
    }

    [Test]
    public async Task GetCurrentLag_ReadCommittedUsesLastStableOffset()
    {
        await using var consumer = CreateConsumer(IsolationLevel.ReadCommitted);
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var response = new FetchResponsePartition
        {
            PartitionIndex = Partition.Partition,
            HighWatermark = 25,
            LastStableOffset = 17,
            LogStartOffset = 0
        };

        UpdateWatermarksFromFetchResponse(consumer, response);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(7);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)!.Value.High).IsEqualTo(25);
    }

    [Test]
    public async Task WatermarkCacheEntry_ConcurrentLagReadsReturnCompleteOffsets()
    {
        const long firstOffset = 0x1111111122222222;
        const long secondOffset = 0x3333333344444444;
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 0)]);
        var updateWatermarks = typeof(KafkaConsumer<string, string>)
            .GetMethod("UpdateWatermarksFromFetchResponse", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<UpdateWatermarks>();
        var first = CreateFetchResponse(firstOffset);
        var second = CreateFetchResponse(secondOffset);
        var fetchBufferEpoch = GetFetchBufferEpoch(consumer);
        updateWatermarks(consumer, Partition, first, fetchBufferEpoch, -1, 0);
        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var iteration = 0; iteration < 100_000; iteration++)
            {
                var response = (iteration & 1) == 0 ? second : first;
                updateWatermarks(consumer, Partition, response, fetchBufferEpoch, -1, 0);
            }
        });

        start.Set();
        var completeOffsetsOnly = true;
        for (var iteration = 0; iteration < 100_000; iteration++)
        {
            var offset = consumer.GetCurrentLag(Partition);
            if (offset != firstOffset && offset != secondOffset)
            {
                completeOffsetsOnly = false;
                break;
            }
        }

        await writer;
        await Assert.That(completeOffsetsOnly).IsTrue();
    }

    [Test]
    public async Task WatermarkCacheEntry_UnrelatedAssignmentAcceptsInFlightPartitionUpdate()
    {
        var otherPartition = new TopicPartition(Partition.Topic, 1);
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var oldFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.IncrementalAssign([
            new TopicPartitionOffset(otherPartition.Topic, otherPartition.Partition, 0)
        ]);
        UpdateWatermarksFromFetchResponse(consumer, CreateFetchResponse(30));

        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            oldFetchBufferEpoch);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 100));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(90);
    }

    [Test]
    public async Task WatermarkCacheEntry_OlderResponsesCannotOverwriteNewerValues()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        // A newer lag query completes before an older fetch response.
        UpdateCachedLagEndOffset(consumer, 110, watermarkUpdateSequence: 2);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            watermarkUpdateSequence: 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 100));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(100);

        // A newer fetch completes before an older lag query response.
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(120),
            watermarkUpdateSequence: 4);
        UpdateCachedLagEndOffset(consumer, 130, watermarkUpdateSequence: 3);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 120));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(110);
    }

    [Test]
    public async Task WatermarkCacheEntry_CrossConnectionUpdatesUseLeaderEpochAndOffsets()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        // A request stamped first can reach the broker after a request on another connection.
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            leaderEpoch: 5,
            watermarkUpdateSequence: 2);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(110),
            leaderEpoch: 5,
            watermarkUpdateSequence: 1);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 110));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(100);

        // A later response from the same leader cannot regress offsets, regardless of stamp order.
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(90),
            leaderEpoch: 5,
            watermarkUpdateSequence: 3);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 110));

        // A new leader epoch may legitimately truncate the log.
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(50),
            leaderEpoch: 6,
            watermarkUpdateSequence: 1);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(120),
            leaderEpoch: 5,
            watermarkUpdateSequence: 4);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 50));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(40);
    }

    [Test]
    public async Task WatermarkCacheEntry_SequenceWrapPreservesIndependentFreshness()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        UpdateCachedLagEndOffset(consumer, 110, watermarkUpdateSequence: 1L << 32);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            watermarkUpdateSequence: uint.MaxValue);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 100));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(100);

        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(120),
            watermarkUpdateSequence: 1L << 32);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 120));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(110);
    }

    [Test]
    public async Task WatermarkCacheEntry_SequenceWrapRejectsPreWrapWatermark()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(100),
            watermarkUpdateSequence: uint.MaxValue);
        UpdateCachedLagEndOffset(consumer, 110, watermarkUpdateSequence: 1L << 32);
        UpdateWatermarksFromFetchResponse(
            consumer,
            CreateFetchResponse(90),
            watermarkUpdateSequence: uint.MaxValue - 1L);

        await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 100));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(100);
    }

    [Test]
    public async Task WatermarkCacheEntry_StaleCreatorCannotRemoveNewerUpdate()
    {
        var otherPartition = new TopicPartition(Partition.Topic, 1);
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var oldFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        KafkaConsumer<string, string>.BeforeWatermarkCacheEntryCreationForTest = () =>
        {
            KafkaConsumer<string, string>.BeforeWatermarkCacheEntryCreationForTest = null;
            consumer.IncrementalAssign([
                new TopicPartitionOffset(otherPartition.Topic, otherPartition.Partition, 0)
            ]);
            UpdateWatermarksFromFetchResponse(
                consumer,
                CreateFetchResponse(30),
                watermarkUpdateSequence: 2);
        };

        try
        {
            UpdateWatermarksFromFetchResponse(
                consumer,
                CreateFetchResponse(20),
                oldFetchBufferEpoch,
                watermarkUpdateSequence: 1);

            await Assert.That(consumer.GetWatermarkOffsets(Partition)).IsEqualTo(new WatermarkOffsets(0, 30));
            await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(20);
        }
        finally
        {
            KafkaConsumer<string, string>.BeforeWatermarkCacheEntryCreationForTest = null;
        }
    }

    [Test]
    public async Task GetCurrentLag_AssignmentChangeAfterSnapshotReturnsNull()
    {
        var otherPartition = new TopicPartition(Partition.Topic, 1);
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));
        var oldAssignmentVersion = GetAssignmentVersion(consumer);
        var entry = GetWatermarkCacheEntry(consumer);

        consumer.IncrementalAssign([
            new TopicPartitionOffset(otherPartition.Topic, otherPartition.Partition, 0)
        ]);

        var lag = CalculateLagIfAssignmentUnchanged(consumer, oldAssignmentVersion, 10, entry);

        await Assert.That(lag).IsNull();
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(15);
    }

    private static FetchResponsePartition CreateFetchResponse(long offset) => new()
    {
        PartitionIndex = Partition.Partition,
        HighWatermark = offset,
        LastStableOffset = offset,
        LogStartOffset = 0
    };

    [Test]
    public async Task QueryCurrentLagAsync_UnassignedPartitionCompletesSynchronouslyWithoutNetwork()
    {
        await using var consumer = CreateConsumer();
        SetInitialized(consumer);

        var result = consumer.QueryCurrentLagAsync(Partition);

        await Assert.That(result.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await result).IsNull();
    }

    [Test]
    public async Task QueryCurrentLagAsync_BeforeInitializationThrowsInvalidOperationException()
    {
        await using var consumer = CreateConsumer();

        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task QueryCurrentLagAsync_CanceledTokenThrowsBeforeNetwork()
    {
        await using var consumer = CreateConsumer();
        SetInitialized(consumer);
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition, cancellation.Token); })
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task LagQueries_AfterDisposal_ThrowObjectDisposedException()
    {
        var consumer = CreateConsumer();
        await consumer.DisposeAsync();

        await Assert.That(() => consumer.GetCurrentLag(Partition))
            .Throws<ObjectDisposedException>();
        await Assert.That(async () => await consumer.QueryCurrentLagAsync(Partition))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task LagExtensions_CustomConsumerWithoutCapability_ThrowNotSupportedException()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();

        await Assert.That(() => consumer.GetCurrentLag(Partition))
            .Throws<NotSupportedException>();
        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition); })
            .Throws<NotSupportedException>();
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted) => new(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "lag-tests",
            IsolationLevel = isolationLevel
        },
        Serializers.String,
        Serializers.String);

    private static void SetCachedWatermarks(
        KafkaConsumer<string, string> consumer,
        WatermarkOffsets watermarks) => SetCachedWatermarks(consumer, Partition, watermarks);

    private static void RetainWatermarksForPartitions(
        KafkaConsumer<string, string> consumer,
        int firstPartition,
        int count)
    {
        for (var partitionIndex = firstPartition; partitionIndex < firstPartition + count; partitionIndex++)
        {
            var partition = new TopicPartition(Partition.Topic, partitionIndex);
            consumer.IncrementalAssign([
                new TopicPartitionOffset(partition.Topic, partition.Partition, partitionIndex)
            ]);
            SetCachedWatermarks(
                consumer,
                partition,
                new WatermarkOffsets(partitionIndex, partitionIndex + 100));
            consumer.IncrementalUnassign([partition]);
        }
    }

    private static void SetCachedWatermarks(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        WatermarkOffsets watermarks)
    {
        UpdateWatermarksFromFetchResponse(consumer, partition, new FetchResponsePartition
        {
            PartitionIndex = partition.Partition,
            HighWatermark = watermarks.High,
            LastStableOffset = watermarks.High,
            LogStartOffset = watermarks.Low
        });
    }

    private static void UpdateWatermarksFromFetchResponse(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition response,
        int? fetchBufferEpoch = null,
        int leaderEpoch = -1,
        long watermarkUpdateSequence = 0) => UpdateWatermarksFromFetchResponse(
            consumer,
            Partition,
            response,
            fetchBufferEpoch,
            leaderEpoch,
            watermarkUpdateSequence);

    private static void UpdateWatermarksFromFetchResponse(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        FetchResponsePartition response,
        int? fetchBufferEpoch = null,
        int leaderEpoch = -1,
        long watermarkUpdateSequence = 0)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "UpdateWatermarksFromFetchResponse",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("UpdateWatermarksFromFetchResponse method not found");
        method.Invoke(consumer, [
            partition,
            response,
            fetchBufferEpoch ?? GetFetchBufferEpoch(consumer),
            leaderEpoch,
            watermarkUpdateSequence
        ]);
    }

    private static void SetQueriedCachedWatermarks(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        WatermarkOffsets watermarks,
        long updateSequence)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "UpdateQueriedCachedWatermarks",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("UpdateQueriedCachedWatermarks method not found");
        method.Invoke(consumer, [
            partition,
            watermarks.Low,
            watermarks.High,
            watermarks.High,
            null,
            -1,
            updateSequence
        ]);
    }

    private static void UpdateCachedLagEndOffset(
        KafkaConsumer<string, string> consumer,
        long lagEndOffset,
        long watermarkUpdateSequence)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "UpdateCachedLagEndOffset",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("UpdateCachedLagEndOffset method not found");
        method.Invoke(consumer, [Partition, lagEndOffset, -1, watermarkUpdateSequence]);
    }

    private static int GetAssignmentVersion(KafkaConsumer<string, string> consumer) =>
        (int)(typeof(KafkaConsumer<string, string>)
            .GetField("_assignmentEnsureVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException("_assignmentEnsureVersion field not found"));

    private static int GetFetchBufferEpoch(KafkaConsumer<string, string> consumer) =>
        (int)(typeof(KafkaConsumer<string, string>)
            .GetField("_fetchBufferEpoch", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException("_fetchBufferEpoch field not found"));

    private static HashSet<TopicPartition> GetAssignment(KafkaConsumer<string, string> consumer) =>
        (HashSet<TopicPartition>)(typeof(KafkaConsumer<string, string>)
            .GetField("_assignment", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException("_assignment field not found"));

    private static void PublishAssignmentSnapshot(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "PublishAssignmentSnapshot",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PublishAssignmentSnapshot method not found");
        method.Invoke(consumer, null);
    }

    private static void InvalidateFetchesForPartitions(
        KafkaConsumer<string, string> consumer,
        IEnumerable<TopicPartition> partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "InvalidateFetchesForPartitionsLocked",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("InvalidateFetchesForPartitionsLocked method not found");
        method.Invoke(consumer, [partitions]);
    }

    private static int GetWatermarkCacheCount(KafkaConsumer<string, string> consumer) =>
        ((System.Collections.IDictionary)(typeof(KafkaConsumer<string, string>)
            .GetField("_watermarks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException("_watermarks field not found"))).Count;

    private static object GetWatermarkCacheEntry(KafkaConsumer<string, string> consumer) =>
        ((System.Collections.IDictionary)(typeof(KafkaConsumer<string, string>)
            .GetField("_watermarks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException("_watermarks field not found")))[Partition]
        ?? throw new InvalidOperationException("Watermark cache entry not found");

    private static long ReadCachedLagEndOffset(KafkaConsumer<string, string> consumer)
    {
        var entry = GetWatermarkCacheEntry(consumer);
        var method = entry.GetType().GetMethod("ReadLagEndOffset", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ReadLagEndOffset method not found");
        return (long)(method.Invoke(entry, null)
            ?? throw new InvalidOperationException("ReadLagEndOffset returned null"));
    }

    private static long? CalculateLagIfAssignmentUnchanged(
        KafkaConsumer<string, string> consumer,
        int assignmentVersion,
        long position,
        object entry)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CalculateLagIfAssignmentUnchanged",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CalculateLagIfAssignmentUnchanged method not found");
        return (long?)method.Invoke(consumer, [assignmentVersion, position, entry]);
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initialized = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found");
        initialized.SetValue(consumer, true);
    }

    private delegate void UpdateWatermarks(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        FetchResponsePartition response,
        int fetchBufferEpoch,
        int leaderEpoch,
        long watermarkUpdateSequence);
}
