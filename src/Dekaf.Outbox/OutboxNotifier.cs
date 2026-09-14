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
    private readonly OutboxBucketHints _hints;
    private int[]? _ownedBuckets;
    private OutboxRemoteNotifications? _remote;

    public OutboxNotifier(TimeProvider timeProvider, int bucketCount = OutboxRelayOptions.DefaultBucketCount)
    {
        _hints = new OutboxBucketHints(bucketCount);
        _timer = timeProvider.CreateTimer(static state => ((OutboxNotifier)state!).Signal(),
            this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void Signal() => _notifications.Writer.TryWrite(0);

    public void NotifyCommitted()
    {
        Volatile.Read(ref _remote)?.Notify(-1);
        NotifyUnknownReceived();
    }

    private void NotifyUnknownReceived()
    {
        _hints.AddUnknown();
        Signal();
    }

    internal void SetRemoteNotifications(OutboxRemoteNotifications remote) => Volatile.Write(ref _remote, remote);

    internal int DrainHints(Span<int> destination, out bool unknown) => _hints.Drain(destination, out unknown);

    public void NotifyCommitted(IReadOnlySet<int> buckets)
    {
        Volatile.Read(ref _remote)?.Notify(buckets);
        var owned = Volatile.Read(ref _ownedBuckets);
        if (owned is null)
        {
            if (buckets.Count > 0)
                NotifyUnknownReceived();
            return;
        }

        // Iterate the small immutable owned array, avoiding interface-enumerator
        // allocations and message-count work on each commit.
        var relevant = false;
        for (var index = 0; index < owned.Length; index++)
        {
            if (buckets.Contains(owned[index]))
            {
                _hints.Add(owned[index]);
                relevant = true;
            }
        }
        if (relevant)
            Signal();
    }

    public void NotifyCommitted(int bucket)
    {
        Volatile.Read(ref _remote)?.Notify(bucket);
        NotifyReceived(bucket);
    }

    // Incoming hints never re-enter the sender, including transports that echo broadcasts.
    internal void NotifyReceived(int bucket)
    {
        if (bucket < 0)
        {
            NotifyUnknownReceived();
            return;
        }
        var owned = Volatile.Read(ref _ownedBuckets);
        if (owned is null || Array.IndexOf(owned, bucket) >= 0)
        {
            _hints.Add(bucket);
            Signal();
        }
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
