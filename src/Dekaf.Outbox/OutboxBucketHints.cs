using System.Numerics;

namespace Dekaf.Outbox;

// Bounded, coalescing hints. Concurrent committers set bits; one consumer drains them.
// Storage and drain work depend on bucket count, never on the number of commits.
internal sealed class OutboxBucketHints(int bucketCount)
{
    private readonly long[] _words = new long[(int)(((long)bucketCount + 63) / 64)];
    private int _unknown;

    public void AddUnknown() => Volatile.Write(ref _unknown, 1);

    public void Add(int bucket)
    {
        if ((uint)bucket >= (uint)bucketCount)
        {
            AddUnknown();
            return;
        }
        ref var word = ref _words[bucket / 64];
        var bit = 1L << (bucket % 64);
        // A set bit already covers this committed write. If a drain races after this
        // read, its subsequent fetch covers the write; if it races before, set again.
        if ((Volatile.Read(ref word) & bit) == 0)
            Interlocked.Or(ref word, bit);
    }

    public int Drain(Span<int> destination, out bool unknown)
    {
        unknown = Volatile.Read(ref _unknown) != 0 && Interlocked.Exchange(ref _unknown, 0) != 0;
        var count = 0;
        for (var index = 0; index < _words.Length; index++)
        {
            // Leave a concurrently arriving hint for the next signalled cycle. Empty
            // sweeps need no write barrier or cache-line invalidation.
            if (Volatile.Read(ref _words[index]) == 0)
                continue;
            var bits = (ulong)Interlocked.Exchange(ref _words[index], 0);
            while (bits != 0)
            {
                var bit = BitOperations.TrailingZeroCount(bits);
                if (count == destination.Length)
                {
                    // A manually constructed notifier may use a different bucket count.
                    // Fall back to discovery rather than dropping a hint or overrunning.
                    unknown = true;
                    break;
                }
                destination[count++] = index * 64 + bit;
                bits &= bits - 1;
            }
        }
        return count;
    }
}
