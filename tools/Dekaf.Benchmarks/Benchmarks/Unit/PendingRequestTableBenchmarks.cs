using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Isolates pending-request tracking used by KafkaConnection for one request/response round trip.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 5, iterationCount: 15)]
public class PendingRequestTableBenchmarks
{
    private const int Operations = 1_000;
    private const int ShardCount = 16;

    private ConcurrentDictionary<int, PendingEntry> _concurrentDictionary = null!;
    private PendingShard[] _stripedDictionary = null!;
    private PendingEntry[] _entries = null!;

    [Params(5, 64)]
    public int InFlight { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _concurrentDictionary = new ConcurrentDictionary<int, PendingEntry>(concurrencyLevel: 1, capacity: InFlight * 4);
        _stripedDictionary = CreateShards(InFlight * 4);
        _entries = new PendingEntry[Operations];

        for (var i = 0; i < _entries.Length; i++)
            _entries[i] = new PendingEntry(new object(), (short)i);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations)]
    public void ConcurrentDictionary_AddGetRemove()
    {
        for (var i = 0; i < Operations; i++)
        {
            var correlationId = i + 1;
            _concurrentDictionary[correlationId] = _entries[i];
            _concurrentDictionary.TryGetValue(correlationId, out _);
            _concurrentDictionary.TryRemove(correlationId, out _);
        }
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public void StripedDictionary_AddGetRemove()
    {
        for (var i = 0; i < Operations; i++)
        {
            var correlationId = i + 1;
            var shard = _stripedDictionary[(int)((uint)correlationId % (uint)_stripedDictionary.Length)];

            lock (shard.Gate)
            {
                shard.Requests.Add(correlationId, _entries[i]);
            }

            lock (shard.Gate)
            {
                shard.Requests.TryGetValue(correlationId, out _);
            }

            lock (shard.Gate)
            {
                shard.Requests.Remove(correlationId);
            }
        }
    }

    private static PendingShard[] CreateShards(int capacity)
    {
        var shardCount = Math.Min(ShardCount, capacity);
        var capacityPerShard = Math.Max(1, (capacity + shardCount - 1) / shardCount);
        var shards = new PendingShard[shardCount];

        for (var i = 0; i < shards.Length; i++)
            shards[i] = new PendingShard(capacityPerShard);

        return shards;
    }

    private sealed class PendingShard
    {
        public PendingShard(int capacity)
        {
            Requests = new Dictionary<int, PendingEntry>(capacity);
        }

        public object Gate { get; } = new();
        public Dictionary<int, PendingEntry> Requests { get; }
    }

    private readonly record struct PendingEntry(object Request, short Version);
}
