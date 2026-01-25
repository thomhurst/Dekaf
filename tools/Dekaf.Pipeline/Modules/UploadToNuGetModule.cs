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

        foreach (var package in packedProjects)
        {
            var nupkgFile = context.Git().RootDirectory.FindFile(x => x.Name == $"{package.Name}.{package.Version}.nupkg");

            if (nupkgFile is null)
            {
                context.Logger.LogWarning("Package file not found for {PackageName}", package.Name);
                continue;
            }

            context.Logger.LogInformation("Uploading {PackageName}", package.Name);

            var result = await context.DotNet().Nuget.Push(new DotNetNugetPushOptions
            {
                Path = nupkgFile.Path,
                Source = options.Source,
                ApiKey = options.ApiKey,
                SkipDuplicate = true
            }, null, cancellationToken);

            results.Add(result);
        }

        return results;
    }
}
