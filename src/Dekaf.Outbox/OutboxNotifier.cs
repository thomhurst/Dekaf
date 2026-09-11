using System.Threading.Channels;

namespace Dekaf.Outbox;

internal sealed class OutboxNotifier : IOutboxBucketNotifier, IDisposable
{
    private readonly Channel<byte> _notifications = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });
    private readonly ITimer _timer;
    private int[]? _ownedBuckets;

    public OutboxNotifier(TimeProvider timeProvider)
    {
        _timer = timeProvider.CreateTimer(static state => ((OutboxNotifier)state!).NotifyCommitted(),
            this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void NotifyCommitted() => _notifications.Writer.TryWrite(0);

    public void NotifyCommitted(IReadOnlySet<int> buckets)
    {
        var owned = Volatile.Read(ref _ownedBuckets);
        if (owned is null)
        {
            if (buckets.Count > 0)
                NotifyCommitted();
            return;
        }

        // Iterate the small immutable owned array, avoiding interface-enumerator
        // allocations and message-count work on each commit.
        for (var index = 0; index < owned.Length; index++)
        {
            if (buckets.Contains(owned[index]))
            {
                NotifyCommitted();
                return;
            }
        }
    }

    public void NotifyCommitted(int bucket)
    {
        var owned = Volatile.Read(ref _ownedBuckets);
        if (owned is null || Array.IndexOf(owned, bucket) >= 0)
            NotifyCommitted();
    }

    public void SetOwnedBuckets(IReadOnlyList<int> buckets)
    {
        var snapshot = buckets.Count == 0 ? [] : new int[buckets.Count];
        for (var index = 0; index < buckets.Count; index++)
            snapshot[index] = buckets[index];
        Volatile.Write(ref _ownedBuckets, snapshot);
    }

    public ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_notifications.Reader.TryRead(out _))
            return ValueTask.CompletedTask;
        return WaitForNotificationAsync(timeout, cancellationToken);
    }

    private async ValueTask WaitForNotificationAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        _timer.Change(timeout, Timeout.InfiniteTimeSpan);
        try
        {
            await _notifications.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose() => _timer.Dispose();
}
