using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>Requires the context model to include the outbox entities via
/// <see cref="OutboxModelBuilderExtensions.UseDekafOutbox(ModelBuilder, string)"/> and an
/// <see cref="IDbContextFactory{TContext}"/> registration (the relay runs outside any
/// request scope).</para>
/// <para>Lease mutations use guarded set-based <c>ExecuteUpdate</c> statements (the
/// ownership predicate lives in the WHERE clause), so no row locks or concurrency tokens
/// are needed, any relational provider works, and the statement count per renewal stays
/// flat regardless of bucket count. This store runs on the relay's polling cadence, not on
/// a Kafka hot path, so EF/LINQ usage here is intentional and fine.</para>
/// </remarks>
/// <typeparam name="TContext">The application's context type containing the outbox model.</typeparam>
public sealed class EfCoreOutboxStore<TContext> : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxLeaseOwnershipStore,
    IOutboxMetricsStore
    where TContext : DbContext
{
    /// <summary>
    /// Dead heartbeats are pruned once per this many renewal rounds; rows older than this
    /// many lease durations are removed.
    /// </summary>
    private const int HeartbeatPruneFactor = 10;

    /// <summary>
    /// How many times a release frees the relay's leases before it gives up on undoing a
    /// claim that a cancelled acquisition round landed after it. The first pass hands the
    /// leases back, the rest confirm; the bound keeps a stopping host from looping forever.
    /// </summary>
    private const int MaxReleaseAttempts = 3;

    // EF compiled queries are model-specific. Weak model keys support custom mappings
    // without retaining application models or sharing a delegate across different models.
    private static readonly ConditionalWeakTable<IModel, Func<TContext, int[], IAsyncEnumerable<int>>> PendingBucketQueries = new();

    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private volatile bool _leasesSeeded;
    private int _renewalRound;
    // Latest timestamp (UTC ticks) any heartbeat of this store was sent with; see StoppedTimestamp.
    private long _lastHeartbeatSent;
    // The round that found the relay a distant standby; null otherwise. Only the relay's
    // acquisitions touch it, and the relay serializes those.
    private DistantStandby? _distantStandby;

    /// <param name="relayId">Named, so a store that serves a second relay id does not answer
    /// it from the first one's place in the queue.</param>
    /// <param name="since">When the round ran.</param>
    private sealed class DistantStandby(string relayId, DateTimeOffset since)
    {
        public string RelayId { get; } = relayId;

        public DateTimeOffset Since { get; } = since;

        /// <summary>The latest acquisition, answered or not.</summary>
        public DateTimeOffset LastCall { get; set; } = since;
    }

    public EfCoreOutboxStore(IDbContextFactory<TContext> contextFactory, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Samples the whole table with server-side aggregates on a separate context.
    /// SQLite's native DateTimeOffset mapping cannot order/aggregate timestamps, so
    /// nonempty SQLite backlogs report count with an unavailable oldest timestamp.
    /// </summary>
    public async ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var messages = context.Set<OutboxMessage>().AsNoTracking();
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            return new OutboxPendingMetrics(await messages.LongCountAsync(cancellationToken).ConfigureAwait(false), null);

        // Compute both aggregates in one command/pass without adding an index to every
        // enqueue/delete. A constant group produces no row for an empty table.
        var sample = await messages.GroupBy(static _ => 1)
            .Select(group => new
            {
                Count = group.LongCount(),
                Oldest = group.Min(message => (DateTimeOffset?)message.CreatedAtUtc)
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return sample is null ? new OutboxPendingMetrics(0, null) : new OutboxPendingMetrics(sample.Count, sample.Oldest);
    }

    public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _timeProvider.GetUtcNow();
        if (IsRestingStandby(request, now))
            return [];

        // A round that fails proves nothing about the queue, so the next one runs in full.
        _distantStandby = null;

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var expiry = now + request.LeaseDuration;
        var leases = context.Set<OutboxLease>();

        var heartbeatRecorded = await RecordHeartbeatAsync(context, request, now, cancellationToken)
            .ConfigureAwait(false);
        await EnsureLeasesSeededAsync(context, request.BucketCount, cancellationToken).ConfigureAwait(false);
        await ThrowIfRowsOutsideBucketRangeAsync(context, request.BucketCount, cancellationToken)
            .ConfigureAwait(false);

        // A relay that released its buckets is gone from that moment, whatever its timestamp
        // says: its row only stays behind to refuse the statements of the round its stop
        // cancelled.
        var activeCutoff = now - request.LeaseDuration;
        var activeRelayIds = await context.Set<OutboxRelayInstance>()
            .Where(r => r.LastSeenUtc >= activeCutoff && r.StoppedAtUtc == null)
            .Select(r => r.RelayId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Bounded to the active range: stale lease rows left behind by a larger previous
        // BucketCount must not participate in fair share or be renewed as owned buckets,
        // or a relay could satisfy its whole share with buckets that no longer exist.
        var snapshot = await leases.AsNoTracking()
            .Where(l => l.Bucket >= 0 && l.Bucket < request.BucketCount)
            .OrderBy(l => l.Bucket)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var mine = new List<int>();
        var free = new List<int>();
        var held = new Dictionary<string, int>(activeRelayIds.Count, StringComparer.Ordinal);
        foreach (var lease in snapshot)
        {
            if (lease.Owner == request.RelayId)
                mine.Add(lease.Bucket);
            else if (lease.Owner is null || lease.ExpiresAtUtc <= now)
                free.Add(lease.Bucket);
            else
                held[lease.Owner] = held.GetValueOrDefault(lease.Owner) + 1;
        }

        held[request.RelayId] = mine.Count;

        // Fair share: total buckets split across relays that heartbeated within one lease
        // duration, leaving what the incumbents hold where it is. The algorithm lives in core
        // (OutboxFairShare) so every store computes shares identically - the anti-starvation
        // guarantee depends on that.
        var fairShare = OutboxFairShare.Compute(request.BucketCount, activeRelayIds, request.RelayId, held);

        // A standby has nothing to renew, hand back or claim, so nothing to read back either.
        if (fairShare == 0 && mine.Count == 0)
        {
            // Not after a refused heartbeat: peers may not count this relay at all, so what
            // it read about its place in the queue is not what they plan with.
            if (heartbeatRecorded
                && OutboxFairShare.StandbyRank(request.BucketCount, activeRelayIds, request.RelayId, held) >= request.BucketCount)
            {
                _distantStandby = new DistantStandby(request.RelayId, now);
            }

            return [];
        }

        // Keep (renew) currently-owned buckets first so rebalancing never churns buckets
        // this relay is already publishing; release the rest down to the fair share.
        var keepCount = Math.Min(mine.Count, fairShare);
        if (keepCount > 0)
        {
            var kept = mine.GetRange(0, keepCount).ToArray();
            // The expiry only moves forward. A statement this relay gave up on (a provider
            // that breaks the connection when cancellation times out) can still run later,
            // and a host whose clock was set back computes an earlier expiry than it stored;
            // either would shorten a lease that peers may already have planned around.
            await leases
                .Where(l => l.Owner == request.RelayId && kept.Contains(l.Bucket) && l.ExpiresAtUtc <= expiry)
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
        // Not after a refused heartbeat: this host's clock is then behind a timestamp already
        // written for its relay id, so the expiry of this round is too early. A kept lease is
        // guarded by the expiry it already stores; a claim has none to compare with, and a peer
        // could take the bucket the moment it lapses on the peer's clock, while this relay
        // still counts a whole lease duration.
        // Nor for a stopped relay: the owner guard cannot refuse a claim, because the lease it
        // takes has no owner, so a claim of the round a stop cancelled, run by the server after
        // the release, would hand a bucket to a relay that is gone. The tombstone refuses it.
        // Nor after a later heartbeat of the same id: a process restarted under it clears the
        // stopped mark, and a claim of the round the stop cancelled would then pass. Rounds of
        // one relay run one at a time, so a later heartbeat means this round was abandoned.
        var deficit = fairShare - keepCount;
        if (deficit > 0 && free.Count > 0 && heartbeatRecorded)
        {
            var candidates = free.GetRange(0, Math.Min(deficit, free.Count)).ToArray();
            await leases
                .Where(l => candidates.Contains(l.Bucket) && (l.Owner == null || l.ExpiresAtUtc <= now)
                    && context.Set<OutboxRelayInstance>().Any(r => r.RelayId == request.RelayId
                        && r.StoppedAtUtc == null && r.LastSeenUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Owner, request.RelayId)
                    .SetProperty(l => l.ExpiresAtUtc, expiry), cancellationToken).ConfigureAwait(false);
        }

        // Read back the true owned set: it reflects lost claim races and stolen leases.
        // Same range bound as the snapshot so stale out-of-range leases never reach the relay.
        // Only leases this round wrote: the relay trusts what it is told for a whole lease
        // duration from now, which a lease that kept a later expiry (see above) cannot
        // promise on a peer's clock. Such a lease still names this relay, so nobody else takes
        // it, and it is kept again once this host's clock has passed it.
        return await leases
            .Where(l => l.Owner == request.RelayId && l.ExpiresAtUtc == expiry
                && l.Bucket >= 0 && l.Bucket < request.BucketCount)
            .Select(l => l.Bucket)
            .OrderBy(b => b)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether this acquisition can be answered without a statement. With more relays than
    /// buckets most relays own nothing, and each of their rounds costs a heartbeat write and
    /// several reads that change nothing. The standbys next in line keep the relay's cadence,
    /// one per bucket, so that they take over what frees up as promptly as before, even if
    /// every owner stops at once. A relay behind them is only there to be counted: it
    /// refreshes its heartbeat often enough to stay active, and finds out that it has moved up
    /// when it does.
    /// </summary>
    private bool IsRestingStandby(OutboxLeaseRequest request, DateTimeOffset now)
    {
        if (_distantStandby is not { } standby || standby.RelayId != request.RelayId)
            return false;

        var rested = now - standby.Since;
        var sinceLastCall = now - standby.LastCall;
        standby.LastCall = now;

        // The relay calls at its own cadence, which the store is not told, so the time since
        // the last call stands in for the time until the next one. The heartbeat is refreshed
        // by the last call that comes within three quarters of the lease duration for which
        // peers count it: every other round with the default timings, and every round with a
        // renew interval too long to skip one. A clock that was set back says nothing about
        // the age of the heartbeat.
        return rested >= TimeSpan.Zero && sinceLastCall >= TimeSpan.Zero
            && rested + sinceLastCall < request.LeaseDuration * 0.75;
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
        // Acquired buckets always have a lease row. Probe each of those bounded rows
        // with EXISTS, allowing the (Bucket, Id) index to stop at the first message.
        // DISTINCT over messages instead visits the entire owned backlog on some providers.
        if (_leasesSeeded)
        {
            var query = PendingBucketQueries.GetValue(context.Model, static _ => EF.CompileAsyncQuery<TContext, int[], int>(
                (TContext db, int[] requestedBuckets) => db.Set<OutboxLease>().AsNoTracking()
                    .Where(lease => requestedBuckets.Contains(lease.Bucket)
                        && db.Set<OutboxMessage>().Any(message => message.Bucket == lease.Bucket))
                    .Select(lease => lease.Bucket)
                    .OrderBy(bucket => bucket)));
            var pending = new List<int>();
            await foreach (var bucket in query(context, bucketArray).WithCancellation(cancellationToken).ConfigureAwait(false))
                pending.Add(bucket);
            return pending;
        }

        // Preserve direct pre-acquisition probes for callers inspecting an unseeded store.
        // The relay always acquires first and never takes this compatibility path.
        return await context.Set<OutboxMessage>().AsNoTracking()
            .Where(m => bucketArray.Contains(m.Bucket))
            .Select(m => m.Bucket)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> RenewBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(buckets);
        if (buckets.Count == 0)
            return true;

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var expiry = now + request.LeaseDuration;
        var bucketArray = buckets as int[] ?? [.. buckets];
        // The expiry only moves forward, as it does in an acquisition: after this host's clock
        // was set back, a renewal would shorten the lease while the relay goes on counting a
        // whole lease duration. Refused, the relay drops the bucket and reacquires it.
        var renewed = await context.Set<OutboxLease>()
            .Where(lease => lease.Owner == request.RelayId
                && lease.ExpiresAtUtc > now && lease.ExpiresAtUtc <= expiry
                && lease.Bucket >= 0 && lease.Bucket < request.BucketCount && bucketArray.Contains(lease.Bucket))
            .ExecuteUpdateAsync(setters => setters.SetProperty(lease => lease.ExpiresAtUtc, expiry), cancellationToken)
            .ConfigureAwait(false);
        // Matching leases may already be extended after a partial mismatch. The relay
        // drops its local ownership and reacquires valid buckets on the next cycle,
        // rather than continuing publication on a partially renewed set.
        if (renewed != buckets.Count)
            return false;

        await RecordHeartbeatAsync(context, request, now, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Acquisition reads the whole lease table before it writes, so it already keeps this
    // relay's buckets first and claims only buckets it saw free. The hint adds nothing.
    ValueTask<IReadOnlyList<int>> IOutboxLeaseOwnershipStore.AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken) => AcquireBucketLeasesAsync(request, cancellationToken);

    public async ValueTask ReleaseBucketLeasesAsync(
        OutboxLeaseRequest request,
        IReadOnlyList<int> previousBuckets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var contextDisposal = context.ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();

        // A stop cancels the acquisition round it interrupts. Providers wait for the server
        // to confirm a cancelled statement, so normally nothing of that round is left to
        // run; one that breaks the connection instead can leave a statement that lands after
        // the release and names a relay that is gone: a heartbeat, or a claim, whose owner
        // guard cannot refuse it because a released lease has no owner.
        // Peers stop dividing the buckets by a relay count that still includes this one. The
        // row is stamped as stopped rather than deleted: a heartbeat only moves its timestamp
        // forward, and a claim is refused for a stopped relay, while a deleted row leaves
        // nothing for either to lose to. Written even when the relay has no row yet, so that a
        // first heartbeat insert still on its way is refused too.
        // Before the leases, not after: a release cut short by the shutdown deadline then
        // leaves leases that expire, which costs what no release costs. The other order leaves
        // freed buckets next to a live heartbeat, and peers keep the fair share of a relay that
        // is gone unclaimed until the heartbeat ages out.
        await RecordStoppedAsync(context, request.RelayId, StoppedTimestamp(now), cancellationToken)
            .ConfigureAwait(false);

        // A claim that passed its guard just before the stamp can still commit after the
        // first pass, so the release repeats until a pass finds nothing. A healthy release is
        // two passes.
        for (var attempt = 0; attempt < MaxReleaseAttempts; attempt++)
        {
            // Scoped to the owner, not to previousBuckets: one statement also frees leases that
            // an acquisition claimed before it failed, and the guard leaves a peer's takeover alone.
            var released = await context.Set<OutboxLease>()
                .Where(l => l.Owner == request.RelayId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Owner, (string?)null)
                    .SetProperty(l => l.ExpiresAtUtc, now), cancellationToken).ConfigureAwait(false);
            if (released == 0)
                return;
        }
    }

    /// <summary>
    /// The timestamp of the stopped row: later than every heartbeat this store sent, so none
    /// of them can replace it. The clock alone does not promise that. A stop in the tick of the
    /// round it cancels, or after the host's clock was set back, would stamp a time that a
    /// heartbeat still on its way satisfies, and that heartbeat would bring back a row without
    /// the stopped mark: peers would count a relay that is gone.
    /// </summary>
    private DateTimeOffset StoppedTimestamp(DateTimeOffset now) =>
        new(Math.Max(now.UtcTicks, Volatile.Read(ref _lastHeartbeatSent) + 1), TimeSpan.Zero);

    /// <summary>
    /// Stamps this relay's row as stopped, inserting it when there is none. Unconditional:
    /// nothing it could lose to is newer.
    /// </summary>
    private static async Task RecordStoppedAsync(
        TContext context, string relayId, DateTimeOffset stoppedAt, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var stamped = await context.Set<OutboxRelayInstance>()
                .Where(r => r.RelayId == relayId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.LastSeenUtc, stoppedAt)
                    .SetProperty(r => r.StoppedAtUtc, stoppedAt), cancellationToken).ConfigureAwait(false);
            if (stamped > 0)
                return;

            context.Set<OutboxRelayInstance>().Add(new OutboxRelayInstance
            {
                RelayId = relayId,
                LastSeenUtc = stoppedAt,
                StoppedAtUtc = stoppedAt
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Usually a first heartbeat insert that landed in between: stamp that row. A
                // genuine failure fails the stamp again and surfaces on the second attempt.
                context.ChangeTracker.Clear();
            }
        }
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

    /// <returns>False when the row already carries a later timestamp.</returns>
    private async Task<bool> RecordHeartbeatAsync(
        TContext context, OutboxLeaseRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RaiseLastHeartbeatSent(now.UtcTicks);

        // The timestamp only moves forward: a statement from an earlier round that runs late
        // must not make a live relay look older, or dead, to its peers, nor bring back a relay
        // whose release stamped a later time. A heartbeat that passes clears the stopped mark,
        // so a relay started again under the same id is counted again.
        var updated = await context.Set<OutboxRelayInstance>()
            .Where(r => r.RelayId == request.RelayId && r.LastSeenUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LastSeenUtc, now)
                .SetProperty(r => r.StoppedAtUtc, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);

        // No row, or a row that already carries a later timestamp, which stays as it is.
        var recorded = updated > 0;
        if (!recorded && !await context.Set<OutboxRelayInstance>()
                .AnyAsync(r => r.RelayId == request.RelayId, cancellationToken).ConfigureAwait(false))
        {
            context.Set<OutboxRelayInstance>().Add(new OutboxRelayInstance
            {
                RelayId = request.RelayId,
                LastSeenUtc = now
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                recorded = true;
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
        // A stopped row is only there to refuse the statements of the round its stop
        // cancelled, which are long gone once it could no longer count as active anyway.
        if (++_renewalRound % HeartbeatPruneFactor == 0)
        {
            var pruneCutoff = now - (request.LeaseDuration * HeartbeatPruneFactor);
            var stoppedCutoff = now - request.LeaseDuration;
            await context.Set<OutboxRelayInstance>()
                .Where(r => r.LastSeenUtc < pruneCutoff || (r.StoppedAtUtc != null && r.LastSeenUtc < stoppedCutoff))
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        return recorded;
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
