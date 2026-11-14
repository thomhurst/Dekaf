using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;

namespace Dekaf.Pipeline.Modules;

[DependsOn<GenerateVersionModule>]
[DependsOn<RestoreModule>]
public class BuildModule : Module<CommandResult>
{
    protected override async Task<CommandResult> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer ?? "1.0.0";

        var solutionFile = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.FindFile(x => x.Name == "Dekaf.sln");
        if (solutionFile is null)
        {
            throw new InvalidOperationException("Dekaf.sln not found");
        }

        return await context.Tools.DotNet.BuildAsync(new DotNetBuildOptions
        {
            ProjectSolution = solutionFile.Path,
            Configuration = "Release",
            NoRestore = true,
            Properties =
            [
                new KeyValue("Version", version),
                new KeyValue("ContinuousIntegrationBuild", "true"),
                new KeyValue("Deterministic", "true"),
                new KeyValue("TreatWarningsAsErrors", "true")
            ]
        }, null, cancellationToken);
    }
}
