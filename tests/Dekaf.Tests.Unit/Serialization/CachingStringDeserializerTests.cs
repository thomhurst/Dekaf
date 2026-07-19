using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class CachingStringDeserializerTests
{
    private const int KeyCacheMaxEntries = 16_384;

    private static SerializationContext KeyContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Key };

    private static SerializationContext ValueContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

    private static ReadOnlyMemory<byte> ToUtf8(string value) =>
        Encoding.UTF8.GetBytes(value);

    private static CachingStringDeserializer CreateKeyCache() =>
        new(Serializers.String, maxCachedBytes: 128, maxCachedEntries: KeyCacheMaxEntries);

    private static void EnterHighCardinalityBypass(
        CachingStringDeserializer deserializer,
        SerializationContext context)
    {
        var lookupCount = CachingStringDeserializer.AdmissionProbeLimit
            + CachingStringDeserializer.ProbeLookupCount
            + KeyCacheMaxEntries;

        for (var uniqueKey = 0; uniqueKey < lookupCount; uniqueKey++)
            deserializer.Deserialize(ToUtf8($"unique-{uniqueKey}"), context);
    }

    private static CachingStringDeserializer CreateValueCache() =>
        new(Serializers.String, maxCachedBytes: 4 * 1024, maxCachedEntries: 128);

    private static IDeserializer<string> GetValueDeserializer(IKafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_valueDeserializer",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_valueDeserializer field not found.");

        return (IDeserializer<string>)field.GetValue(consumer)!;
    }

    [Test]
    public async Task Cache_Uses128BitHashKeys()
    {
        var cacheField = typeof(CachingStringDeserializer).GetField(
            "_cache",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_cache field not found.");

        var keyType = cacheField.FieldType.GetGenericArguments()[0];

        var keyWords = keyType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.FieldType == typeof(ulong))
            .ToArray();

        await Assert.That(keyWords).Count().IsEqualTo(2);
    }

    [Test]
    public async Task CacheHit_ReturnsSameReference()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = ToUtf8("my-key");

        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(first).IsEqualTo("my-key");
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
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
        // Both return correct values, but they should be different string instances
        // because the cache was bypassed (inner deserializer allocates each time).
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
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

        for (var i = 0; i < maxEntries; i++)
        {
            var data = ToUtf8($"key-{i}");
            sut.Deserialize(data, context);
            sut.Deserialize(data, context);
        }

        // The next unique key should still return the correct value but not be cached.
        var overflow = ToUtf8("overflow-key");
        var first = sut.Deserialize(overflow, context);
        var second = sut.Deserialize(overflow, context);

        await Assert.That(first).IsEqualTo("overflow-key");
        await Assert.That(second).IsEqualTo("overflow-key");
        // Not cached — different string instances from the inner deserializer.
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task HighCardinalityKeys_BypassCacheAfterLowHitRateProbe()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();

        EnterHighCardinalityBypass(sut, context);

        var data = ToUtf8("bypassed");
        var first = sut.Deserialize(data, context);
        var second = sut.Deserialize(data, context);

        await Assert.That(first).IsEqualTo("bypassed");
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task BoundedReusableKeys_RemainCachedAfterHitRateProbe()
    {
        const int keyCount = 1_000;
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = new ReadOnlyMemory<byte>[keyCount];
        var references = new string[keyCount];

        for (var i = 0; i < keyCount; i++)
        {
            data[i] = ToUtf8($"bounded-{i}");
            references[i] = sut.Deserialize(data[i], context);
        }

        // Complete the 1,024-lookup probe with enough reuse to exceed its 10% hit gate.
        var repeatedLookups = CachingStringDeserializer.ProbeLookupCount
            - (keyCount - CachingStringDeserializer.AdmissionProbeLimit);
        for (var i = 0; i < repeatedLookups; i++)
            sut.Deserialize(data[i], context);

        var allReferencesCached = true;
        for (var i = 0; i < keyCount; i++)
            allReferencesCached &= ReferenceEquals(references[i], sut.Deserialize(data[i], context));

        await Assert.That(allReferencesCached).IsTrue();
    }

    [Test]
    public async Task BoundedReusableKeys_LargerThanPrimaryProbe_AreAdmittedDuringReuseProbe()
    {
        const int keyCount = 5_000;
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = new ReadOnlyMemory<byte>[keyCount];
        var references = new string[keyCount];

        for (var i = 0; i < keyCount; i++)
        {
            data[i] = ToUtf8($"bounded-reuse-{i}");
            references[i] = sut.Deserialize(data[i], context);
        }

        var allReferencesCached = true;
        for (var i = 0; i < keyCount; i++)
            allReferencesCached &= ReferenceEquals(references[i], sut.Deserialize(data[i], context));

        await Assert.That(allReferencesCached).IsTrue();
    }

    [Test]
    public async Task LowCardinalityKeys_RemainCachedPastAdmissionProbeLimit()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = ToUtf8("repeated");
        var first = sut.Deserialize(data, context);
        var allReferencesCached = true;

        for (var i = 0; i < CachingStringDeserializer.AdmissionProbeLimit * 2; i++)
            allReferencesCached &= ReferenceEquals(first, sut.Deserialize(data, context));

        await Assert.That(allReferencesCached).IsTrue();
    }

    [Test]
    public async Task Bypass_ReprobesAndRecoversWhenKeysBecomeRepetitive()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();

        EnterHighCardinalityBypass(sut, context);

        var repeated = ToUtf8("new-repeated-key");
        for (var i = 0; i < CachingStringDeserializer.BypassInterval; i++)
            sut.Deserialize(repeated, context);

        var firstProbe = sut.Deserialize(repeated, context);
        var secondProbe = sut.Deserialize(repeated, context);

        await Assert.That(ReferenceEquals(firstProbe, secondProbe)).IsTrue();
    }

    [Test]
    public async Task TwoDistinctKeys_BothCachedCorrectly()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();

        var dataA = ToUtf8("key-alpha");
        var dataB = ToUtf8("key-bravo");

        var resultA1 = sut.Deserialize(dataA, context);
        var resultB1 = sut.Deserialize(dataB, context);
        var resultA2 = sut.Deserialize(dataA, context);
        var resultB2 = sut.Deserialize(dataB, context);

        await Assert.That(resultA1).IsEqualTo("key-alpha");
        await Assert.That(resultA2).IsEqualTo("key-alpha");
        await Assert.That(resultB1).IsEqualTo("key-bravo");
        await Assert.That(resultB2).IsEqualTo("key-bravo");

        await Assert.That(ReferenceEquals(resultA1, resultA2)).IsTrue();
        await Assert.That(ReferenceEquals(resultB1, resultB2)).IsTrue();
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

        // Verify all keys are correctly cached after concurrent access.
        foreach (var key in keys)
        {
            var result = sut.Deserialize(ToUtf8(key), context);
            await Assert.That(result).IsEqualTo(key);
        }
    }

    [Test]
    public async Task EmptyData_BypassesCache()
    {
        var sut = CreateKeyCache();
        var context = KeyContext();
        var data = ReadOnlyMemory<byte>.Empty;

        var result = sut.Deserialize(data, context);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ValueCache_Repeated1000BytePayload_ReturnsSameReference()
    {
        var sut = CreateValueCache();
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

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
    public async Task ConsumerBuilder_DefaultStringValueDeserializer_DoesNotCacheValues()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("cache-test")
            .Build();

        var deserializer = GetValueDeserializer(consumer);
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

        var first = deserializer.Deserialize(data, context);
        var second = deserializer.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    [Test]
    public async Task ConsumerBuilder_WithCachedStringValues_UsesBoundedCache()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("cache-test")
            .WithCachedStringValues()
            .Build();

        var deserializer = GetValueDeserializer(consumer);
        var context = ValueContext();
        var payload = new string('x', 1000);
        var data = ToUtf8(payload);

        var first = deserializer.Deserialize(data, context);
        var second = deserializer.Deserialize(data, context);

        await Assert.That(first).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }
}
