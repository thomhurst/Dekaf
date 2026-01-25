using Microsoft.Extensions.Logging;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

public record VersionInfo(string SemVer, string? NuGetVersion);

public class GenerateVersionModule : Module<VersionInfo>
{
    protected override async Task<VersionInfo?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var gitVersionInformation = await context.Git().Versioning.GetGitVersioningInformation();

        var semVer = gitVersionInformation.SemVer ?? gitVersionInformation.FullSemVer ?? "1.0.0";

        context.Logger.LogInformation("Version is: {SemVer}", semVer);

        return new VersionInfo(semVer, semVer);
    }
}
