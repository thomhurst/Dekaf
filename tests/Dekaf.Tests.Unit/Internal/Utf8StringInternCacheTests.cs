using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public sealed class Utf8StringInternCacheTests
{
    [Test]
    public async Task InternUncached_OversizedKeyRetainsDecodeOnlyBehavior()
    {
        var cache = new Utf8StringInternCache(maxCachedEntries: 1, maxCachedBytes: 4);
        var hit = cache.TryGetCached("longer"u8, out _, out var hash);
        var first = cache.InternUncached("longer"u8, hash);
        var second = cache.InternUncached("longer"u8, hash);
        var cached = cache.TryGetCached("longer"u8, out _, out _);

        await Assert.That(hit).IsFalse();
        await Assert.That(cached).IsFalse();
        await Assert.That(first).IsEqualTo("longer");
        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second).IsNotSameReferenceAs(first);
    }

    [Test]
    public async Task InternUncached_UsesLookupHashAndPreservesCompetingAdmission()
    {
        var cache = new Utf8StringInternCache(maxCachedEntries: 3, maxCachedBytes: 64);
        var miss = cache.TryGetCached("first"u8, out _, out var firstHash);
        var first = cache.InternUncached("first"u8, firstHash);
        var cached = cache.Intern("first"u8);
        var secondMiss = cache.TryGetCached("second"u8, out _, out var secondHash);
        var competing = cache.Intern("second"u8);
        var second = cache.InternUncached("second"u8, secondHash);

        await Assert.That(miss).IsFalse();
        await Assert.That(secondMiss).IsFalse();
        await Assert.That(cached).IsSameReferenceAs(first);
        await Assert.That(second).IsSameReferenceAs(competing);
    }

    [Test]
    public async Task TryGetCached_MissesDoNotDecodeOrConsumeCapacity()
    {
        var cache = new Utf8StringInternCache(maxCachedEntries: 1, maxCachedBytes: 6);
        var missing = cache.TryGetCached("absent"u8, out _, out _);
        var tooLong = cache.TryGetCached("oversize"u8, out _, out _);
        var empty = cache.TryGetCached([], out var emptyValue, out _);
        var first = cache.Intern("cached"u8);
        var hit = cache.TryGetCached("cached"u8, out var cached, out _);
        _ = cache.Intern("filled"u8);
        var overflow = cache.TryGetCached("filled"u8, out _, out _);

        await Assert.That(missing).IsFalse();
        await Assert.That(tooLong).IsFalse();
        await Assert.That(empty).IsTrue();
        await Assert.That(emptyValue).IsEqualTo(string.Empty);
        await Assert.That(hit).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(first);
        await Assert.That(overflow).IsFalse();
    }

    [Test]
    public async Task Intern_EntryLimitExceeded_DecodesWithoutCaching()
    {
        var cache = new Utf8StringInternCache(maxCachedEntries: 1, maxCachedBytes: 64);
        var cachedBytes = "cached"u8.ToArray();
        var overflowBytes = "overflow"u8.ToArray();

        var cached = cache.Intern(cachedBytes);
        var cachedAgain = cache.Intern(cachedBytes);
        var overflow = cache.Intern(overflowBytes);
        var overflowAgain = cache.Intern(overflowBytes);

        await Assert.That(cachedAgain).IsSameReferenceAs(cached);
        await Assert.That(overflow).IsEqualTo("overflow");
        await Assert.That(overflowAgain).IsEqualTo("overflow");
        await Assert.That(overflowAgain).IsNotSameReferenceAs(overflow);
    }
}
