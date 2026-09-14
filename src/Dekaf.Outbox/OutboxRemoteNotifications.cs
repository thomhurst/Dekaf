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
        // EF supplies a HashSet; retain its struct enumerator on the commit path.
        if (buckets is HashSet<int> set)
        {
            foreach (var bucket in set)
                _hints.Add(bucket);
        }
        else
        {
            foreach (var bucket in buckets)
                _hints.Add(bucket);
        }
        if (buckets.Count > 0)
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
