using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git;

namespace Dekaf.Pipeline.Modules;

// Publishing runs only for a published GitHub release (see .github/workflows/release-draft.yml):
// the release's tag is the version, and GitHub has already created the tag and the release. A
// branch gate such as RunOnlyOnBranch("main") would skip every release run, which checks out the
// tag on a detached HEAD.
[DependsOn<PackModule>]
public class UploadToNuGetModule(IOptions<NuGetOptions> nuGetOptions) : Module<List<CommandResult>>
{
    protected override void Configure(ModuleConfigurationBuilder module)
    {
        module.WithSkipWhen(_ => Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") == "release"
            ? SkipDecision.DoNotSkip
            : SkipDecision.Skip("Not a published release"));
    }

    protected override async Task<List<CommandResult>> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var options = nuGetOptions.Value;

        if (!options.ShouldPublish)
        {
            context.Logger.LogInformation("NuGet publishing is disabled");
            return [];
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            context.Logger.LogWarning("NUGET_API_KEY is not set - skipping NuGet upload");
            return [];
        }

        var packModule = await context.GetModule<PackModule>();
        var packedProjects = packModule.ValueOrDefault ?? [];

        if (packedProjects.Count == 0)
        {
            context.Logger.LogWarning("No packages found to upload");
            return [];
        }

        // Holds only the packages that were newly published this run (duplicates are excluded).
        var newlyPublished = new List<CommandResult>();
        var duplicates = 0;

        foreach (var package in packedProjects)
        {
            var nupkgFile = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.FindFile(x => x.Name == $"{package.Name}.{package.Version}.nupkg");

            if (nupkgFile is null)
            {
                context.Logger.LogWarning("Package file not found for {PackageName}", package.Name);
                continue;
            }

            context.Logger.LogInformation("Uploading {PackageName}", package.Name);

            var result = await PushPackageAsync(nupkgFile.Path);

            // A non-duplicate push failure throws (non-zero exit), so anything reaching here either
            // published or was skipped as a duplicate. --skip-duplicate reports "already exists" on
            // a conflict; treat its absence as a genuine new publish.
            var isDuplicate =
                result.StandardOutput.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains("already exists", StringComparison.OrdinalIgnoreCase);
            if (isDuplicate)
            {
                duplicates++;
                context.Logger.LogWarning(
                    "Package {PackageName} {Version} already exists on the feed - skipped as duplicate",
                    package.Name, package.Version);
            }
            else
            {
                newlyPublished.Add(result);
                context.Logger.LogInformation("Published {PackageName} {Version}", package.Name, package.Version);
            }
        }

        // Guard against silent no-op releases: the job succeeds but ships nothing because every
        // package version already exists (the release's tag names a version NuGet already has) or
        // no package file was found. Fail loudly instead. A re-run of a partly published release
        // still passes: the packages it had not pushed yet are new.
        if (newlyPublished.Count == 0)
        {
            var reason = duplicates > 0
                ? $"every push was a duplicate of an already-published version ({packedProjects[0].Version})"
                : "no package files were found to push";

            throw new InvalidOperationException(
                $"NuGet publish ran but 0 of {packedProjects.Count} packages were newly published - {reason}.");
        }

        context.Logger.LogInformation(
            "NuGet publish complete: {NewlyPublished} newly published, {Duplicates} skipped as duplicates",
            newlyPublished.Count, duplicates);

        return newlyPublished;

        Task<CommandResult> PushPackageAsync(string packagePath)
        {
            return context.Tools.DotNet.NuGet.PushAsync(new DotNetNuGetPushOptions([packagePath])
            {
                Source = options.Source,
                ApiKey = options.ApiKey,
                SkipDuplicate = true
            }, null, cancellationToken);
        }
    }
}
