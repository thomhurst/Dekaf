using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public sealed class Utf8StringInternCacheTests
{
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
