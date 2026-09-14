using System.Collections.Immutable;
using System.Threading.Channels;

namespace Dekaf.Outbox;

internal sealed class OutboxRemoteNotifications(int bucketCount)
{
    private readonly OutboxBucketHints _hints = new(bucketCount);
    private readonly Channel<byte> _ready = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Notify(int bucket)
    {
        _hints.Add(bucket);
        Signal();
    }

    public void Notify(IReadOnlySet<int> buckets)
    {
        if (buckets.Count == 0)
            return;
        // EF supplies a HashSet; retain its struct enumerator on the commit path.
        if (buckets is HashSet<int> set)
        {
            foreach (var bucket in set)
                _hints.Add(bucket);
        }
        else if (buckets is ImmutableHashSet<int> immutable)
        {
            foreach (var bucket in immutable)
                _hints.Add(bucket);
        }
        else if (buckets is SortedSet<int> sorted && sorted.Count <= 2)
        {
            // These sets need only their endpoints, without a traversal stack.
            var first = sorted.Min;
            var last = sorted.Max;
            _hints.Add(first);
            if (sorted.Count > 1)
                _hints.Add(last);
        }
        else
        {
            // IReadOnlySet has no allocation-free enumeration contract. Keep commit
            // work constant for unsupported sets; the remote relay discovers its
            // owned buckets from this coalesced advisory notification.
            _hints.AddUnknown();
        }
        Signal();
    }

    private void Signal() => _ready.Writer.TryWrite(0);

    public async ValueTask<int> ReadAsync(Memory<int> destination, CancellationToken cancellationToken)
    {
        await _ready.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var count = _hints.Drain(destination.Span, out var unknown);
        if (!unknown)
            return count;
        destination.Span[0] = -1;
        return 1;
    }
}
