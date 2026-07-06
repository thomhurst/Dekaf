using System.Globalization;
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
        // Prefer a monotonic, unique-per-commit stable version: {Major}.{Minor}.{commit height}.
        // Major/Minor come from GitVersion (so +semver bump messages still work); the height is the
        // commit count, which strictly increases on main so every publish gets a fresh version.
        // Without this, GitVersion emits a frozen MajorMinorPatch (1.0.0) on every commit, which
        // made each NuGet push a duplicate that --skip-duplicate silently swallowed (nothing shipped).
        var major = Environment.GetEnvironmentVariable("GitVersion_Major");
        var minor = Environment.GetEnvironmentVariable("GitVersion_Minor");

        if (!string.IsNullOrEmpty(major) && !string.IsNullOrEmpty(minor))
        {
            // Use commit height from the repository root (git rev-list --count HEAD) rather than
            // GitVersion's CommitsSinceVersionSource. The latter resets whenever a version-shaped tag
            // is the nearest version source, so the v{version} release tags CreateReleaseModule pushes
            // would make the height — and thus the composed version — jump backwards. Commit height is
            // strictly increasing and tag-independent. Falls back to the GitVersion value if git can't
            // be queried (identical to the root count while no such tags exist).
            var commitsOnBranch = context.Git().Information.CommitsOnBranch;
            var height = commitsOnBranch > 0
                ? commitsOnBranch.ToString(CultureInfo.InvariantCulture)
                : Environment.GetEnvironmentVariable("GitVersion_CommitsSinceVersionSource");

            if (!string.IsNullOrEmpty(height))
            {
                var version = $"{major}.{minor}.{height}";
                context.Logger.LogInformation("Using commit-height version: {Version}", version);
                return new VersionInfo(version, version);
            }
        }

        // Fallback for when the Major/Minor/height variables are unavailable (e.g. GitVersion only
        // partially populated the environment): use the SemVer it computed directly. This still
        // avoids re-running GitVersion, which can fail on PR branches due to GitVersion 6.x bugs.
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
