using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

[DependsOn<GenerateVersionModule>]
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

        return await context.DotNet().Build(new DotNetBuildOptions
        {
            ProjectSolution = solutionFile.Path,
            Configuration = "Release",
            Properties =
            [
                new KeyValue("Version", version),
                new KeyValue("TreatWarningsAsErrors", "true")
            ]
        }, null, cancellationToken);
    }
}
