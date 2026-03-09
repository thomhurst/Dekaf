using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

[DependsOn<GenerateVersionModule>]
[DependsOn<RestoreModule>]
public class BuildModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer ?? "1.0.0";

        var solutionFile = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.sln");
        if (solutionFile is null)
        {
            throw new InvalidOperationException("Dekaf.sln not found");
        }

        var properties = new KeyValue[]
        {
            new("Version", version),
            new("TreatWarningsAsErrors", "true")
        };

        // Build Release for packaging
        var releaseResult = await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = solutionFile.Path,
            Configuration = "Release",
            NoRestore = true,
            Properties = properties
        }, null, cancellationToken);

        // Build Debug for tests (enables #if DEBUG diagnostic tracking)
        await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = solutionFile.Path,
            Configuration = "Debug",
            NoRestore = true,
            Properties = properties
        }, null, cancellationToken);

        return releaseResult;
    }
}
