using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dekaf.StressTests.Scenarios;

/// <summary>Counts store operations without replacing database work or optional store capabilities.</summary>
internal sealed class ObservedOutboxStore<TContext>(EfCoreOutboxStore<TContext> inner, OutboxWorkloadState state)
    : IOutboxStore, IOutboxLeaseRenewalStore, IOutboxMetricsStore where TContext : DbContext
{
    private long _acquisitions;
    private long _renewals;
    private long _probes;
    private long _reads;
    private long _marks;
    private long _metricQueries;
    private long _published;
    private long _errors;

    public long Published => Interlocked.Read(ref _published);

    public OutboxOperationCounts Snapshot() => new(
        Interlocked.Read(ref _acquisitions), Interlocked.Read(ref _renewals), Interlocked.Read(ref _probes),
        Interlocked.Read(ref _reads), Interlocked.Read(ref _marks), Interlocked.Read(ref _metricQueries),
        Interlocked.Read(ref _published), Interlocked.Read(ref _errors));

    public async ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _acquisitions);
        try { return await inner.AcquireBucketLeasesAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    public async ValueTask<bool> RenewBucketLeasesAsync(OutboxLeaseRequest request, IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _renewals);
        try { return await inner.RenewBucketLeasesAsync(request, buckets, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    public async ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _probes);
        try { return await inner.GetBucketsWithPendingAsync(buckets, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    public async ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reads);
        try { return await inner.GetNextBatchAsync(bucket, maxCount, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    public async ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> publishedMessages, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _marks);
        try
        {
            await inner.MarkPublishedAsync(bucket, publishedMessages, cancellationToken).ConfigureAwait(false);
            Interlocked.Add(ref _published, publishedMessages.Count);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    public async ValueTask<OutboxPendingMetrics?> GetPendingMetricsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _metricQueries);
        try { return await inner.GetPendingMetricsAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { RecordFailure(error); throw; }
    }

    private void RecordFailure(Exception error)
    {
        Interlocked.Increment(ref _errors);
        state.RecordFailure(error);
    }
}

internal sealed record OutboxOperationCounts(long Acquisitions, long Renewals, long Probes,
    long Reads, long Marks, long MetricQueries, long Published, long Errors)
{
    public OutboxOperationCounts Since(OutboxOperationCounts start) => new(
        Acquisitions - start.Acquisitions, Renewals - start.Renewals, Probes - start.Probes,
        Reads - start.Reads, Marks - start.Marks, MetricQueries - start.MetricQueries,
        Published - start.Published, Errors - start.Errors);
}

internal sealed class ObservedOutboxPublisher(IOutboxPublisher inner, OutboxWorkloadState state) : IOutboxPublisher
{
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        try { await inner.InitializeAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { state.RecordFailure(error); throw; }
    }

    public async ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages, string messageIdHeaderName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await inner.PublishAsync(messages, messageIdHeaderName, cancellationToken).ConfigureAwait(false);
            if (result.FirstError is { } error)
                state.RecordFailure(error);
            else if (result.AckedCount != messages.Count)
                state.RecordFailure(new InvalidOperationException("The outbox publisher returned an incomplete prefix without an error."));
            return result;
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested) { state.RecordFailure(error); throw; }
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
