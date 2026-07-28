using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class CachingStringDeserializerTests
{
    private const int KeyCacheMaxEntries = 16_384;
    private const int ValueCacheMaxEntries = 128;

    private static SerializationContext KeyContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Key };

    private static SerializationContext ValueContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    private static ReadOnlyMemory<byte> ToUtf8(string value) =>
        Encoding.UTF8.GetBytes(value);

    private static CachingStringDeserializer CreateKeyCache() =>
        new(Serializers.String, maxCachedBytes: 128, maxCachedEntries: KeyCacheMaxEntries);

    private static CachingStringDeserializer CreateValueCache() =>
        new(Serializers.String, maxCachedBytes: 4 * 1024, maxCachedEntries: ValueCacheMaxEntries);

    /// <summary>
    /// Cycles a bounded key set through the deserializer until sampled reuse promotes
    /// the cache. One pass of hits contributes about keys/stride samples, so the
    /// promotion threshold is reached in a bounded number of passes.
    /// </summary>
    private static void PromoteWithBoundedKeys(
        CachingStringDeserializer deserializer,
        SerializationContext context,
        ReadOnlyMemory<byte>[] keys)
    {
        var maxPasses = 8 * CachingStringDeserializer.ObserveSampleStride
            * CachingStringDeserializer.PromoteSampledHits / keys.Length + 16;
        for (var pass = 0; pass < maxPasses && !deserializer.IsCacheEnabled; pass++)
        {
            for (var i = 0; i < keys.Length; i++)
                deserializer.Deserialize(keys[i], context);
        }

        if (!deserializer.IsCacheEnabled)
            throw new InvalidOperationException("Bounded key reuse did not promote the cache.");
    }

    private static ReadOnlyMemory<byte>[] CreateKeys(string prefix, int count)
    {
        var keys = new ReadOnlyMemory<byte>[count];
        for (var i = 0; i < count; i++)
            keys[i] = ToUtf8($"{prefix}-{i}");
        return keys;
    }

    private static IDeserializer<string> GetDeserializerField(
        IKafkaConsumer<string, string> consumer,
        string fieldName)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");

        return (IDeserializer<string>)field.GetValue(consumer)!;
    }

    [Test]
    public async Task Cache_Uses128BitHashKeys()
    {
        var cacheField = typeof(CachingStringDeserializer).GetField(
            "_cache",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_cache field not found.");
        var entriesField = cacheField.FieldType.GetField(
            "Entries",
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Cache entries field not found.");

        var keyType = entriesField.FieldType.GetGenericArguments()[0];

        var keyWords = keyType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.FieldType == typeof(ulong))
            .ToArray();

        await Assert.That(keyWords).Count().IsEqualTo(2);
    }

    [Test]
    public async Task NewDeserializer_StartsDisabled_PlainDecodes()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = ToUtf8("my-key");

        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(sut.IsCacheEnabled).IsFalse();
        await Assert.That(first).IsEqualTo("my-key");
        // Observe mode decodes plainly; no cached instance is returned yet.
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task BoundedReuse_PromotesCache_ThenReturnsCachedReferences()
    {
        const int keyCount = 1_000;
        var sut = CreateKeyCache();
        var context = KeyContext();
        var keys = CreateKeys("bounded", keyCount);

        PromoteWithBoundedKeys(sut, context, keys);

        // One pass fills the freshly promoted cache; the next pass must hit it.
        var references = new string[keyCount];
        for (var i = 0; i < keyCount; i++)
            references[i] = sut.Deserialize(keys[i], context);

        var allReferencesCached = true;
        for (var i = 0; i < keyCount; i++)
            allReferencesCached &= ReferenceEquals(references[i], sut.Deserialize(keys[i], context));

        await Assert.That(allReferencesCached).IsTrue();
    }

    [Test]
    public async Task MidCardinalityBoundedReuse_WithinCapacity_Promotes()
    {
        // 8,000 recurring keys: more than the observe window, well within the
        // 16,384-entry capacity. The reuse-detection table is sized to capacity, so
        // this working set must produce promotion evidence.
        const int keyCount = 8_000;
        var sut = CreateKeyCache();
        var context = KeyContext();
        var keys = CreateKeys("mid", keyCount);

        PromoteWithBoundedKeys(sut, context, keys);

        await Assert.That(sut.IsCacheEnabled).IsTrue();
    }

    [Test]
    public async Task UniqueKeys_NeverPromote()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();

        for (var i = 0; i < 50_000; i++)
            sut.Deserialize(ToUtf8($"unique-{i}"), context);

        await Assert.That(sut.IsCacheEnabled).IsFalse();
    }

    [Test]
    public async Task ReuseDisappearing_DemotesCache()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        PromoteWithBoundedKeys(sut, context, CreateKeys("demote-warm", 64));

        // Admissions count as productive, so unique traffic must exhaust the cache
        // capacity before a window can score unproductive; then one more window
        // demotes, with slack.
        var uniqueLookups = KeyCacheMaxEntries + 3 * CachingStringDeserializer.DemoteWindowLookups;
        for (var i = 0; i < uniqueLookups; i++)
            sut.Deserialize(ToUtf8($"demote-unique-{i}"), context);

        await Assert.That(sut.IsCacheEnabled).IsFalse();
    }

    [Test]
    public async Task DemotedCache_RepromotesWhenReuseReturns()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var keys = CreateKeys("cycle", 64);
        PromoteWithBoundedKeys(sut, context, keys);

        var uniqueLookups = KeyCacheMaxEntries + 3 * CachingStringDeserializer.DemoteWindowLookups;
        for (var i = 0; i < uniqueLookups; i++)
            sut.Deserialize(ToUtf8($"cycle-unique-{i}"), context);
        await Assert.That(sut.IsCacheEnabled).IsFalse();

        PromoteWithBoundedKeys(sut, context, keys);

        await Assert.That(sut.IsCacheEnabled).IsTrue();
    }

    [Test]
    public async Task CapacityStarvedReuse_DoesNotPromote()
    {
        // 500 recurring keys against a 16-entry cache: the reuse-detection radius is
        // scaled to capacity, so this must stay in plain-decode mode instead of
        // flapping through promote/demote cycles.
        var sut = new CachingStringDeserializer(
            Serializers.String,
            maxCachedBytes: 128,
            maxCachedEntries: 16);
        var context = KeyContext();
        var keys = CreateKeys("starved", 500);

        for (var pass = 0; pass < 100; pass++)
        {
            for (var i = 0; i < keys.Length; i++)
                sut.Deserialize(keys[i], context);
        }

        await Assert.That(sut.IsCacheEnabled).IsFalse();
    }

    [Test]
    public async Task MaxCachedEntries_StopsNewEntries()
    {
        const int maxEntries = 16;
        var sut = new CachingStringDeserializer(
            Serializers.String,
            maxCachedBytes: 128,
            maxCachedEntries: maxEntries);
        var context = KeyContext();
        var keys = CreateKeys("cap", 8);

        PromoteWithBoundedKeys(sut, context, keys);

        // Promotion can land mid-pass, so admit the warm set explicitly, then fill
        // the remaining capacity with a second batch of distinct keys.
        for (var i = 0; i < keys.Length; i++)
            sut.Deserialize(keys[i], context);
        for (var i = 0; i < 8; i++)
        {
            var data = ToUtf8($"cap-fill-{i}");
            sut.Deserialize(data, context);
        }

        // The next unique key should still return the correct value but not be cached.
        var overflow = ToUtf8("overflow-key");
        var first = sut.Deserialize(overflow, context);
        var second = sut.Deserialize(overflow, context);

        await Assert.That(first).IsEqualTo("overflow-key");
        await Assert.That(second).IsEqualTo("overflow-key");
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task KeyLongerThan128Bytes_BypassesCache()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var longKey = new string('x', 129); // 129 ASCII chars = 129 UTF-8 bytes
        var data = ToUtf8(longKey);

        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(longKey);
        // Oversized payloads are never observed or cached; plain decode each time.
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task OversizedKeys_DoNotContributePromotionEvidence()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var oversized = ToUtf8(new string('x', 129));

        for (var i = 0; i < 20_000; i++)
            sut.Deserialize(oversized, context);

        await Assert.That(sut.IsCacheEnabled).IsFalse();
    }

    [Test]
    public async Task EmptyData_BypassesCache()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();

        var result = sut.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ObserveMode_AllocatesOnlyTheDecodedString()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        // Distinct warmup and measurement key ranges: replaying the warmup keys would
        // be genuine reuse and could legitimately promote the cache mid-measurement.
        var warmupKeys = CreateKeys("alloc-warm", 8_192);
        var keys = CreateKeys("alloc", 8_192);

        // Warm the code paths so JIT/tiering allocations do not pollute the measurement.
        for (var i = 0; i < warmupKeys.Length; i++)
            sut.Deserialize(warmupKeys[i], context);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < keys.Length; i++)
            sut.Deserialize(keys[i], context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Each lookup allocates one short string (~40 B); sampling and the recent-hash
        // table must add nothing per lookup.
        await Assert.That(allocated).IsLessThan(keys.Length * 80L);
    }

    [Test]
    public async Task ConcurrentAccess_ReturnsCorrectValues()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var keys = Enumerable.Range(0, 100).Select(i => $"concurrent-key-{i}").ToArray();

        // Run concurrent deserialization from multiple threads.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 1000),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            (i, _) =>
            {
                var key = keys[i % keys.Length];
                var data = ToUtf8(key);
                var result = sut.Deserialize(data, context);
                if (result != key)
                    throw new InvalidOperationException($"Expected '{key}' but got '{result}'");
                return ValueTask.CompletedTask;
            });

        // Verify all keys still decode correctly after concurrent access.
        foreach (var key in keys)
        {
            var result = sut.Deserialize(ToUtf8(key), context);
            await Assert.That(result).IsEqualTo(key);
        }
    }

    [Test]
    public async Task ValueCache_RepeatedPayload_CachesAfterPromotion()
    {
        var sut = CreateValueCache();
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

        PromoteWithBoundedKeys(sut, context, [data]);

        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task ValueLongerThan4096Bytes_BypassesCache()
    {
        var sut = CreateValueCache();
        var context = ValueContext();
        var payload = new string('x', 4097);
        var data = ToUtf8(payload);

        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ConsumerBuilder_DefaultStringKeyDeserializer_StartsInObserveMode()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("cache-test")
            .Build();

        var deserializer = GetDeserializerField(consumer, "_keyDeserializer");

        var caching = deserializer as CachingStringDeserializer;
        await Assert.That(caching).IsNotNull();
        await Assert.That(caching!.IsCacheEnabled).IsFalse();
    }

    [Test]
    public async Task ConsumerBuilder_DefaultStringValueDeserializer_DoesNotCacheValues()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("cache-test")
            .Build();

        var deserializer = GetDeserializerField(consumer, "_valueDeserializer");
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

        var first = deserializer.Deserialize(data, context);
        var second = deserializer.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ConsumerBuilder_WithCachedStringValues_CachesAfterPromotion()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("cache-test")
            .WithCachedStringValues()
            .Build();

        var deserializer = (CachingStringDeserializer)GetDeserializerField(consumer, "_valueDeserializer");
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

        PromoteWithBoundedKeys(deserializer, context, [data]);

        var first = deserializer.Deserialize(data, context);
        var second = deserializer.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }
}
