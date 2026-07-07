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
        // Use the version GitVersion computed directly. +semver: bump messages drive Major/Minor/Patch,
        // and the v{version} release tags CreateReleaseModule pushes act as GitVersion's version
        // sources, so each publishing commit on main advances the patch and gets a fresh, unique
        // version. SemVer excludes the +build metadata (which NuGet ignores), so it is the value we
        // pack and publish.
        //
        // NOTE: uniqueness relies on every release pushing a v{version} tag. If a release ships
        // packages without creating its tag, the next run can recompute the same version, and
        // UploadToNuGetModule throws once every push is a duplicate (see its guard). Keep the tag and
        // publish steps together.
        var envSemVer = Environment.GetEnvironmentVariable("GitVersion_SemVer");
        var envFullSemVer = Environment.GetEnvironmentVariable("GitVersion_FullSemVer");

        if (!string.IsNullOrEmpty(envSemVer) || !string.IsNullOrEmpty(envFullSemVer))
        {
            var semVer = envSemVer ?? envFullSemVer!;
            context.Logger.LogInformation("Using GitVersion from environment: {SemVer}", semVer);
            return new VersionInfo(semVer, semVer);
        }

        // Fall back to running GitVersion directly (local development, where the CI action hasn't
        // exported the GitVersion_* environment variables).
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
