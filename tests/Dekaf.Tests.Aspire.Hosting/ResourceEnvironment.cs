using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Dekaf.Tests.Aspire.Hosting;

internal static class ResourceEnvironment
{
    /// <summary>Evaluates a resource's environment variables for the given operation.</summary>
    internal static ValueTask<Dictionary<string, string>> GetAsync(IResourceWithEnvironment resource, DistributedApplicationOperation operation)
    {
        // Aspire recommends ExecutionConfigurationBuilder, but this remains the simplest way to inspect variables in tests.
#pragma warning disable CS0618
        return resource.GetEnvironmentVariableValuesAsync(operation);
#pragma warning restore CS0618
    }
}
