using System.Reflection;
using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Seek and StoreOffset do not check ownership, and they run concurrently with assignment sync.
/// A group-managed consumer must not commit an offset stored for a partition it no longer owns
/// (or does not own yet): that commit would rewind the progress of the member that owns it.
/// </summary>
public sealed partial class ConsumerDirtyCommitTests
{
    // Tests that subscribe to the static AfterStoredOffsetOwnershipEvaluatedForTest seam run one at
    // a time: concurrent += and -= on the same delegate field can drop a subscription.
    private const string OwnershipHook = "StoredOffsetOwnershipHook";
    private static readonly TopicPartition OwnedP0 = new("topic-a", 0);
    private static readonly TopicPartition RevokedP1 = new("topic-a", 1);

    [Test]
    [Arguments("Seek", "CommitAsync")]
    [Arguments("Seek", "AutoCommit")]
    [Arguments("Seek", "Close")]
    [Arguments("StoreOffset", "CommitAsync")]
    [Arguments("StoreOffset", "AutoCommit")]
    [Arguments("StoreOffset", "Close")]
    public async Task GroupConsumer_OffsetStoredAfterSyncRevokedPartition_IsNotCommitted(string store, string commit)
    {
        // A caller checks ownership, the assignment sync then revokes P1 and clears its state,
        // and only then does the caller's seek or store land (the hosted service's check-then-act).
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            commit == "AutoCommit" ? OffsetCommitMode.Auto : OffsetCommitMode.Manual);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0, RevokedP1);
        RevokeLikeAssignmentSync(consumer, RevokedP1);

        if (store == "Seek")
            consumer.Seek(new TopicPartitionOffset("topic-a", 1, 3));
        else
            consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await CommitByAsync(consumer, commit);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
        // No later commit sends it either while P1 stays unowned.
        if (commit == "CommitAsync")
        {
            await consumer.CommitAsync(CancellationToken.None);
            await Assert.That(requests.Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GroupConsumer_OffsetStoredForAssignedButUninitializedPartition_IsNotCommitted()
    {
        // Assignment sync publishes a new partition before it initializes the position, which
        // replaces anything stored before; a commit in between must not send the stale offset.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        AddUninitializedAssignment(consumer, RevokedP1);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
    }

    [Test]
    [NotInParallel(OwnershipHook)]
    public async Task GroupConsumer_OwnershipChangesWhileStoredOffsetsAreFiltered_CommitsAConsistentRequest()
    {
        // Assignment sync runs concurrently with commits. Ownership must be evaluated once per
        // offset: re-evaluating it after a sync initialized P1 sized the request for one offset
        // and then tried to fill two.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        AddUninitializedAssignment(consumer, RevokedP1);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        var setFetchPosition = typeof(KafkaConsumer<string, string>).GetMethod(
            "SetFetchPosition",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Action<object> initializeP1 = instance =>
        {
            if (ReferenceEquals(instance, consumer))
                setFetchPosition.Invoke(consumer, [RevokedP1, 0L]);
        };

        KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest += initializeP1;
        try
        {
            await consumer.CommitAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest -= initializeP1;
        }

        // Every offset sent is one that was stored: P0's, and P1's only if it was evaluated after
        // the sync initialized it.
        var committed = GetCommittedOffsets(requests.Single());
        await Assert.That(committed).Contains(new TopicPartitionOffset("topic-a", 0, 10));
        foreach (var offset in committed)
        {
            await Assert.That(offset == new TopicPartitionOffset("topic-a", 0, 10)
                || offset == new TopicPartitionOffset("topic-a", 1, 3)).IsTrue();
        }
    }

    [Test]
    public async Task GroupConsumer_StoreOffsetOfRecordFetchedBeforeReassignment_IsIgnored()
    {
        // A handler keeps a record across a revocation and a reassignment of its partition to
        // this member, then stores it: that offset belongs to the ended ownership and would
        // rewind the progress made under the new one.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var retained = CreateFetchedResult(fetch, offset: 2);
        RevokeLikeAssignmentSync(consumer, OwnedP0);
        PublishInitializedAssignment(consumer, OwnedP0);

        consumer.StoreOffset(retained);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task GroupConsumer_StoreOffsetOfRecordFetchedUnderCurrentOwnership_IsStoredAfterUnrelatedRebalance()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var current = CreateFetchedResult(fetch, offset: 2);
        // Another partition joins: the assignment version moves on, P0's ownership does not.
        PublishInitializedAssignment(consumer, RevokedP1);

        consumer.StoreOffset(current);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 3, leaderEpoch: 0)
        ]);
    }

    [Test]
    public async Task ManualAssignmentConsumer_StoreOffsetOfRecordFetchedBeforeReassignment_IsStillStored()
    {
        // The application owns partition bookkeeping without a subscription: unchanged.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Assign(OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var retained = CreateFetchedResult(fetch, offset: 2);
        consumer.Unassign();
        consumer.Assign(OwnedP0);

        consumer.StoreOffset(retained);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 3, leaderEpoch: 0)
        ]);
    }

    [Test]
    public async Task GroupConsumer_RevocationPending_StoresAndCommitsNothingAfterTheRevocationCommit()
    {
        // The coordinator revoked P0 (a heartbeat) and the revocation commit sent what was stored
        // until then; assignment sync has not run yet. A later store, or a commit of an offset
        // stored meanwhile, would follow the revocation commit over the next owner's progress.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None, OffsetCommitMode.Auto);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        consumer.StoreOffset(CreateFetchedResult(fetch, offset: 9));
        SetCoordinatorAssignment(consumer);

        await CommitRevokedOffsetsAsync(consumer, [OwnedP0]);
        consumer.StoreOffset(CreateFetchedResult(fetch, offset: 20));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 30));
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(1);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 0)
        ]);
        await Assert.That(GetDirtyStoredOffsets(consumer).TryGetValue(OwnedP0, out var stored) ? stored : -1)
            .IsEqualTo(30);
    }

    [Test]
    public async Task GroupConsumer_RecordOwnership_FollowsTheRecordsFetchAcrossRebalances()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        using var oldFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var oldRecord = CreateFetchedResult(oldFetch, offset: 2);

        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.Owned);

        // The coordinator revokes P0: pending until assignment sync.
        SetCoordinatorAssignment(consumer);
        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.RevocationPending);

        // Sync drops P0, then P0 is assigned back (ABA): the old record's ownership has ended, a
        // record fetched under the new ownership is owned.
        RevokeLikeAssignmentSync(consumer, OwnedP0);
        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.Ended);
        PublishInitializedAssignment(consumer, OwnedP0);
        using var newFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());

        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.Ended);
        await Assert.That(ownership.GetRecordOwnership(CreateFetchedResult(newFetch, offset: 2)))
            .IsEqualTo(RecordOwnership.Owned);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GroupConsumer_InterceptorReplacementOfARecord_KeepsItsFetchIdentity(bool batchDelivery)
    {
        // An OnConsume interceptor may return a new result. It must keep the delivered record's
        // fetch generation, or StoreOffset could no longer tell its ownership has ended.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        typeof(KafkaConsumer<string, string>)
            .GetField("_interceptors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(consumer, new IConsumerInterceptor<string, string>[] { new ReplacingInterceptor() });
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var delivered = CreateFetchedResult(fetch, offset: 2);
        var replaced = batchDelivery
            ? new ReplacingInterceptor().OnConsume(delivered).WithBrokerIdentityFrom(in delivered)
            : (ConsumeResult<string, string>)typeof(KafkaConsumer<string, string>)
                .GetMethod("ApplyOnConsumeInterceptorsSlow", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(consumer, [delivered])!;
        await Assert.That(replaced.Value).IsEqualTo("replaced");
        await Assert.That(replaced.FetchGeneration).IsEqualTo(delivered.FetchGeneration);

        RevokeLikeAssignmentSync(consumer, OwnedP0);
        PublishInitializedAssignment(consumer, OwnedP0);
        consumer.StoreOffset(replaced);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task GroupConsumer_InterceptorReplacementBorrowedFromAnotherLiveFetch_KeepsItsHeadersAndTheDeliveredOwnership()
    {
        // A batch interceptor may return a result delivered from another fetch that is still in
        // use. Its headers belong to that fetch and must stay readable; ownership is still the
        // delivered record's.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var deliveredFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var delivered = CreateFetchedResult(deliveredFetch, offset: 2);
        Header[] headers = [new("trace-id", "abc"u8.ToArray())];
        using var otherFetch = PendingFetchData.Create("topic-b", 3, Array.Empty<RecordBatch>());
        var borrowed = new ConsumeResult<string, string>(
            "topic-b", 3, 40, default, true, default, true, headers, headers.Length, otherFetch,
            0, TimestampType.CreateTime, null, null, null);

        var replaced = borrowed.WithBrokerIdentityFrom(in delivered);

        await Assert.That(replaced.Headers.Count).IsEqualTo(1);
        await Assert.That(replaced.Headers[0].Key).IsEqualTo("trace-id");
        await Assert.That(replaced.FetchGeneration).IsEqualTo(delivered.FetchGeneration);
        await Assert.That(replaced.ResolveFetchGeneration()).IsEqualTo(delivered.ResolveFetchGeneration());

        // The delivered record's ownership ended (P0 was revoked and assigned again).
        RevokeLikeAssignmentSync(consumer, OwnedP0);
        PublishInitializedAssignment(consumer, OwnedP0);
        consumer.StoreOffset(replaced);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task GroupConsumer_InterceptorReplacementBorrowedFromAnotherLiveFetch_CommitsWhileTheDeliveredOwnershipHolds()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var deliveredFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var delivered = CreateFetchedResult(deliveredFetch, offset: 2);
        using var otherFetch = PendingFetchData.Create("topic-b", 3, Array.Empty<RecordBatch>());
        var borrowed = new ConsumeResult<string, string>(
            "topic-b", 3, 40, default, true, default, true, null, 0, otherFetch,
            0, TimestampType.CreateTime, null, null, null);

        consumer.StoreOffset(borrowed.WithBrokerIdentityFrom(in delivered));
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 3, leaderEpoch: 0)
        ]);
    }

    [Test]
    [NotInParallel(OwnershipHook)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GroupConsumer_CommitFlushRacingRevokeAndReassign_DoesNotTagTheOldPositionWithTheNewOwnership(
        bool fetchStillQueued)
    {
        // CommitAsync flushes the active consumed position of the old ownership. P0 is revoked and
        // assigned again between the read and the store: the old position must not become the new
        // ownership's committed offset (a retried commit would rewind the new owner).
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None, OffsetCommitMode.Auto);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownershipStart = ((System.Collections.Concurrent.ConcurrentDictionary<TopicPartition, long>)
            typeof(KafkaConsumer<string, string>)
                .GetField("_ownershipStartGenerations", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(consumer)!)[OwnedP0];
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>(), ownershipStart: ownershipStart);
        typeof(KafkaConsumer<string, string>)
            .GetMethod("PublishActiveConsumedPositionCore", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [OwnedP0, 0L, -1, ownershipStart]);
        if (fetchStillQueued)
            EnqueuePendingFetch(consumer, fetch);

        var reassigned = 0;
        Action<object> revokeAndReassign = instance =>
        {
            if (!ReferenceEquals(instance, consumer) || Interlocked.Exchange(ref reassigned, 1) != 0)
                return;

            RevokeLikeAssignmentSync(consumer, OwnedP0);
            PublishInitializedAssignment(consumer, OwnedP0);
        };

        KafkaConsumer<string, string>.AfterActiveConsumedPositionReadForTest += revokeAndReassign;
        try
        {
            await consumer.CommitAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterActiveConsumedPositionReadForTest -= revokeAndReassign;
        }

        await Assert.That(Volatile.Read(ref reassigned)).IsEqualTo(1);
        await Assert.That(requests.SelectMany(r => r.Topics).SelectMany(t => t.Partitions)).IsEmpty();
    }

    private static void EnqueuePendingFetch(KafkaConsumer<string, string> consumer, PendingFetchData fetch)
    {
        var queue = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        queue.GetType().GetMethod("Enqueue")!.Invoke(queue, [fetch]);
    }

    private sealed class ReplacingInterceptor : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
            => new(
                result.Topic,
                result.Partition,
                result.Offset,
                ReadOnlyMemory<byte>.Empty,
                isKeyNull: true,
                "replaced"u8.ToArray(),
                isValueNull: false,
                headers: null,
                timestampMs: 0,
                TimestampType.NotAvailable,
                leaderEpoch: null,
                keyDeserializer: null,
                valueDeserializer: Serializers.String);

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
        {
        }
    }

    [Test]
    [NotInParallel(OwnershipHook)]
    public async Task GroupConsumer_PartitionReassignedWhileItsStaleOffsetIsFiltered_KeepsTheNewOwnershipsEqualOffset()
    {
        // A commit finds P1's stored offset with its revocation pending, so it is filtered. Before
        // the commit marks it clean, assignment sync drops P1 and gives it back, and the
        // application stores the same numeric offset under the new ownership. That offset must
        // survive the commit.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0, RevokedP1);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        SetCoordinatorAssignment(consumer, OwnedP0);
        var reassigned = 0;
        Action<object> reassignP1 = instance =>
        {
            if (!ReferenceEquals(instance, consumer) || Interlocked.Exchange(ref reassigned, 1) != 0)
                return;

            RevokeLikeAssignmentSync(consumer, RevokedP1);
            SetCoordinatorAssignment(consumer, OwnedP0, RevokedP1);
            PublishInitializedAssignment(consumer, RevokedP1);
            consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        };

        KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest += reassignP1;
        try
        {
            await consumer.CommitAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest -= reassignP1;
        }

        await Assert.That(Volatile.Read(ref reassigned)).IsEqualTo(1);
        await Assert.That(requests).IsEmpty();
        await consumer.CommitAsync(CancellationToken.None);
        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 3)
        ]);
    }

    [Test]
    [NotInParallel(OwnershipHook)]
    public async Task GroupConsumer_PartitionRevokedBetweenOwnershipDecisionAndSend_IsNotCommitted()
    {
        // CommitAsync decides P1 is owned, then P1 is revoked, synchronized and acknowledged (the
        // member epoch moves on) before the request is sent. Sent with the current epoch, the
        // broker accepts it and P1's new owner's progress rewinds. Sent with the epoch the
        // decision was made under, the broker rejects it (KIP-848), and the retry decides again.
        KafkaConsumer<string, string>? consumer = null;
        var accepted = new List<TopicPartitionOffset>();
        var requests = new List<OffsetCommitRequest>();
        consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            respond: request =>
            {
                if (request.GenerationIdOrMemberEpoch != GetCoordinator(consumer!).GenerationId)
                    return ErrorCode.StaleMemberEpoch;

                accepted.AddRange(GetCommittedOffsets(request));
                return ErrorCode.None;
            });
        await using var lifetime = consumer;
        consumer.Subscribe("topic-a");
        SetMemberEpoch(consumer, 5);
        PublishInitializedAssignment(consumer, OwnedP0, RevokedP1);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));
        var revoked = 0;
        Action<object> revokeP1 = instance =>
        {
            if (!ReferenceEquals(instance, consumer) || Interlocked.Exchange(ref revoked, 1) != 0)
                return;

            RevokeLikeAssignmentSync(consumer, RevokedP1);
            SetMemberEpoch(consumer, 6);
        };

        KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest += revokeP1;
        try
        {
            await consumer.CommitAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest -= revokeP1;
        }

        await Assert.That(Volatile.Read(ref revoked)).IsEqualTo(1);
        await Assert.That(accepted).IsEmpty();
        await Assert.That(requests.Select(static request => request.GenerationIdOrMemberEpoch)).IsEquivalentTo([5]);
    }

    [Test]
    [NotInParallel(OwnershipHook)]
    public async Task GroupConsumer_EpochChangedWithoutLosingThePartition_CommitsUnderTheNewEpoch()
    {
        // Another member's join moves this member to a new epoch without changing its partitions:
        // the rejected commit is decided again and sent with the new epoch.
        KafkaConsumer<string, string>? consumer = null;
        var accepted = new List<TopicPartitionOffset>();
        var requests = new List<OffsetCommitRequest>();
        consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            respond: request =>
            {
                if (request.GenerationIdOrMemberEpoch != GetCoordinator(consumer!).GenerationId)
                    return ErrorCode.StaleMemberEpoch;

                accepted.AddRange(GetCommittedOffsets(request));
                return ErrorCode.None;
            });
        await using var lifetime = consumer;
        consumer.Subscribe("topic-a");
        SetMemberEpoch(consumer, 5);
        PublishInitializedAssignment(consumer, OwnedP0);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 3));
        var bumped = 0;
        Action<object> bumpEpoch = instance =>
        {
            if (ReferenceEquals(instance, consumer) && Interlocked.Exchange(ref bumped, 1) == 0)
                SetMemberEpoch(consumer, 6);
        };

        KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest += bumpEpoch;
        try
        {
            await consumer.CommitAsync(CancellationToken.None);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterStoredOffsetOwnershipEvaluatedForTest -= bumpEpoch;
        }

        await Assert.That(accepted).IsEquivalentTo([new TopicPartitionOffset("topic-a", 0, 3)]);
        await Assert.That(requests.Select(static request => request.GenerationIdOrMemberEpoch)).IsEquivalentTo([5, 6]);
    }

    private static void SetMemberEpoch(KafkaConsumer<string, string> consumer, int epoch)
        => typeof(ConsumerCoordinator)
            .GetField("_generationId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(GetCoordinator(consumer), epoch);

    [Test]
    public async Task GroupConsumer_RevokedAndReassignedBeforeSync_RevocationDrained_IsNotOwnedUntilSyncStartsTheNewOwnership()
    {
        // A heartbeat revokes P0 and the next assigns it back; assignment sync drains the
        // revocation but has not published the new ownership yet. The coordinator's assignment
        // holds P0 again and its revocation queue is empty, yet records of the old ownership
        // must not count as owned.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        using var oldFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var oldRecord = CreateFetchedResult(oldFetch, offset: 2);
        var coordinator = GetCoordinator(consumer);

        SetCoordinatorAssignment(consumer);
        SetCoordinatorAssignment(consumer, OwnedP0);
        await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.RevocationPending);
        consumer.StoreOffset(oldRecord);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 7));
        await consumer.CommitAsync(CancellationToken.None);
        await Assert.That(requests).IsEmpty();

        // Sync publishes the reassignment: the old record's ownership has ended.
        PublishSynchronized(consumer, new[] { OwnedP0 }, coordinator.DrainedRevocationGenerations);
        await Assert.That(ownership.GetRecordOwnership(oldRecord)).IsEqualTo(RecordOwnership.Ended);
        using var newFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        await Assert.That(ownership.GetRecordOwnership(CreateFetchedResult(newFetch, offset: 2)))
            .IsEqualTo(RecordOwnership.Owned);
    }

    [Test]
    public async Task GroupConsumer_RevocationPublishedButNotYetEnqueuedWhenSyncDrains_StaysPending()
    {
        // An earlier revocation and assignment back of P0 is queued. A newer revocation of P0 has
        // taken its generation but is not enqueued yet when assignment sync drains the queue. The
        // sync applies only what it drained: the newer revocation must stay pending until a later
        // sync drains it.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        var coordinator = GetCoordinator(consumer);
        var notifyRevoking = typeof(ConsumerCoordinator)
            .GetMethod("NotifyRevoking", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var enqueue = typeof(ConsumerCoordinator)
            .GetMethod("EnqueueRevokedPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var p0 = new List<TopicPartition> { OwnedP0 };

        var (earlierSequence, earlier) = ((long, long))notifyRevoking.Invoke(coordinator, [p0])!;
        enqueue.Invoke(coordinator, [p0, earlier, earlierSequence]);
        var (newerSequence, newer) = ((long, long))notifyRevoking.Invoke(coordinator, [p0])!;
        await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);
        PublishSynchronized(consumer, new[] { OwnedP0 }, coordinator.DrainedRevocationGenerations);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var record = CreateFetchedResult(fetch, offset: 2);

        await Assert.That(ownership.GetRecordOwnership(record)).IsEqualTo(RecordOwnership.RevocationPending);

        // The newer revocation is enqueued and the next sync applies it.
        enqueue.Invoke(coordinator, [p0, newer, newerSequence]);
        await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);
        PublishSynchronized(consumer, new[] { OwnedP0 }, coordinator.DrainedRevocationGenerations);
        using var laterFetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        await Assert.That(ownership.GetRecordOwnership(CreateFetchedResult(laterFetch, offset: 2)))
            .IsEqualTo(RecordOwnership.Owned);
    }

    [Test]
    public async Task GroupConsumer_RecordFetchedAfterARevocationWasPublished_IsPendingBeforeTheCoordinatorMovesOn()
    {
        // The coordinator's hook published a revocation of P0, but the coordinator has not
        // published its new assignment (version) yet. A record fetched after the revocation took
        // its generation must not take the fast path to Owned.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        typeof(ConsumerCoordinator)
            .GetMethod("NotifyRevoking", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(GetCoordinator(consumer), [new List<TopicPartition> { OwnedP0 }]);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var record = CreateFetchedResult(fetch, offset: 2);

        await Assert.That(ownership.GetRecordOwnership(record)).IsEqualTo(RecordOwnership.RevocationPending);
        consumer.StoreOffset(record);
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(OwnedP0)).IsFalse();
    }

    [Test]
    [NotInParallel("PendingRevocationsSeam")]
    public async Task GroupConsumer_RevocationOfSeveralPartitions_BecomesVisibleForAllAtOnce()
    {
        // Until the revocation is published no revoked partition reads as pending, and once it is
        // every one does, on the fast and the slow path alike.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0, RevokedP1);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        using var fetch0 = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        using var fetch1 = PendingFetchData.Create("topic-a", 1, Array.Empty<RecordBatch>());
        var record0 = CreateFetchedResult(fetch0, offset: 2);
        var record1 = new ConsumeResult<string, string>(
            "topic-a", 1, 2, ReadOnlyMemory<byte>.Empty, true, ReadOnlyMemory<byte>.Empty, true,
            pooledHeaders: null, pooledHeaderCount: 0, headerOwner: fetch1, timestampMs: 0,
            TimestampType.NotAvailable, leaderEpoch: 0, keyDeserializer: null, valueDeserializer: null);
        RecordOwnership[]? beforePublication = null;
        Action<object> observe = instance =>
        {
            if (ReferenceEquals(instance, consumer))
                beforePublication = [ownership.GetRecordOwnership(record0), ownership.GetRecordOwnership(record1)];
        };

        KafkaConsumer<string, string>.BeforePendingRevocationsPublishedForTest += observe;
        try
        {
            typeof(ConsumerCoordinator)
                .GetMethod("NotifyRevoking", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(GetCoordinator(consumer), [new List<TopicPartition> { OwnedP0, RevokedP1 }]);
        }
        finally
        {
            KafkaConsumer<string, string>.BeforePendingRevocationsPublishedForTest -= observe;
        }

        await Assert.That(beforePublication).IsEquivalentTo([RecordOwnership.Owned, RecordOwnership.Owned]);
        await Assert.That(ownership.GetRecordOwnership(record0)).IsEqualTo(RecordOwnership.RevocationPending);
        await Assert.That(ownership.GetRecordOwnership(record1)).IsEqualTo(RecordOwnership.RevocationPending);
    }

    [Test]
    public async Task GroupConsumer_FetchGenerationsPastTwoToThe31_StillOrderOwnership()
    {
        // Every consumer in the process takes fetch generations; they must not wrap. Records
        // fetched 2^31 generations after their partition's ownership began are still owned, and
        // a later reassignment still ends the ownership of records fetched before it.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        AdvanceFetchGeneration((1L << 31) + 10);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var record = CreateFetchedResult(fetch, offset: 2);

        consumer.StoreOffset(record);
        await consumer.CommitAsync(CancellationToken.None);
        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 3, leaderEpoch: 0)
        ]);

        RevokeLikeAssignmentSync(consumer, OwnedP0);
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;
        await Assert.That(ownership.GetRecordOwnership(record)).IsEqualTo(RecordOwnership.Ended);
    }

    [Test]
    public async Task GroupConsumer_RecordFetchedMoreThanTwoToThe32GenerationsBeforeOwnershipStart_IsEnded()
    {
        // A record whose fetch is 2^32 + 1 generations older than its partition's current
        // ownership start: its low 32 bits alone would read as fetched after that start.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        RevokeLikeAssignmentSync(consumer, OwnedP0);
        PublishInitializedAssignment(consumer, OwnedP0);
        var ownershipStart = GetOwnershipStart(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var fetchedAt = ownershipStart + 1 - (1L << 32);
        typeof(PendingFetchData).GetField("_fetchGeneration", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(fetch, fetchedAt);
        typeof(PendingFetchData).GetField("_headerGeneration", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(fetch, (int)fetchedAt);
        var record = CreateFetchedResult(fetch, offset: 2);
        var ownership = (IConsumerRecordOwnership<string, string>)(object)consumer;

        await Assert.That(ownership.GetRecordOwnership(record)).IsEqualTo(RecordOwnership.Ended);
        consumer.StoreOffset(record);
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(OwnedP0)).IsFalse();
    }

    private static long GetOwnershipStart(KafkaConsumer<string, string> consumer, TopicPartition partition)
    {
        var starts = typeof(KafkaConsumer<string, string>)
            .GetField("_ownershipStartGenerations", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        return (long)starts.GetType().GetProperty("Item")!.GetValue(starts, [partition])!;
    }

    [Test]
    [NotInParallel("AssignmentSyncPublishedSeam")]
    public async Task AssignmentSync_RevokedAndReassignedPartition_StaleStoredOffsetIsNotCommittedDuringCleanup()
    {
        // P0 is revoked and assigned back before the consumer synchronizes. A commit that runs
        // while the sync has published the new assignment but not yet cleared the old ownership's
        // stored offset must not send that offset under the new ownership.
        var topicId = Guid.Parse("00000000-0000-0000-0000-00000000000d");
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator { Key = "group-a", NodeId = 0, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None }
                ]
            }));

        // The join assigns p0, the next heartbeat revokes it, the one after assigns it back.
        var heartbeats = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var heartbeat = Interlocked.Increment(ref heartbeats);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1 + heartbeat,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = new ConsumerGroupHeartbeatAssignment
                    {
                        AssignedTopicPartitions =
                        [
                            new ConsumerGroupHeartbeatTopicPartitions
                            {
                                TopicId = topicId,
                                Partitions = heartbeat == 2 ? [] : [0]
                            }
                        ],
                        PendingTopicPartitions = []
                    }
                });
            });

        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(new OffsetFetchResponse
            {
                Topics = callInfo.Arg<OffsetFetchRequest>()!.Topics!
                    .Select(static topic => new OffsetFetchResponseTopic
                    {
                        Name = topic.Name,
                        Partitions = topic.PartitionIndexes!
                            .Select(static index => new OffsetFetchResponsePartition
                            {
                                PartitionIndex = index,
                                CommittedOffset = 5,
                                ErrorCode = ErrorCode.None
                            })
                            .ToList()
                    })
                    .ToList()
            }));

        var committed = new List<TopicPartitionOffset>();
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<OffsetCommitRequest>()!;
                lock (committed)
                    committed.AddRange(GetCommittedOffsets(request));
                return ValueTask.FromResult(CreateResponse(request, ErrorCode.None));
            });

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "topic-a",
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata { PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0] }
                    ]
                }
            ]
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, 7, 7);
        metadataManager.SetApiVersion(ApiKey.OffsetCommit, OffsetCommitRequest.LowestSupportedVersion, 9);

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        consumer.Subscribe("topic-a");
        var coordinator = GetCoordinator(consumer);
        var ensureAssignment = typeof(KafkaConsumer<string, string>)
            .GetMethod("EnsureAssignmentAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await (ValueTask)ensureAssignment.Invoke(consumer, [timeout.Token])!;
        await coordinator.StopHeartbeatAsync();

        // Stored under the first ownership, never committed.
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 3));

        var sendHeartbeat = typeof(ConsumerCoordinator)
            .GetMethod("SendConsumerGroupHeartbeatAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var coordinatorId = (int)typeof(ConsumerCoordinator)
            .GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(coordinator)!;
        for (var revokeThenAssign = 0; revokeThenAssign < 2; revokeThenAssign++)
        {
            var pending = sendHeartbeat.Invoke(
                coordinator,
                [coordinatorId, false, true, new StrongBox<int>(), CancellationToken.None])!;
            await (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
        }

        // While the sync is between publication and cleanup, a commit runs on another thread. It
        // either waits for the sync or must not see the stale offset as owned; the sync does not
        // wait for it to finish.
        Task? concurrentCommit = null;
        Action<object> commitDuringCleanup = instance =>
        {
            if (!ReferenceEquals(instance, consumer) || concurrentCommit is not null)
                return;

            concurrentCommit = Task.Run(async () => await consumer.CommitAsync(CancellationToken.None));
            // Bounded: a commit that does not wait for the sync completes here.
            concurrentCommit.Wait(TimeSpan.FromMilliseconds(500));
        };

        KafkaConsumer<string, string>.AfterAssignmentSyncPublishedForTest += commitDuringCleanup;
        try
        {
            await (ValueTask)ensureAssignment.Invoke(consumer, [timeout.Token])!;
        }
        finally
        {
            KafkaConsumer<string, string>.AfterAssignmentSyncPublishedForTest -= commitDuringCleanup;
        }

        await Assert.That(concurrentCommit).IsNotNull();
        await concurrentCommit!.WaitAsync(timeout.Token);
        await Assert.That(committed.Any(static offset => offset.Partition == 0 && offset.Offset == 3)).IsFalse();
    }

    [Test]
    [NotInParallel("PendingRevocationsSeam")]
    public async Task Heartbeat_RevokingPartitions_PublishesTheNewMemberEpochOnlyAfterRecordingTheRevocation()
    {
        // A stored-offset commit reads the member epoch, then decides ownership. If a heartbeat
        // published its new epoch before recording its revocation, such a commit could send a
        // revoked partition's offset with an epoch the broker accepts.
        var topicId = Guid.Parse("00000000-0000-0000-0000-00000000000e");
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator { Key = "group-a", NodeId = 0, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None }
                ]
            }));

        // The join assigns p0 under epoch 2; the next heartbeat revokes it under epoch 3.
        var heartbeats = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var heartbeat = Interlocked.Increment(ref heartbeats);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1 + heartbeat,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = new ConsumerGroupHeartbeatAssignment
                    {
                        AssignedTopicPartitions =
                        [
                            new ConsumerGroupHeartbeatTopicPartitions { TopicId = topicId, Partitions = heartbeat == 1 ? [0] : [] }
                        ],
                        PendingTopicPartitions = []
                    }
                });
            });
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(new OffsetFetchResponse
            {
                Topics = callInfo.Arg<OffsetFetchRequest>()!.Topics!
                    .Select(static topic => new OffsetFetchResponseTopic
                    {
                        Name = topic.Name,
                        Partitions = topic.PartitionIndexes!
                            .Select(static index => new OffsetFetchResponsePartition
                            {
                                PartitionIndex = index,
                                CommittedOffset = 5,
                                ErrorCode = ErrorCode.None
                            })
                            .ToList()
                    })
                    .ToList()
            }));

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "topic-a",
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata { PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0] }
                    ]
                }
            ]
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, 7, 7);

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        consumer.Subscribe("topic-a");
        var coordinator = GetCoordinator(consumer);
        var ensureAssignment = typeof(KafkaConsumer<string, string>)
            .GetMethod("EnsureAssignmentAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await (ValueTask)ensureAssignment.Invoke(consumer, [timeout.Token])!;
        await coordinator.StopHeartbeatAsync();
        var epochBeforeRevocation = coordinator.GenerationId;

        int? epochWhenRevocationRecorded = null;
        Action<object> observe = instance =>
        {
            if (ReferenceEquals(instance, consumer))
                epochWhenRevocationRecorded = coordinator.GenerationId;
        };

        KafkaConsumer<string, string>.BeforePendingRevocationsPublishedForTest += observe;
        try
        {
            var sendHeartbeat = typeof(ConsumerCoordinator)
                .GetMethod("SendConsumerGroupHeartbeatAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var coordinatorId = (int)typeof(ConsumerCoordinator)
                .GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(coordinator)!;
            var pending = sendHeartbeat.Invoke(
                coordinator,
                [coordinatorId, false, true, new StrongBox<int>(), CancellationToken.None])!;
            await (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
        }
        finally
        {
            KafkaConsumer<string, string>.BeforePendingRevocationsPublishedForTest -= observe;
        }

        await Assert.That(epochBeforeRevocation).IsEqualTo(2);
        await Assert.That(epochWhenRevocationRecorded).IsEqualTo(2);
        await Assert.That(coordinator.GenerationId).IsEqualTo(3);
    }

    [Test]
    [NotInParallel("StoreOffsetSeam")]
    public async Task GroupConsumer_PartitionReassignedBetweenStoreOffsetCheckAndWrite_StaleOffsetIsNotCommitted()
    {
        // StoreOffset finds the record owned; before its write lands, the partition is revoked,
        // assigned back and synchronized. The write must not become the new ownership's offset.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);
        using var fetch = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var record = CreateFetchedResult(fetch, offset: 2);
        var reassigned = 0;
        Action<object> reassignBeforeWrite = instance =>
        {
            if (!ReferenceEquals(instance, consumer) || Interlocked.Exchange(ref reassigned, 1) != 0)
                return;

            RevokeLikeAssignmentSync(consumer, OwnedP0);
            PublishInitializedAssignment(consumer, OwnedP0);
        };

        KafkaConsumer<string, string>.AfterStoreOffsetOwnershipCheckedForTest += reassignBeforeWrite;
        try
        {
            consumer.StoreOffset(record);
        }
        finally
        {
            KafkaConsumer<string, string>.AfterStoreOffsetOwnershipCheckedForTest -= reassignBeforeWrite;
        }

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(Volatile.Read(ref reassigned)).IsEqualTo(1);
        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task GroupConsumer_ManyRevokeAndReassignCycles_WithExplicitCommitsOnly_KeepStoredOffsetsBounded()
    {
        // An application that commits only explicit offsets never runs the stored-offset commit
        // that drops entries of ended ownerships; the ownership changes themselves must.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        for (var cycle = 0; cycle < 10_000; cycle++)
        {
            PublishInitializedAssignment(consumer, OwnedP0);
            consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, cycle + 1, leaderEpoch: 1));
            if (cycle % 1_000 == 0)
                await consumer.CommitAsync([new TopicPartitionOffset("topic-a", 0, cycle)], CancellationToken.None);
            RevokeLikeAssignmentSync(consumer, OwnedP0);
        }

        PublishInitializedAssignment(consumer, OwnedP0);
        await Assert.That(GetMapCount(consumer, "_storedOffsetSlots")).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task GroupConsumer_ManyDistinctPartitionsAssignedAndRevoked_KeepsSlotsOnlyForTheAssignment()
    {
        // A pattern subscription over transient topics sees a stream of partitions it stores once
        // and then loses. Slots must follow the assignment, not every partition ever stored.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        for (var i = 0; i < 10_000; i++)
        {
            var partition = new TopicPartition("topic-a", i);
            PublishInitializedAssignment(consumer, partition);
            consumer.StoreOffset(new TopicPartitionOffset("topic-a", i, 5, leaderEpoch: 1));
            RevokeLikeAssignmentSync(consumer, partition);
            // A store for a partition the consumer no longer owns keeps nothing either.
            consumer.StoreOffset(new TopicPartitionOffset("topic-a", i, 6, leaderEpoch: 1));
        }

        await Assert.That(GetMapCount(consumer, "_storedOffsetSlots")).IsEqualTo(0);

        PublishInitializedAssignment(consumer, OwnedP0);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 7, leaderEpoch: 1));
        await consumer.CommitAsync(CancellationToken.None);
        await Assert.That(requests.SelectMany(r => r.Topics).SelectMany(t => t.Partitions)
            .Select(p => (p.PartitionIndex, p.CommittedOffset))).IsEquivalentTo([(0, 7L)]);
    }

    private static int GetMapCount(KafkaConsumer<string, string> consumer, string field)
        => ((System.Collections.ICollection)typeof(KafkaConsumer<string, string>)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!).Count;

    /// <summary>Moves the process-wide fetch generation forward by <paramref name="distance"/>.</summary>
    private static void AdvanceFetchGeneration(long distance)
    {
        var counter = typeof(PendingFetchData).GetField("s_fetchGeneration", BindingFlags.NonPublic | BindingFlags.Static)!;
        if (counter.FieldType == typeof(long))
        {
            typeof(PendingFetchData)
                .GetMethod("AdvanceFetchGenerationForTest", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [(long)counter.GetValue(null)! + distance]);
            return;
        }

        counter.SetValue(null, unchecked((int)((int)counter.GetValue(null)! + distance)));
    }

    [Test]
    public async Task GroupConsumer_RevocationCommit_StillCommitsRevokedPartitionsStoredOffsets()
    {
        // Revocation commits run before the sync removes the partition, while it is still owned.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None, OffsetCommitMode.Auto);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0, RevokedP1);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));

        await CommitRevokedOffsetsAsync(consumer, [RevokedP1]);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 3)
        ]);
    }

    [Test]
    public async Task ManualAssignmentConsumer_OffsetStoredForUnassignedPartition_IsStillCommitted()
    {
        // Without a subscription the application owns partition bookkeeping: unchanged.
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Assign(OwnedP0);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 3));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 3)
        ]);
    }

    [Test]
    public async Task GroupConsumer_ExplicitOffsetsForUnassignedPartition_AreStillCommitted()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        consumer.Subscribe("topic-a");
        PublishInitializedAssignment(consumer, OwnedP0);

        await consumer.CommitAsync([new TopicPartitionOffset("topic-a", 1, 3)], CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 3)
        ]);
    }

    [Test]
    public async Task AssignmentSync_NewPartitionWithOffsetStoredWhileUnowned_IsNotCommittableBeforeInitialization()
    {
        // A seek left while P1 was unowned would otherwise ride into the new ownership: the sync
        // publishes P1 (with the fetch position the seek set) before initialization replaces it.
        var topicId = Guid.Parse("00000000-0000-0000-0000-00000000000c");
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator { Key = "group-a", NodeId = 0, Host = "localhost", Port = 9092, ErrorCode = ErrorCode.None }
                ]
            }));

        // The join assigns p0; the next heartbeat adds p1.
        var heartbeats = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1 + Interlocked.Increment(ref heartbeats),
                HeartbeatIntervalMs = 60_000,
                Assignment = new ConsumerGroupHeartbeatAssignment
                {
                    AssignedTopicPartitions =
                    [
                        new ConsumerGroupHeartbeatTopicPartitions
                        {
                            TopicId = topicId,
                            Partitions = Volatile.Read(ref heartbeats) == 1 ? [0] : [0, 1]
                        }
                    ],
                    PendingTopicPartitions = []
                }
            }));

        // The committed-offset fetch runs inside the window: P1 is published but not initialized.
        KafkaConsumer<string, string>? consumer = null;
        var p1DirtyDuringInitialization = (long?)null;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<OffsetFetchRequest>()!;
                if (consumer!.Assignment.Contains(RevokedP1)
                    && GetDirtyStoredOffsets(consumer).TryGetValue(RevokedP1, out var dirty))
                {
                    p1DirtyDuringInitialization = dirty;
                }

                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Topics = request.Topics!
                        .Select(static topic => new OffsetFetchResponseTopic
                        {
                            Name = topic.Name,
                            Partitions = topic.PartitionIndexes!
                                .Select(static index => new OffsetFetchResponsePartition
                                {
                                    PartitionIndex = index,
                                    CommittedOffset = 5,
                                    ErrorCode = ErrorCode.None
                                })
                                .ToList()
                        })
                        .ToList()
                });
            });

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "topic-a",
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata { PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0] },
                        new PartitionMetadata { PartitionIndex = 1, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0] }
                    ]
                }
            ]
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, 7, 7);

        consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        await using var consumerLifetime = consumer;
        consumer.Subscribe("topic-a");
        var coordinator = GetCoordinator(consumer);
        var ensureAssignment = typeof(KafkaConsumer<string, string>)
            .GetMethod("EnsureAssignmentAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await (ValueTask)ensureAssignment.Invoke(consumer, [timeout.Token])!;
        await coordinator.StopHeartbeatAsync();

        // A stale seek for P1 while this member does not own it.
        consumer.Seek(new TopicPartitionOffset("topic-a", 1, 3));

        var sendHeartbeat = typeof(ConsumerCoordinator)
            .GetMethod("SendConsumerGroupHeartbeatAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var coordinatorId = (int)typeof(ConsumerCoordinator)
            .GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(coordinator)!;
        var pending = sendHeartbeat.Invoke(
            coordinator,
            [coordinatorId, false, true, new StrongBox<int>(), CancellationToken.None])!;
        await (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
        await (ValueTask)ensureAssignment.Invoke(consumer, [timeout.Token])!;

        await Assert.That(consumer.Assignment.Contains(RevokedP1)).IsTrue();
        await Assert.That(p1DirtyDuringInitialization).IsNull();
        await Assert.That(consumer.GetPosition(RevokedP1)).IsEqualTo(5);
    }

    private static async Task CommitByAsync(KafkaConsumer<string, string> consumer, string commit)
    {
        switch (commit)
        {
            case "CommitAsync":
                await consumer.CommitAsync(CancellationToken.None);
                break;
            case "AutoCommit":
                SetCoordinatorState(consumer, CoordinatorState.Stable);
                await RunAutoCommitCycleAsync(consumer);
                break;
            case "Close":
                // The close path's final commit.
                var method = typeof(KafkaConsumer<string, string>).GetMethod(
                    "CommitPendingOffsetsOnCloseAsync",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
                await (ValueTask)method.Invoke(consumer, [CancellationToken.None])!;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(commit), commit, null);
        }
    }

    /// <summary>Owned partitions as assignment sync leaves them: published and initialized.</summary>
    private static void PublishInitializedAssignment(
        KafkaConsumer<string, string> consumer,
        params TopicPartition[] partitions)
    {
        AddUninitializedAssignment(consumer, partitions);
        var setFetchPosition = typeof(KafkaConsumer<string, string>).GetMethod(
            "SetFetchPosition",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var partition in partitions)
        {
            SetPosition(consumer, partition, 0, dirty: false);
            setFetchPosition.Invoke(consumer, [partition, 0L]);
        }
    }

    private static void AddUninitializedAssignment(
        KafkaConsumer<string, string> consumer,
        params TopicPartition[] partitions)
    {
        var assignment = (HashSet<TopicPartition>)typeof(KafkaConsumer<string, string>)
            .GetField("_assignment", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        foreach (var partition in partitions)
            assignment.Add(partition);
        PublishAssignment(consumer);
    }

    /// <summary>The revocation part of assignment sync: publish without P, then clear P's state.</summary>
    private static void RevokeLikeAssignmentSync(KafkaConsumer<string, string> consumer, TopicPartition partition)
    {
        var assignment = (HashSet<TopicPartition>)typeof(KafkaConsumer<string, string>)
            .GetField("_assignment", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        assignment.Remove(partition);
        PublishAssignment(consumer);
        typeof(KafkaConsumer<string, string>)
            .GetMethod("RemovePartitionState", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [new[] { partition }, null]);
    }

    /// <summary>A record of P0 as the consumer delivers it from <paramref name="fetch"/>.</summary>
    private static ConsumeResult<string, string> CreateFetchedResult(PendingFetchData fetch, long offset)
        => new(
            "topic-a",
            0,
            offset,
            ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            headerOwner: fetch,
            timestampMs: 0,
            TimestampType.NotAvailable,
            leaderEpoch: 0,
            keyDeserializer: null,
            valueDeserializer: null);

    /// <summary>
    /// Publishes the consumer's assignment, which the coordinator's assignment matches, and
    /// completes the sync as assignment sync does.
    /// </summary>
    private static void PublishAssignment(KafkaConsumer<string, string> consumer)
    {
        var assignment = (HashSet<TopicPartition>)typeof(KafkaConsumer<string, string>)
            .GetField("_assignment", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        SetCoordinatorAssignment(consumer, [.. assignment]);
        PublishAsSync(consumer, GetCoordinator(consumer));
        typeof(KafkaConsumer<string, string>)
            .GetField("_lastCoordinatorAssignmentVersion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(consumer, GetCoordinator(consumer).AssignmentVersion);
    }

    /// <summary>
    /// The coordinator's assignment, which a heartbeat changes (with a new assignment version)
    /// before assignment sync.
    /// </summary>
    private static void SetCoordinatorAssignment(KafkaConsumer<string, string> consumer, params TopicPartition[] partitions)
    {
        var coordinator = GetCoordinator(consumer);
        var assigned = typeof(ConsumerCoordinator)
            .GetField("_assignedPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        // A heartbeat publishes the revocation of dropped partitions before the new assignment.
        var revoked = ((HashSet<TopicPartition>)assigned.GetValue(coordinator)!)
            .Where(partition => !partitions.Contains(partition))
            .ToList();
        if (revoked.Count != 0)
            RevokeInCoordinator(coordinator, revoked);

        assigned.SetValue(coordinator, new HashSet<TopicPartition>(partitions));
        var version = typeof(ConsumerCoordinator)
            .GetField("_assignmentVersion", BindingFlags.NonPublic | BindingFlags.Instance)!;
        version.SetValue(coordinator, (int)version.GetValue(coordinator)! + 1);
    }

    /// <summary>
    /// The coordinator revokes partitions: it publishes the revocation (taking its generation),
    /// then enqueues it for assignment sync.
    /// </summary>
    private static long RevokeInCoordinator(ConsumerCoordinator coordinator, List<TopicPartition> revoked)
    {
        var (sequence, generation) = ((long, long))typeof(ConsumerCoordinator)
            .GetMethod("NotifyRevoking", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(coordinator, [revoked])!;
        EnqueueRevocation(coordinator, revoked, generation, sequence);
        return generation;
    }

    private static void EnqueueRevocation(
        ConsumerCoordinator coordinator,
        List<TopicPartition> revoked,
        long generation,
        long revocationSequence)
        => typeof(ConsumerCoordinator)
            .GetMethod("EnqueueRevokedPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(coordinator, [revoked, generation, revocationSequence]);

    /// <summary>The publication of assignment sync: publish, then apply the drained revocations.</summary>
    private static void PublishSynchronized(
        KafkaConsumer<string, string> consumer,
        TopicPartition[]? reassigned,
        IReadOnlyDictionary<TopicPartition, long>? drained)
    {
        typeof(KafkaConsumer<string, string>)
            .GetMethod("PublishAssignmentSnapshotCore", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [reassigned]);
        if (drained is not null)
        {
            typeof(KafkaConsumer<string, string>)
                .GetMethod("ForgetDrainedRevocations", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(consumer, [drained]);
        }
    }

    /// <summary>Assignment sync: drains the coordinator's revocations and publishes, applying them.</summary>
    private static void PublishAsSync(KafkaConsumer<string, string> consumer, ConsumerCoordinator coordinator)
    {
        coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        PublishSynchronized(consumer, null, coordinator.DrainedRevocationGenerations);
    }

    private static IReadOnlyDictionary<TopicPartition, long> GetDirtyStoredOffsets(
        KafkaConsumer<string, string> consumer)
        => consumer.DirtyStoredOffsetsForTest;
}
