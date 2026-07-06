using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Attributes;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

[RunOnlyOnBranch("main")]
[DependsOn<PackModule>]
public class UploadToNuGetModule(IOptions<NuGetOptions> nuGetOptions) : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
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

        var results = new List<CommandResult>();
        var newlyPublished = 0;
        var duplicates = 0;

        foreach (var package in packedProjects)
        {
            var nupkgFile = context.Git().RootDirectory.FindFile(x => x.Name == $"{package.Name}.{package.Version}.nupkg");

            if (nupkgFile is null)
            {
                context.Logger.LogWarning("Package file not found for {PackageName}", package.Name);
                continue;
            }

            context.Logger.LogInformation("Uploading {PackageName}", package.Name);

            var result = await PushPackageAsync(nupkgFile.Path);
            results.Add(result);

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
                newlyPublished++;
                context.Logger.LogInformation("Published {PackageName} {Version}", package.Name, package.Version);
            }
        }

        // Guard against silent no-op releases: the job succeeds but ships nothing because every
        // package version already exists (e.g. the version stopped advancing). Fail loudly instead.
        if (newlyPublished == 0)
        {
            var reason = duplicates > 0
                ? $"every push was a duplicate of an already-released version ({packedProjects[0].Version}); the package version likely did not advance"
                : "no package files were found to push";

            throw new InvalidOperationException(
                $"NuGet publish ran but 0 of {packedProjects.Count} packages were newly published - {reason}.");
        }

        context.Logger.LogInformation(
            "NuGet publish complete: {NewlyPublished} newly published, {Duplicates} skipped as duplicates",
            newlyPublished, duplicates);

        return results;

        Task<CommandResult> PushPackageAsync(string packagePath)
        {
            return context.DotNet().Nuget.Push(new DotNetNugetPushOptions
            {
                Path = packagePath,
                Source = options.Source,
                ApiKey = options.ApiKey,
                SkipDuplicate = true
            }, null, cancellationToken);
        }
    }
}
