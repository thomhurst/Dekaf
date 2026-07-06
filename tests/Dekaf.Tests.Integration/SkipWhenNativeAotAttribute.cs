using System.Runtime.CompilerServices;

namespace Dekaf.Tests.Integration;

public sealed class SkipWhenNativeAotAttribute(string reason) : SkipAttribute(reason)
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!RuntimeFeature.IsDynamicCodeSupported);
    }
}
