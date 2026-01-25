using ModularPipelines.Attributes;

namespace Dekaf.Pipeline;

public sealed class NuGetOptions
{
    [SecretValue]
    public string? ApiKey { get; set; }

    public bool ShouldPublish { get; set; }

    public string Source { get; set; } = "https://api.nuget.org/v3/index.json";
}
