using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Amazon DynamoDB implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>All items live in one table under <see cref="DynamoDbOutboxOptions.KeyPrefix"/>; see
/// the package documentation for the item layout. Enqueue with
/// <see cref="IDynamoDbOutboxWriter"/>, which shares that layout.</para>
/// <para><b>Leases:</b> each lease is one conditional <c>UpdateItem</c>, the only atomic
/// primitive the contract needs. Every refused conditional write is billed, and the AWS SDK
/// reports it as an error, so acquisition first reads every lease and every heartbeat with
/// one strongly consistent query, plans the writes of every active relay from that read, and
/// carries out only its own. Relays that read the same state never write the same lease, so a
/// refusal means a real race during a membership change, not steady-state noise.</para>
/// <para><b>Clocks:</b> lease expiry and heartbeat age compare timestamps written by the
/// relay hosts, as <see cref="IOutboxStore.AcquireBucketLeasesAsync"/> describes.</para>
/// <para>This store runs on the relay's polling cadence, not on a Kafka hot path.</para>
/// </remarks>
public sealed partial class DynamoDbOutboxStore : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxLeaseOwnershipStore,
    IOutboxMetricsStore
{
    /// <summary>
    /// Dead heartbeats are pruned once per this many acquisition rounds; records older than
    /// this many lease durations are removed.
    /// </summary>
    private const int HeartbeatPruneFactor = 10;

    private const int BatchWriteLimit = 25;
    private const int MaxBatchWriteAttempts = 8;

    /// <summary>
    /// Gaps probed per fetch. Many writers in flight leave many gaps in one batch; the bound
    /// keeps the probes a fraction of the fetch they guard.
    /// </summary>
    private const int MaxGapProbes = 8;

    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbOutboxOptions _options;
    private readonly DynamoDbOutboxSchema _schema;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    // Last sequence number returned per bucket. The relay serializes fetches, so no lock.
    private readonly long[] _lastSequence;
    private int _acquisitionRound;

    /// <summary>
    /// Creates the store. The caller owns <paramref name="client"/>.
    /// </summary>
    public DynamoDbOutboxStore(
        IAmazonDynamoDB client,
        DynamoDbOutboxOptions options,
        TimeProvider? timeProvider = null,
        ILogger<DynamoDbOutboxStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
        _schema = new DynamoDbOutboxSchema(options);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DynamoDbOutboxStore>.Instance;
        _lastSequence = new long[options.BucketCount];
    }

    // The hint orders blind probes. This store reads every lease before it writes, which
    // tells it more than the hint can, so both acquisitions are the same.
    ValueTask<IReadOnlyList<int>> IOutboxLeaseOwnershipStore.AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken) => AcquireBucketLeasesAsync(request, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfBucketCountDiffers(request);

        // One timestamp for the whole round: a heartbeat and the leases written with it then
        // lapse on the same tick, so a dead relay's share and buckets free up together.
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var expiry = now + request.LeaseDuration.Ticks;

        // Before the read, so peers see this relay as early as possible.
        await RecordHeartbeatAsync(request.RelayId, now, cancellationToken).ConfigureAwait(false);

        var coordination = await ReadCoordinationAsync(request, now, cancellationToken).ConfigureAwait(false);
        var plan = DynamoDbLeasePlanner.Plan(
            request.BucketCount, coordination.ActiveRelayIds, request.RelayId, coordination.Leases);

        var owned = new List<int>(plan.Keep.Count + plan.Claim.Count);
        var write = new LeaseWrite(request.RelayId, now, expiry);
        long SeenExpiry(int bucket) => coordination.SeenExpiry[bucket];

        // Renew first: these leases are the ones being published and the closest to expiry.
        var kept = await WriteLeasesAsync(plan.Keep, KeepLeaseRequest, SeenExpiry, write, cancellationToken)
            .ConfigureAwait(false);
        Collect(plan.Keep, kept, owned, request.RelayId, claimed: false);

        await WriteLeasesAsync(plan.Release, ReleaseLeaseRequest, SeenExpiry, write, cancellationToken)
            .ConfigureAwait(false);

        var claimed = await WriteLeasesAsync(plan.Claim, ClaimLeaseRequest, SeenExpiry, write, cancellationToken)
            .ConfigureAwait(false);
        Collect(plan.Claim, claimed, owned, request.RelayId, claimed: true);

        // Pruning is housekeeping, not correctness; run it occasionally instead of per round.
        if (++_acquisitionRound % HeartbeatPruneFactor == 0)
            await PruneHeartbeatsAsync(coordination.StaleRelayIds, now, request, cancellationToken).ConfigureAwait(false);

        owned.Sort();
        return owned;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RenewBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(buckets);
        if (buckets.Count == 0)
            return true;

        for (var index = 0; index < buckets.Count; index++)
        {
            if ((uint)buckets[index] >= (uint)request.BucketCount)
                return false;
        }

        var now = _timeProvider.GetUtcNow().UtcTicks;
        var expiry = now + request.LeaseDuration.Ticks;
        var renewed = await WriteLeasesAsync(
            buckets, RenewLeaseRequest, static _ => 0, new LeaseWrite(request.RelayId, now, expiry), cancellationToken)
            .ConfigureAwait(false);

        // Matching leases may already be extended after a partial mismatch. The relay drops
        // its local ownership and reacquires valid buckets on the next cycle, rather than
        // continuing publication on a partially renewed set.
        if (Array.IndexOf(renewed, false) >= 0)
            return false;

        await RecordHeartbeatAsync(request.RelayId, now, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask ReleaseBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Released by owner, not by previousBuckets: the read also finds leases that an
        // acquisition claimed before it failed, and stale lease items beyond the bucket count.
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var owned = new List<int>();
        var seenExpiry = new Dictionary<int, long>();
        await foreach (var item in QueryCoordinationAsync(DynamoDbOutboxSchema.LeaseSortKeyPrefix, cancellationToken)
            .ConfigureAwait(false))
        {
            if (TryReadLease(item, out var bucket, out var owner, out var expiresAt) && owner == request.RelayId)
            {
                owned.Add(bucket);
                seenExpiry[bucket] = expiresAt;
            }
        }

        // The owner guard leaves a lease alone that a peer took over after it expired.
        await WriteLeasesAsync(
            owned, ReleaseLeaseRequest, bucket => seenExpiry[bucket], new LeaseWrite(request.RelayId, now, now),
            cancellationToken).ConfigureAwait(false);

        // Peers stop dividing the buckets by a relay count that still includes this one.
        // If this request fails, the heartbeat ages out as it does without a release.
        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _schema.TableName,
            Key = _schema.RelayKey(request.RelayId)
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Probes each bucket with an eventually consistent one-item query. A stale answer costs
    /// one empty fetch or one poll interval; it halves the read cost of an idle relay.
    /// </summary>
    public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        if (buckets.Count == 0)
            return [];

        var hasPending = new bool[buckets.Count];
        await BoundedConcurrency.ForAsync(buckets.Count, _options.MaxConcurrency, async (index, token) =>
        {
            var response = await _client.QueryAsync(HeadQuery(buckets[index], _schema.SortKeyName), token)
                .ConfigureAwait(false);
            hasPending[index] = response.Items is { Count: > 0 };
        }, cancellationToken).ConfigureAwait(false);

        var pending = new List<int>();
        for (var index = 0; index < buckets.Count; index++)
        {
            if (hasPending[index])
                pending.Add(buckets[index]);
        }

        pending.Sort();
        return pending;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
        int bucket,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bucket);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);

        var messages = await QueryBatchAsync(bucket, maxCount, cancellationToken).ConfigureAwait(false);
        // Once: the read that follows a committed write sees all of it.
        if (await HasLateArrivalAsync(bucket, messages, cancellationToken).ConfigureAwait(false))
            messages = await QueryBatchAsync(bucket, maxCount, cancellationToken).ConfigureAwait(false);

        if (messages.Count > 0 && bucket < _lastSequence.Length)
            _lastSequence[bucket] = messages[^1].Id;
        return messages;
    }

    private async Task<List<OutboxMessage>> QueryBatchAsync(int bucket, int maxCount, CancellationToken cancellationToken)
    {
        var messages = new List<OutboxMessage>();
        Dictionary<string, AttributeValue>? startKey = null;
        do
        {
            // Strongly consistent: an eventually consistent read could return rows that
            // MarkPublishedAsync already deleted, and the relay would publish them again.
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _schema.TableName,
                KeyConditionExpression = "#pk = :pk",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _schema.PartitionKeyName },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new() { S = _schema.MessagePartition(bucket) }
                },
                ConsistentRead = true,
                ScanIndexForward = true,
                Limit = maxCount - messages.Count,
                ExclusiveStartKey = startKey
            }, cancellationToken).ConfigureAwait(false);

            foreach (var item in response.Items ?? [])
            {
                ThrowIfWriterBucketCountDiffers(bucket, item);
                messages.Add(_schema.FromItem(bucket, item));
            }

            // A page ends at 1 MB even when fewer than Limit items were read.
            startKey = response.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
        }
        while (startKey is not null && messages.Count < maxCount);

        return messages;
    }

    /// <summary>
    /// Finds a message that committed into a gap of <paramref name="messages"/> while the
    /// query was running.
    /// </summary>
    /// <remarks>
    /// A query is only read-committed against <c>TransactWriteItems</c>: it can pass the
    /// position of message n before a transaction commits and still return n+1 of the same
    /// transaction. Publishing that batch would put n+1 ahead of n. A gap is normal (an
    /// abandoned reservation, a writer that has reserved but not committed), so each gap gets
    /// one cheap range probe, and only a probe that finds a message costs a second fetch.
    /// A writer that has reserved but not committed is outside this: its message does not
    /// exist yet, and writers that do not serialize their commits have no order to keep.
    /// </remarks>
    private async Task<bool> HasLateArrivalAsync(int bucket, List<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        // The end of the previous batch is known only while this store keeps reading the
        // bucket. After a start or a handover it is not, and everything below the head is
        // the gap: one probe per bucket for the life of the store. A stale value from an
        // earlier ownership is merely a wider gap.
        var expected = bucket < _lastSequence.Length && _lastSequence[bucket] > 0 ? _lastSequence[bucket] + 1 : 1;
        var probes = 0;
        foreach (var message in messages)
        {
            if (message.Id > expected)
            {
                if (++probes > MaxGapProbes)
                    return false;
                if (await ExistsBetweenAsync(bucket, expected, message.Id - 1, cancellationToken).ConfigureAwait(false))
                    return true;
            }

            expected = Math.Max(expected, message.Id + 1);
        }

        return false;
    }

    private async Task<bool> ExistsBetweenAsync(int bucket, long first, long last, CancellationToken cancellationToken)
    {
        var response = await _client.QueryAsync(new QueryRequest
        {
            TableName = _schema.TableName,
            KeyConditionExpression = "#pk = :pk AND #sk BETWEEN :first AND :last",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#pk"] = _schema.PartitionKeyName,
                ["#sk"] = _schema.SortKeyName
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = _schema.MessagePartition(bucket) },
                [":first"] = new() { S = DynamoDbOutboxSchema.MessageSortKey(first) },
                [":last"] = new() { S = DynamoDbOutboxSchema.MessageSortKey(last) }
            },
            ConsistentRead = true,
            Select = Select.COUNT,
            Limit = 1
        }, cancellationToken).ConfigureAwait(false);
        return response.Count > 0;
    }

    /// <inheritdoc />
    public async ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> publishedMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publishedMessages);

        // In ascending chunks, one after another: if a chunk fails, the rows that remain are
        // a suffix, so the retry republishes them in order.
        for (var offset = 0; offset < publishedMessages.Count; offset += BatchWriteLimit)
        {
            var count = Math.Min(BatchWriteLimit, publishedMessages.Count - offset);
            var deletes = new List<WriteRequest>(count);
            for (var index = offset; index < offset + count; index++)
            {
                deletes.Add(new WriteRequest
                {
                    DeleteRequest = new DeleteRequest { Key = _schema.MessageKey(bucket, publishedMessages[index].Id) }
                });
            }

            await DeleteBatchAsync(deletes, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Counts each bucket up to <see cref="DynamoDbOutboxOptions.PendingCountLimit"/> and
    /// reports the earliest creation time among the bucket heads. DynamoDB has no aggregate:
    /// the head of a bucket is its oldest enqueue, which is the row a stalled outbox is stuck on.
    /// </summary>
    public async ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new long[_options.BucketCount];
        var oldest = new long[_options.BucketCount];
        await BoundedConcurrency.ForAsync(_options.BucketCount, _options.MaxConcurrency, async (bucket, token) =>
        {
            var head = await _client.QueryAsync(HeadQuery(bucket, DynamoDbOutboxSchema.CreatedAtUtc), token)
                .ConfigureAwait(false);
            if (head.Items is not { Count: > 0 } items)
                return;

            if (DynamoDbOutboxSchema.TryReadNumber(items[0], DynamoDbOutboxSchema.CreatedAtUtc, out var createdAt))
                oldest[bucket] = createdAt;
            counts[bucket] = await CountBucketAsync(bucket, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        long pendingCount = 0;
        long oldestTicks = 0;
        for (var bucket = 0; bucket < counts.Length; bucket++)
        {
            pendingCount += counts[bucket];
            if (oldest[bucket] != 0 && (oldestTicks == 0 || oldest[bucket] < oldestTicks))
                oldestTicks = oldest[bucket];
        }

        return new OutboxPendingMetrics(
            pendingCount, oldestTicks == 0 ? null : new DateTimeOffset(oldestTicks, TimeSpan.Zero));
    }

    private async Task<long> CountBucketAsync(int bucket, CancellationToken cancellationToken)
    {
        long count = 0;
        Dictionary<string, AttributeValue>? startKey = null;
        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _schema.TableName,
                KeyConditionExpression = "#pk = :pk",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _schema.PartitionKeyName },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new() { S = _schema.MessagePartition(bucket) }
                },
                Select = Select.COUNT,
                Limit = (int)(_options.PendingCountLimit - count),
                ExclusiveStartKey = startKey
            }, cancellationToken).ConfigureAwait(false);

            count += response.Count ?? 0;
            startKey = response.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
        }
        while (startKey is not null && count < _options.PendingCountLimit);

        return count;
    }

    private QueryRequest HeadQuery(int bucket, string projectedAttribute) => new()
    {
        TableName = _schema.TableName,
        KeyConditionExpression = "#pk = :pk",
        ProjectionExpression = "#projected",
        ExpressionAttributeNames = new Dictionary<string, string>
        {
            ["#pk"] = _schema.PartitionKeyName,
            ["#projected"] = projectedAttribute
        },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new() { S = _schema.MessagePartition(bucket) }
        },
        ScanIndexForward = true,
        Limit = 1
    };

    private async Task DeleteBatchAsync(List<WriteRequest> deletes, CancellationToken cancellationToken)
    {
        var pending = deletes;
        for (var attempt = 1; ; attempt++)
        {
            var response = await _client.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>> { [_schema.TableName] = pending }
            }, cancellationToken).ConfigureAwait(false);

            // DynamoDB returns what it throttled instead of failing the request.
            if (response.UnprocessedItems is null
                || !response.UnprocessedItems.TryGetValue(_schema.TableName, out var unprocessed)
                || unprocessed.Count == 0)
            {
                return;
            }

            if (attempt == MaxBatchWriteAttempts)
            {
                // The rows stay pending and are published again: duplicates, never loss.
                throw new InvalidOperationException(
                    $"DynamoDB left {unprocessed.Count} published outbox message(s) undeleted after " +
                    $"{MaxBatchWriteAttempts} attempts. The table is throttling writes.");
            }

            pending = unprocessed;
            var ceiling = Math.Min(1000, 25 << attempt);
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(ceiling / 2, ceiling + 1)), _timeProvider,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void ThrowIfBucketCountDiffers(OutboxLeaseRequest request)
    {
        if (request.BucketCount != _options.BucketCount)
        {
            throw new OutboxMisconfigurationException(
                $"OutboxRelayOptions.BucketCount is {request.BucketCount} but DynamoDbOutboxOptions.BucketCount is " +
                $"{_options.BucketCount}. The writer stamps and validates messages with the latter, so the two must match.");
        }
    }

    /// <summary>
    /// A writer with another bucket count hashes the same key to another bucket, so that
    /// key's messages lose their order, and a writer with a larger count also fills buckets
    /// no relay claims. DynamoDB cannot list those partitions cheaply, but such a writer
    /// spreads its messages over the claimed buckets too, where the stamp gives it away.
    /// </summary>
    private void ThrowIfWriterBucketCountDiffers(int bucket, Dictionary<string, AttributeValue> item)
    {
        if (DynamoDbOutboxSchema.TryReadNumber(item, DynamoDbOutboxSchema.BucketCount, out var writerBucketCount)
            && writerBucketCount != _options.BucketCount)
        {
            throw new OutboxMisconfigurationException(
                $"Outbox bucket {bucket} holds a message enqueued with a bucket count of {writerBucketCount}, but this " +
                $"relay uses {_options.BucketCount}. Messages of one key would be published from different buckets, " +
                "and buckets beyond the relay's count would never be published. Align the bucket count across all " +
                "writers and relays, and drain the table before changing it.");
        }
    }
}
