using Microsoft.Extensions.Logging;
using ModularPipelines;
using ModularPipelines.Context;

namespace Dekaf.Pipeline.Modules;

public record VersionInfo(string SemVer, string? NuGetVersion);

public class GenerateVersionModule : Module<VersionInfo>
{
    protected override async Task<VersionInfo> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        // In CI, .github/scripts/resolve-version.sh has already chosen the version and exported it
        // as DEKAF_VERSION: the tag of a published release, or the version GitVersion computed for
        // any other run. Reuse it so the packages carry the version the workflow resolved.
        var resolvedVersion = Environment.GetEnvironmentVariable("DEKAF_VERSION");

        if (!string.IsNullOrEmpty(resolvedVersion))
        {
            context.Logger.LogInformation("Using the version CI resolved: {SemVer}", resolvedVersion);
            return new VersionInfo(resolvedVersion, resolvedVersion);
        }

        // A release must ship the version of its tag; never fall back to a computed one.
        if (Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") == "release")
        {
            throw new InvalidOperationException(
                "DEKAF_VERSION is not set on a release run; run .github/scripts/resolve-version.sh first.");
        }

        // Local development, or a CI run where GitVersion failed: run GitVersion directly. SemVer
        // excludes the +build metadata (which NuGet ignores), so it is the value we pack.
        try
        {
            var gitVersionInformation = await context.Tools.Git.Versioning.GetVersioningInformationAsync(cancellationToken);
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
