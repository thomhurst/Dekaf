using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Attributes;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace Dekaf.Pipeline.Modules;

[RunOnLinuxOnly]
[DependsOn<BuildModule>]
public class RunBenchmarksModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var benchmarkProject = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.Benchmarks.csproj");
        if (benchmarkProject is null)
        {
            throw new InvalidOperationException("Dekaf.Benchmarks.csproj not found");
        }

        return await context.DotNet().Run(
            new DotNetRunOptions
            {
                Configuration = "Release",
                Arguments = ["--filter", "*Memory*", "--exporters", "GitHub", "CSV", "HTML"]
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = benchmarkProject.Folder!.Path
            },
            cancellationToken);
    }
}
