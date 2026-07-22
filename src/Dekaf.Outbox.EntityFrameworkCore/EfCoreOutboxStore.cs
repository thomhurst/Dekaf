using Microsoft.EntityFrameworkCore;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>Requires the context model to include the outbox entities via
/// <see cref="OutboxModelBuilderExtensions.UseDekafOutbox"/> and an
/// <see cref="IDbContextFactory{TContext}"/> registration (the relay runs outside any
/// request scope).</para>
/// <para>Lease mutations use guarded set-based <c>ExecuteUpdate</c> statements (the
/// ownership predicate lives in the WHERE clause), so no row locks or concurrency tokens
/// are needed, any relational provider works, and the statement count per renewal stays
/// flat regardless of bucket count. This store runs on the relay's polling cadence, not on
/// a Kafka hot path, so EF/LINQ usage here is intentional and fine.</para>
/// </remarks>
/// <typeparam name="TContext">The application's context type containing the outbox model.</typeparam>
public sealed class EfCoreOutboxStore<TContext> : IOutboxStore
    where TContext : DbContext
{
    /// <summary>
    /// Dead heartbeats are pruned once per this many renewal rounds; rows older than this
    /// many lease durations are removed.
    /// </summary>
    private const int HeartbeatPruneFactor = 10;

    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private volatile bool _leasesSeeded;
    private int _renewalRound;

    public EfCoreOutboxStore(IDbContextFactory<TContext> contextFactory, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var expiry = now + request.LeaseDuration;
        var leases = context.Set<OutboxLease>();

        await RecordHeartbeatAsync(context, request, now, cancellationToken).ConfigureAwait(false);
        await EnsureLeasesSeededAsync(context, request.BucketCount, cancellationToken).ConfigureAwait(false);
        await ThrowIfRowsOutsideBucketRangeAsync(context, request.BucketCount, cancellationToken)
            .ConfigureAwait(false);

        // Fair share: total buckets split across relays that heartbeated within one lease
        // duration. The algorithm lives in core (OutboxFairShare) so every store computes
        // shares identically - the anti-starvation guarantee depends on that.
        var activeCutoff = now - request.LeaseDuration;
        var activeRelayIds = await context.Set<OutboxRelayInstance>()
            .Where(r => r.LastSeenUtc >= activeCutoff)
            .Select(r => r.RelayId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var fairShare = OutboxFairShare.Compute(request.BucketCount, activeRelayIds, request.RelayId);

        // Bounded to the active range: stale lease rows left behind by a larger previous
        // BucketCount must not participate in fair share or be renewed as owned buckets,
        // or a relay could satisfy its whole share with buckets that no longer exist.
        var snapshot = await leases.AsNoTracking()
            .Where(l => l.Bucket >= 0 && l.Bucket < request.BucketCount)
            .OrderBy(l => l.Bucket)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var mine = new List<int>();
        var free = new List<int>();
        foreach (var lease in snapshot)
        {
            if (lease.Owner == request.RelayId)
                mine.Add(lease.Bucket);
            else if (lease.Owner is null || lease.ExpiresAtUtc <= now)
                free.Add(lease.Bucket);
        }

        // Keep (renew) currently-owned buckets first so rebalancing never churns buckets
        // this relay is already publishing; release the rest down to the fair share.
        var keepCount = Math.Min(mine.Count, fairShare);
        if (keepCount > 0)
        {
            var kept = mine.GetRange(0, keepCount).ToArray();
            await leases
                .Where(l => l.Owner == request.RelayId && kept.Contains(l.Bucket))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.ExpiresAtUtc, expiry), cancellationToken).ConfigureAwait(false);
        }

        if (mine.Count > fairShare)
        {
            var excess = mine.GetRange(fairShare, mine.Count - fairShare).ToArray();
            await leases
                .Where(l => l.Owner == request.RelayId && excess.Contains(l.Bucket))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Owner, (string?)null)
                    .SetProperty(l => l.ExpiresAtUtc, now), cancellationToken).ConfigureAwait(false);
        }

        // Claim unowned or expired buckets up to the fair share. The candidate list is
        // pre-limited to the deficit so over-claiming is impossible, and the guarded WHERE
        // re-evaluates per row so a concurrent claimer simply wins some of the candidates.
        var deficit = fairShare - keepCount;
        if (deficit > 0 && free.Count > 0)
        {
            var candidates = free.GetRange(0, Math.Min(deficit, free.Count)).ToArray();
            await leases
                .Where(l => candidates.Contains(l.Bucket) && (l.Owner == null || l.ExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Owner, request.RelayId)
                    .SetProperty(l => l.ExpiresAtUtc, expiry), cancellationToken).ConfigureAwait(false);
        }

        // Read back the true owned set: it reflects lost claim races and stolen leases.
        // Same range bound as the snapshot so stale out-of-range leases never reach the relay.
        return await leases
            .Where(l => l.Owner == request.RelayId && l.Bucket >= 0 && l.Bucket < request.BucketCount)
            .Select(l => l.Bucket)
            .OrderBy(b => b)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        if (buckets.Count == 0)
            return [];

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var bucketArray = buckets as int[] ?? [.. buckets];
        return await context.Set<OutboxMessage>().AsNoTracking()
            .Where(m => bucketArray.Contains(m.Bucket))
            .Select(m => m.Bucket)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
        int bucket,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        return await context.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.Bucket == bucket)
            .OrderBy(m => m.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> publishedMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publishedMessages);
        if (publishedMessages.Count == 0)
            return;

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var ids = new long[publishedMessages.Count];
        for (var i = 0; i < publishedMessages.Count; i++)
            ids[i] = publishedMessages[i].Id;
        await context.Set<OutboxMessage>()
            .Where(m => m.Bucket == bucket && ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordHeartbeatAsync(
        TContext context, OutboxLeaseRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var updated = await context.Set<OutboxRelayInstance>()
            .Where(r => r.RelayId == request.RelayId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LastSeenUtc, now), cancellationToken).ConfigureAwait(false);

        if (updated == 0)
        {
            context.Set<OutboxRelayInstance>().Add(new OutboxRelayInstance
            {
                RelayId = request.RelayId,
                LastSeenUtc = now
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
                // DbUpdateException is usually the benign concurrent-insert race, but it also
                // wraps genuine failures (dropped connection, timeout). Verify instead of
                // assuming: if the row is really missing, rethrow so the relay's backoff
                // retries rather than silently proceeding without a heartbeat.
                var exists = await context.Set<OutboxRelayInstance>()
                    .AnyAsync(r => r.RelayId == request.RelayId, cancellationToken).ConfigureAwait(false);
                if (!exists)
                    throw;
            }
        }

        // Pruning is housekeeping, not correctness; run it occasionally instead of per round.
        if (++_renewalRound % HeartbeatPruneFactor == 0)
        {
            var pruneCutoff = now - (request.LeaseDuration * HeartbeatPruneFactor);
            await context.Set<OutboxRelayInstance>()
                .Where(r => r.LastSeenUtc < pruneCutoff)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureLeasesSeededAsync(
        TContext context, int bucketCount, CancellationToken cancellationToken)
    {
        // Seeding is a once-per-table event and lease rows are never deleted, so the
        // confirmed state is cached and this becomes a no-op after the first renewal.
        if (_leasesSeeded)
            return;

        // Verify bucket identity, never row counts: a table can hold rows outside
        // [0, bucketCount) (e.g. seeded by a larger-count relay), and Bucket is the
        // primary key, so an in-range count of bucketCount proves every bucket exists.
        var existing = await context.Set<OutboxLease>().AsNoTracking()
            .Where(l => l.Bucket >= 0 && l.Bucket < bucketCount)
            .Select(l => l.Bucket)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (existing.Count < bucketCount)
        {
            var have = new HashSet<int>(existing);
            for (var bucket = 0; bucket < bucketCount; bucket++)
            {
                if (!have.Contains(bucket))
                {
                    context.Set<OutboxLease>().Add(new OutboxLease
                    {
                        Bucket = bucket,
                        Owner = null,
                        ExpiresAtUtc = DateTimeOffset.MinValue
                    });
                }
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
                // Same verify-don't-assume rule as the heartbeat insert: latching the
                // seeded flag on a transient failure would leave unclaimable buckets.
                var seededCount = await context.Set<OutboxLease>()
                    .Where(l => l.Bucket >= 0 && l.Bucket < bucketCount)
                    .CountAsync(cancellationToken).ConfigureAwait(false);
                if (seededCount < bucketCount)
                    throw;
            }
        }

        _leasesSeeded = true;
    }

    /// <summary>
    /// Fails fast when the table contains rows in buckets this relay can never claim -
    /// the silent-message-loss failure mode of a writer configured with a larger bucket
    /// count than the relay. An index seek on (Bucket, Id), so cheap at renewal cadence.
    /// </summary>
    private static async Task ThrowIfRowsOutsideBucketRangeAsync(
        TContext context, int bucketCount, CancellationToken cancellationToken)
    {
        var orphaned = await context.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.Bucket >= bucketCount || m.Bucket < 0)
            .AnyAsync(cancellationToken).ConfigureAwait(false);
        if (orphaned)
        {
            throw new OutboxMisconfigurationException(
                $"The outbox table contains rows in buckets outside [0, {bucketCount}). " +
                "A writer is enqueuing with a larger bucket count than this relay's " +
                "OutboxRelayOptions.BucketCount; those rows would never be published. " +
                "Align the bucket count across all writers and relays.");
        }
    }
}
