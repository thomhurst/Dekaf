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
        // First check if GitVersion environment variables are already set (e.g., by GitHub Actions)
        // This avoids re-running GitVersion which can fail on PR branches due to GitVersion 6.x bugs
        var envSemVer = Environment.GetEnvironmentVariable("GitVersion_SemVer");
        var envFullSemVer = Environment.GetEnvironmentVariable("GitVersion_FullSemVer");

        if (!string.IsNullOrEmpty(envSemVer) || !string.IsNullOrEmpty(envFullSemVer))
        {
            var semVer = envSemVer ?? envFullSemVer ?? "1.0.0";
            context.Logger.LogInformation("Using GitVersion from environment: {SemVer}", semVer);
            return new VersionInfo(semVer, semVer);
        }

        // Fall back to running GitVersion directly (for local development)
        try
        {
            var gitVersionInformation = await context.Git().Versioning.GetGitVersioningInformation();
            var semVer = gitVersionInformation.SemVer ?? gitVersionInformation.FullSemVer ?? "1.0.0";
            context.Logger.LogInformation("Version is: {SemVer}", semVer);
            return new VersionInfo(semVer, semVer);
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "GitVersion failed, using fallback version");
            return new VersionInfo("0.0.1-local", "0.0.1-local");
        }
    }
}
