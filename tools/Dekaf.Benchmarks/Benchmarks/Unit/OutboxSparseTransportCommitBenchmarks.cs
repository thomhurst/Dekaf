using System.Collections;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Sparse transport notifications while a sender is busy. Isolates synchronous commit
/// work from the amortized background drain, comparing small and large bucket domains.
/// Buffer storage stays bounded and notifications coalesce throughout measurement.
/// </summary>
[MemoryDiagnoser]
public class OutboxSparseTransportCommitBenchmarks
{
    [Params(8, 1000001)]
    public int BucketCount { get; set; }

    [Params(false, true)]
    public bool CustomSet { get; set; }

    private IReadOnlySet<int> _committed = null!;
    private Action<IReadOnlySet<int>> _notify = null!;

    [GlobalSetup]
    public void Setup()
    {
        var buckets = new HashSet<int> { 0, BucketCount - 2, BucketCount - 1 };
        _committed = CustomSet ? new SetAdapter(buckets) : new SortedSet<int>(buckets);
        var type = typeof(IOutboxNotificationTransport).Assembly.GetType("Dekaf.Outbox.OutboxRemoteNotifications")!;
        var buffer = Activator.CreateInstance(type, BucketCount)!;
        _notify = type.GetMethod("Notify", [typeof(IReadOnlySet<int>)])!
            .CreateDelegate<Action<IReadOnlySet<int>>>(buffer);
        _notify(_committed);
    }

    [Benchmark]
    public void NotifyWhileTransportBusy() => _notify(_committed);

    private sealed class SetAdapter(HashSet<int> buckets) : IReadOnlySet<int>
    {
        public int Count => buckets.Count;
        public bool Contains(int item) => buckets.Contains(item);
        public IEnumerator<int> GetEnumerator() => buckets.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<int> other) => buckets.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<int> other) => buckets.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<int> other) => buckets.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<int> other) => buckets.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<int> other) => buckets.Overlaps(other);
        public bool SetEquals(IEnumerable<int> other) => buckets.SetEquals(other);
    }
}
