using System.Buffers;
using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks demonstrating the TopicPartition allocation optimization in RecordAccumulator.
/// Compares the old approach (allocating TopicPartition on every call) vs the new cached approach.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class TopicPartitionCacheBenchmarks
{
    private RecordAccumulator _cachedAccumulator = null!;
    private ValueTaskSourcePool<RecordMetadata> _pool = null!;
    private ProducerOptions _options = null!;
    private const string TestTopic = "benchmark-topic";
    private const int TestPartition = 0;
    private byte[] _keyTemplate = null!;
    private byte[] _valueTemplate = null!;
    private int _keyLength;
    private int _valueLength;

    [Params(1, 10, 100)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "benchmark",
            BatchSize = 16384,
            LingerMs = 5,
            BufferMemory = 32 * 1024 * 1024
        };

        _cachedAccumulator = new RecordAccumulator(_options);
        _pool = new ValueTaskSourcePool<RecordMetadata>();

        // Create test data templates (we'll copy to pooled arrays for each message)
        _keyTemplate = System.Text.Encoding.UTF8.GetBytes("test-key");
        _valueTemplate = System.Text.Encoding.UTF8.GetBytes("test-value-" + new string('x', 100));
        _keyLength = _keyTemplate.Length;
        _valueLength = _valueTemplate.Length;
    }

    /// <summary>
    /// Creates a PooledMemory with a properly rented array from ArrayPool.
    /// </summary>
    private PooledMemory CreatePooledKey()
    {
        var array = ArrayPool<byte>.Shared.Rent(_keyLength);
        _keyTemplate.AsSpan().CopyTo(array);
        return new PooledMemory(array, _keyLength);
    }

    /// <summary>
    /// Creates a PooledMemory with a properly rented array from ArrayPool.
    /// </summary>
    private PooledMemory CreatePooledValue()
    {
        var array = ArrayPool<byte>.Shared.Rent(_valueLength);
        _valueTemplate.AsSpan().CopyTo(array);
        return new PooledMemory(array, _valueLength);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _cachedAccumulator.DisposeAsync().ConfigureAwait(false);
        await _pool.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(Description = "Cached: Single partition, 1000 produces")]
    public async Task CachedSinglePartition()
    {
        for (var i = 0; i < 1000; i++)
        {
            var completion = _pool.Rent();
            var key = CreatePooledKey();
            var value = CreatePooledValue();

            await _cachedAccumulator.AppendAsync(
                TestTopic,
                TestPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key,
                value,
                null,
                null,
                completion,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark(Baseline = true, Description = "Uncached (OLD): Single partition, 1000 produces")]
    public async Task UncachedSinglePartition()
    {
        // Simulate the old behavior by creating TopicPartition on every call
        for (var i = 0; i < 1000; i++)
        {
            var completion = _pool.Rent();
            var key = CreatePooledKey();
            var value = CreatePooledValue();

            // This simulates the old code path which allocated TopicPartition
            var topicPartition = new TopicPartition(TestTopic, TestPartition);

            await _cachedAccumulator.AppendAsync(
                TestTopic,
                TestPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key,
                value,
                null,
                null,
                completion,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "Cached: Multiple partitions, round-robin")]
    public async Task CachedMultiplePartitions()
    {
        for (var i = 0; i < 1000; i++)
        {
            var partition = i % PartitionCount;
            var completion = _pool.Rent();
            var key = CreatePooledKey();
            var value = CreatePooledValue();

            await _cachedAccumulator.AppendAsync(
                TestTopic,
                partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key,
                value,
                null,
                null,
                completion,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "Uncached (OLD): Multiple partitions, round-robin")]
    public async Task UncachedMultiplePartitions()
    {
        // Simulate the old behavior
        for (var i = 0; i < 1000; i++)
        {
            var partition = i % PartitionCount;
            var completion = _pool.Rent();
            var key = CreatePooledKey();
            var value = CreatePooledValue();

            // This simulates the old code path which allocated TopicPartition
            var topicPartition = new TopicPartition(TestTopic, partition);

            await _cachedAccumulator.AppendAsync(
                TestTopic,
                partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key,
                value,
                null,
                null,
                completion,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "Cached: Concurrent append (4 threads)")]
    public async Task CachedConcurrentAppend()
    {
        var tasks = new Task[4];
        for (var t = 0; t < 4; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(async () =>
            {
                for (var i = 0; i < 250; i++)
                {
                    var partition = (threadId * 250 + i) % PartitionCount;
                    var completion = _pool.Rent();
                    var key = CreatePooledKey();
                    var value = CreatePooledValue();

                    await _cachedAccumulator.AppendAsync(
                        TestTopic,
                        partition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion,
                        CancellationToken.None).ConfigureAwait(false);
                }
            });
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [Benchmark(Description = "Uncached (OLD): Concurrent append (4 threads)")]
    public async Task UncachedConcurrentAppend()
    {
        var tasks = new Task[4];
        for (var t = 0; t < 4; t++)
        {
            var threadId = t;
            tasks[t] = Task.Run(async () =>
            {
                for (var i = 0; i < 250; i++)
                {
                    var partition = (threadId * 250 + i) % PartitionCount;
                    var completion = _pool.Rent();
                    var key = CreatePooledKey();
                    var value = CreatePooledValue();

                    // This simulates the old code path which allocated TopicPartition
                    var topicPartition = new TopicPartition(TestTopic, partition);

                    await _cachedAccumulator.AppendAsync(
                        TestTopic,
                        partition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion,
                        CancellationToken.None).ConfigureAwait(false);
                }
            });
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}

/// <summary>
/// Micro-benchmarks for TopicPartition cache lookup performance.
/// Demonstrates the O(1) cache lookup vs allocation cost.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class TopicPartitionCacheLookupBenchmarks
{
    private ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartition>> _cache = null!;
    private const string TestTopic = "test-topic";
    private const int TestPartition = 5;

    [GlobalSetup]
    public void Setup()
    {
        _cache = new ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartition>>();

        // Pre-warm the cache for "cached lookup" benchmarks
        var partitionCache = _cache.GetOrAdd(TestTopic, _ => new ConcurrentDictionary<int, TopicPartition>());
        partitionCache.GetOrAdd(TestPartition, (p, t) => new TopicPartition(t, p), TestTopic);
    }

    [Benchmark(Baseline = true, Description = "Allocate TopicPartition (OLD approach)")]
    public TopicPartition AllocateTopicPartition()
    {
        return new TopicPartition(TestTopic, TestPartition);
    }

    [Benchmark(Description = "Cached lookup (warm cache)")]
    public TopicPartition CachedLookupWarm()
    {
        var partitionCache = _cache.GetOrAdd(TestTopic, _ => new ConcurrentDictionary<int, TopicPartition>());
        return partitionCache.GetOrAdd(TestPartition, (p, t) => new TopicPartition(t, p), TestTopic);
    }

    [Benchmark(Description = "Cached lookup (cold cache)")]
    public TopicPartition CachedLookupCold()
    {
        // Clear and rebuild cache on each iteration to simulate cold cache
        _cache.Clear();
        var partitionCache = _cache.GetOrAdd(TestTopic, _ => new ConcurrentDictionary<int, TopicPartition>());
        return partitionCache.GetOrAdd(TestPartition, (p, t) => new TopicPartition(t, p), TestTopic);
    }

    [Benchmark(Description = "Cached lookup: 10 partitions round-robin")]
    public void CachedLookupRoundRobin()
    {
        for (var i = 0; i < 100; i++)
        {
            var partition = i % 10;
            var partitionCache = _cache.GetOrAdd(TestTopic, _ => new ConcurrentDictionary<int, TopicPartition>());
            _ = partitionCache.GetOrAdd(partition, (p, t) => new TopicPartition(t, p), TestTopic);
        }
    }

    [Benchmark(Description = "Allocate: 10 partitions round-robin (OLD)")]
    public void AllocateRoundRobin()
    {
        for (var i = 0; i < 100; i++)
        {
            var partition = i % 10;
            _ = new TopicPartition(TestTopic, partition);
        }
    }
}
