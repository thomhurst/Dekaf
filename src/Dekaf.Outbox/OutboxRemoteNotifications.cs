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
        else if (buckets is SortedSet<int> sorted)
        {
            // Capture the endpoints directly so sparse commits do not scan the gap
            // before the last bucket. SortedSet's enumerator allocates a traversal stack.
            var first = sorted.Min;
            var last = sorted.Max;
            _hints.Add(first);
            if (sorted.Count > 1)
                _hints.Add(last);
            if (sorted.Count > 2)
                AddRemaining(sorted, sorted.Count - 2, first, last);
        }
        else
        {
            // Probe the configured bucket domain, as local ownership filtering does.
            // Arbitrary set enumerators can allocate even when their concrete type is
            // known. Contains preserves precise hints without enumerating the set.
            AddRemaining(buckets, buckets.Count);
        }
        Signal();
    }

    private void AddRemaining(IReadOnlySet<int> buckets, int remaining, int first = -1, int last = -1)
    {
        for (var bucket = 0; bucket < bucketCount && remaining > 0; bucket++)
        {
            if (bucket != first && bucket != last && buckets.Contains(bucket))
            {
                _hints.Add(bucket);
                remaining--;
            }
        }
        if (remaining > 0)
            _hints.AddUnknown();
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
