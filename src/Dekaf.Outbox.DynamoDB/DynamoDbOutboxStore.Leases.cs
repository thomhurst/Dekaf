using System.Globalization;
using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox.DynamoDB;

public sealed partial class DynamoDbOutboxStore
{
    /// <summary>The relay and the single timestamp of one round of lease writes.</summary>
    private readonly record struct LeaseWrite(string RelayId, long Now, long Expiry);

    /// <param name="seenExpiry">The expiry this relay read for the lease; zero where the
    /// request does not depend on a read.</param>
    private delegate UpdateItemRequest LeaseRequestFactory(int bucket, long seenExpiry, in LeaseWrite write);

    /// <param name="Leases">Owner and expiry state per bucket, for the planner.</param>
    /// <param name="SeenExpiry">The expiry read per bucket, which fences this round's writes.</param>
    private readonly record struct Coordination(
        DynamoDbLeaseState[] Leases, long[] SeenExpiry, List<string> ActiveRelayIds, List<string> StaleRelayIds);

    /// <summary>
    /// Reads every lease and every heartbeat with one strongly consistent query. Both kinds
    /// share a partition, so relays that read at the same moment plan from the same state.
    /// </summary>
    private async Task<Coordination> ReadCoordinationAsync(
        OutboxLeaseRequest request, long now, CancellationToken cancellationToken)
    {
        // A bucket without an item was never claimed: free, like a released one.
        var leases = new DynamoDbLeaseState[request.BucketCount];
        var seenExpiry = new long[request.BucketCount];
        var activeRelayIds = new List<string>();
        var staleRelayIds = new List<string>();
        var activeCutoff = now - request.LeaseDuration.Ticks;
        var staleCutoff = now - (request.LeaseDuration.Ticks * HeartbeatPruneFactor);

        await foreach (var item in QueryCoordinationAsync(sortKeyPrefix: null, cancellationToken).ConfigureAwait(false))
        {
            var sortKey = item[_schema.SortKeyName].S;
            if (sortKey.StartsWith(DynamoDbOutboxSchema.RelaySortKeyPrefix, StringComparison.Ordinal))
            {
                var relayId = sortKey[DynamoDbOutboxSchema.RelaySortKeyPrefix.Length..];
                DynamoDbOutboxSchema.TryReadNumber(item, DynamoDbOutboxSchema.LastSeenUtc, out var lastSeen);
                // A relay that released its buckets is gone from this moment, whatever its
                // timestamp says: the record only stays behind to fence its own stragglers,
                // which is why it is also pruned as soon as it could no longer count as active.
                var stopped = item.ContainsKey(DynamoDbOutboxSchema.StoppedAtUtc);
                // Strictly newer than the cutoff, as a lease is expired at its expiry tick:
                // the last round of a dead relay stops counting when its leases free up.
                if (!stopped && lastSeen > activeCutoff)
                    activeRelayIds.Add(relayId);
                else if (lastSeen < (stopped ? activeCutoff : staleCutoff))
                    staleRelayIds.Add(relayId);
            }
            // Lease items beyond the range are left over from a larger bucket count. They
            // must not count towards this relay's share or come back as owned buckets.
            // A lease without an expiry is not one this store wrote; the claim condition
            // accepts it, so it is free rather than stuck with whoever it names.
            else if (TryReadLease(item, out var bucket, out var owner, out var expiresAt) && bucket < request.BucketCount)
            {
                leases[bucket] = new DynamoDbLeaseState(owner, expiresAt <= now);
                seenExpiry[bucket] = expiresAt;
            }
        }

        return new Coordination(leases, seenExpiry, activeRelayIds, staleRelayIds);
    }

    private async IAsyncEnumerable<Dictionary<string, AttributeValue>> QueryCoordinationAsync(
        string? sortKeyPrefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string> { ["#pk"] = _schema.PartitionKeyName };
        var values = new Dictionary<string, AttributeValue> { [":pk"] = new() { S = _schema.CoordinationPartition } };
        var keyCondition = "#pk = :pk";
        if (sortKeyPrefix is not null)
        {
            names["#sk"] = _schema.SortKeyName;
            values[":prefix"] = new AttributeValue { S = sortKeyPrefix };
            keyCondition += " AND begins_with(#sk, :prefix)";
        }

        Dictionary<string, AttributeValue>? startKey = null;
        do
        {
            var response = await _client.QueryAsync(new QueryRequest
            {
                TableName = _schema.TableName,
                KeyConditionExpression = keyCondition,
                ExpressionAttributeNames = names,
                ExpressionAttributeValues = values,
                ConsistentRead = true,
                ExclusiveStartKey = startKey
            }, cancellationToken).ConfigureAwait(false);

            foreach (var item in response.Items ?? [])
                yield return item;

            startKey = response.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
        }
        while (startKey is not null);
    }

    /// <returns>False for an item that is not a lease. <paramref name="owner"/> is null for
    /// a released lease and for one without a readable expiry.</returns>
    private bool TryReadLease(Dictionary<string, AttributeValue> item, out int bucket, out string? owner, out long expiresAt)
    {
        bucket = -1;
        owner = null;
        expiresAt = 0;
        if (!item.TryGetValue(_schema.SortKeyName, out var sortKey)
            || sortKey.S is not { } value
            || !value.StartsWith(DynamoDbOutboxSchema.LeaseSortKeyPrefix, StringComparison.Ordinal)
            || !int.TryParse(value.AsSpan(DynamoDbOutboxSchema.LeaseSortKeyPrefix.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out bucket))
        {
            return false;
        }

        if (DynamoDbOutboxSchema.TryReadNumber(item, DynamoDbOutboxSchema.ExpiresAtUtc, out expiresAt))
            owner = item.TryGetValue(DynamoDbOutboxSchema.Owner, out var ownerValue) ? ownerValue.S : null;
        return true;
    }

    /// <summary>
    /// Writes one lease per bucket with bounded concurrency.
    /// </summary>
    /// <returns>Per bucket, whether DynamoDB accepted the conditional write.</returns>
    private async Task<bool[]> WriteLeasesAsync(
        IReadOnlyList<int> buckets, LeaseRequestFactory createRequest, Func<int, long> seenExpiry, LeaseWrite write,
        CancellationToken cancellationToken)
    {
        var written = new bool[buckets.Count];
        await BoundedConcurrency.ForAsync(buckets.Count, _options.MaxConcurrency, async (index, token) =>
        {
            try
            {
                var bucket = buckets[index];
                await _client.UpdateItemAsync(createRequest(bucket, seenExpiry(bucket), write), token).ConfigureAwait(false);
                written[index] = true;
            }
            catch (ConditionalCheckFailedException)
            {
                // The condition is the authority. Somebody wrote this lease after the read.
            }
        }, cancellationToken).ConfigureAwait(false);
        return written;
    }

    private void Collect(IReadOnlyList<int> buckets, bool[] written, List<int> owned, string relayId, bool claimed)
    {
        for (var index = 0; index < buckets.Count; index++)
        {
            if (written[index])
                owned.Add(buckets[index]);
            else if (claimed)
                LogClaimRefused(relayId, buckets[index]);
            else
                LogLeaseTakenOver(relayId, buckets[index]);
        }
    }

    /// <summary>
    /// Leaves out the leases this round would move backwards: the host's clock was set back
    /// by more than a renew interval since the round that wrote them. The relay counts a kept
    /// lease as valid for a whole lease duration from now, which holds for the expiry this
    /// round writes but not for an earlier one it left in place, so such a bucket is not
    /// reported as owned. Its lease still names this relay, so nobody else takes it; it is
    /// kept again once the clock has caught up, or claimed afresh once it has lapsed. The
    /// write is not sent at all, because its condition would refuse it.
    /// </summary>
    private IReadOnlyList<int> LeasesThatMoveForward(
        IReadOnlyList<int> keep, long[] seenExpiry, long expiry, string relayId)
    {
        List<int>? forward = null;
        for (var index = 0; index < keep.Count; index++)
        {
            var bucket = keep[index];
            if (seenExpiry[bucket] <= expiry)
            {
                forward?.Add(bucket);
                continue;
            }

            if (forward is null)
            {
                forward = new List<int>(keep.Count);
                for (var earlier = 0; earlier < index; earlier++)
                    forward.Add(keep[earlier]);
            }

            LogLeaseNotMovedBackwards(relayId, bucket);
        }

        return forward ?? keep;
    }

    // The expiry doubles as the lease's version. A request that this relay gave up on (a
    // cancelled sibling of a failed write, a timed-out attempt) can still reach DynamoDB
    // later. Requiring the expiry that this round read makes such a straggler fail, instead
    // of releasing or shortening a lease that a later round renewed.
    private UpdateItemRequest KeepLeaseRequest(int bucket, long seenExpiry, in LeaseWrite write) => new()
    {
        TableName = _schema.TableName,
        Key = _schema.LeaseKey(bucket),
        UpdateExpression = "SET #expires = :expiry",
        // No expiry check against the clock: a lease that lapsed but still names this relay
        // was taken by nobody. The expiry only moves forward, as the heartbeat's timestamp
        // does: peers may already have planned around the later one.
        ConditionExpression = "#owner = :me AND #expires = :seen AND #expires <= :expiry",
        ExpressionAttributeNames = LeaseAttributeNames(),
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":me"] = new() { S = write.RelayId },
            [":seen"] = DynamoDbOutboxSchema.Number(seenExpiry),
            [":expiry"] = DynamoDbOutboxSchema.Number(write.Expiry)
        }
    };

    private UpdateItemRequest ClaimLeaseRequest(int bucket, long seenExpiry, in LeaseWrite write) => new()
    {
        TableName = _schema.TableName,
        Key = _schema.LeaseKey(bucket),
        UpdateExpression = "SET #owner = :me, #expires = :expiry",
        // A missing item has no owner either, so the first claim of a bucket creates its lease.
        ConditionExpression = "attribute_not_exists(#owner) OR attribute_not_exists(#expires) OR #expires <= :now",
        ExpressionAttributeNames = LeaseAttributeNames(),
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":me"] = new() { S = write.RelayId },
            [":now"] = DynamoDbOutboxSchema.Number(write.Now),
            [":expiry"] = DynamoDbOutboxSchema.Number(write.Expiry)
        }
    };

    private UpdateItemRequest RenewLeaseRequest(int bucket, long seenExpiry, in LeaseWrite write) => new()
    {
        TableName = _schema.TableName,
        Key = _schema.LeaseKey(bucket),
        UpdateExpression = "SET #expires = :expiry",
        // Never revive an expired lease during a publish, even if its owner has not changed:
        // the relay cannot prove that it owned the bucket throughout. There is no read to
        // fence against here, so a straggler is stopped from moving the expiry backwards.
        ConditionExpression = "#owner = :me AND #expires > :now AND #expires <= :expiry",
        ExpressionAttributeNames = LeaseAttributeNames(),
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":me"] = new() { S = write.RelayId },
            [":now"] = DynamoDbOutboxSchema.Number(write.Now),
            [":expiry"] = DynamoDbOutboxSchema.Number(write.Expiry)
        }
    };

    private UpdateItemRequest ReleaseLeaseRequest(int bucket, long seenExpiry, in LeaseWrite write) => new()
    {
        TableName = _schema.TableName,
        Key = _schema.LeaseKey(bucket),
        UpdateExpression = "SET #expires = :now REMOVE #owner",
        ConditionExpression = "#owner = :me AND #expires = :seen",
        ExpressionAttributeNames = LeaseAttributeNames(),
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":me"] = new() { S = write.RelayId },
            [":seen"] = DynamoDbOutboxSchema.Number(seenExpiry),
            [":now"] = DynamoDbOutboxSchema.Number(write.Now)
        }
    };

    private static Dictionary<string, string> LeaseAttributeNames() => new(2)
    {
        ["#owner"] = DynamoDbOutboxSchema.Owner,
        ["#expires"] = DynamoDbOutboxSchema.ExpiresAtUtc
    };

    /// <returns>False when the record already carries a later timestamp.</returns>
    private Task<bool> RecordHeartbeatAsync(string relayId, long now, CancellationToken cancellationToken) =>
        WriteRelayRecordAsync(relayId, now, stopped: false, cancellationToken);

    /// <summary>
    /// The timestamp of the stopped record: later than every heartbeat this store sent, so
    /// none of them can replace it. The clock alone does not promise that. A stop in the tick
    /// of the round it cancels, or after the host's clock was set back, would stamp a time
    /// that a heartbeat still on its way satisfies, and that heartbeat would bring back a
    /// record without the stopped mark: peers would count a relay that is gone.
    /// </summary>
    private long StoppedTimestamp(long now) => Math.Max(now, Volatile.Read(ref _lastHeartbeatSent) + 1);

    /// <summary>
    /// Writes this relay's coordination record: its heartbeat, or the stopped record a
    /// release leaves behind. One writer, so both carry the timestamp peers read.
    /// </summary>
    /// <remarks>
    /// Only this relay writes its own record, so the one condition a heartbeat needs is its
    /// own clock: the timestamp only ever moves forward. A request that this relay gave up on
    /// can still reach DynamoDB later, and without the condition it would revive a heartbeat
    /// that a later round, or the release of a stopping relay, had already replaced. The
    /// stopped record is written unconditionally: nothing it could lose to is newer.
    /// </remarks>
    /// <returns>False when a heartbeat was refused.</returns>
    private async Task<bool> WriteRelayRecordAsync(
        string relayId, long now, bool stopped, CancellationToken cancellationToken)
    {
        if (!stopped)
            RaiseLastHeartbeatSent(now);

        var record = _schema.RelayKey(relayId);
        record[DynamoDbOutboxSchema.LastSeenUtc] = DynamoDbOutboxSchema.Number(now);
        if (stopped)
            record[DynamoDbOutboxSchema.StoppedAtUtc] = DynamoDbOutboxSchema.Number(now);

        var write = new PutItemRequest { TableName = _schema.TableName, Item = record };
        if (!stopped)
        {
            write.ConditionExpression = "attribute_not_exists(#lastSeen) OR #lastSeen <= :now";
            write.ExpressionAttributeNames = new Dictionary<string, string>(1)
            {
                ["#lastSeen"] = DynamoDbOutboxSchema.LastSeenUtc
            };
            write.ExpressionAttributeValues = new Dictionary<string, AttributeValue>(1)
            {
                [":now"] = DynamoDbOutboxSchema.Number(now)
            };
        }

        try
        {
            await _client.PutItemAsync(write, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            // The record already carries a later timestamp. Either this request is the
            // straggler the condition exists for, and nothing is waiting for it, or two
            // hosts share one relay id, or this host's clock went backwards: peers then keep
            // the later timestamp until this relay's clock passes it.
            LogHeartbeatNotRecorded(relayId);
            return false;
        }
    }

    // Acquisition and renewal can run on different threads.
    private void RaiseLastHeartbeatSent(long now)
    {
        var seen = Volatile.Read(ref _lastHeartbeatSent);
        while (now > seen)
        {
            var current = Interlocked.CompareExchange(ref _lastHeartbeatSent, now, seen);
            if (current == seen)
                return;
            seen = current;
        }
    }

    private async Task PruneHeartbeatsAsync(
        List<string> staleRelayIds, long now, OutboxLeaseRequest request, CancellationToken cancellationToken)
    {
        var dead = DynamoDbOutboxSchema.Number(now - (request.LeaseDuration.Ticks * HeartbeatPruneFactor));
        // A stopped record is only there to refuse the requests of the round its stop
        // cancelled, which are long gone by the time it could no longer count as active.
        var stopped = DynamoDbOutboxSchema.Number(now - request.LeaseDuration.Ticks);
        await BoundedConcurrency.ForAsync(staleRelayIds.Count, _options.MaxConcurrency, async (index, token) =>
        {
            try
            {
                await _client.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = _schema.TableName,
                    Key = _schema.RelayKey(staleRelayIds[index]),
                    // Every relay prunes the same records. The missing-item branch lets the
                    // second delete of a record succeed instead of being refused.
                    ConditionExpression = "attribute_not_exists(#pk) OR #lastSeen < :dead"
                        + " OR (attribute_exists(#stopped) AND #lastSeen < :stopped)",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#pk"] = _schema.PartitionKeyName,
                        ["#lastSeen"] = DynamoDbOutboxSchema.LastSeenUtc,
                        ["#stopped"] = DynamoDbOutboxSchema.StoppedAtUtc
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>(2)
                    {
                        [":dead"] = dead,
                        [":stopped"] = stopped
                    }
                }, token).ConfigureAwait(false);
            }
            catch (ConditionalCheckFailedException)
            {
                // The relay came back after the read.
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} did not record its heartbeat: the coordination record already carries a later timestamp. Expected from a request abandoned by a stopping host; otherwise two hosts share this relay id, or this host's clock went backwards")]
    private partial void LogHeartbeatNotRecorded(string relayId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} did not claim {Buckets} free bucket(s): its heartbeat was refused, so this host's clock is behind a timestamp already written for this relay id, and a lease claimed now would expire early on its peers' clocks. It claims again once its clock passes that timestamp")]
    private partial void LogClaimsSkipped(string relayId, int buckets);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} did not renew its lease on bucket {Bucket}: this host's clock was set back, so the renewal would have moved the lease's expiry backwards. The bucket is not published by this relay until its clock passes the stored expiry")]
    private partial void LogLeaseNotMovedBackwards(string relayId, int bucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay {RelayId} still held bucket leases after releasing them repeatedly; a request abandoned by this host keeps taking them back. Peers take over after LeaseDuration ({LeaseDuration})")]
    private partial void LogReleaseUnfinished(string relayId, TimeSpan leaseDuration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Outbox relay {RelayId} did not get bucket {Bucket}: a peer claimed it first. Expected while relay membership is changing")]
    private partial void LogClaimRefused(string relayId, int bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay {RelayId} no longer owns bucket {Bucket}: the lease changed after this round read it, normally because a peer took it over after it expired")]
    private partial void LogLeaseTakenOver(string relayId, int bucket);
}
