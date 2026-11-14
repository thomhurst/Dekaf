using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Options;

namespace Dekaf.Pipeline.Modules;

/// <summary>
/// Runs the memory benchmarks. Opt-in only: registered when <c>RUN_BENCHMARKS=true</c>, because the
/// run takes about 20 minutes and no other module or workflow consumes its output.
/// </summary>
[DependsOn<BuildModule>]
public class RunBenchmarksModule : Module<CommandResult>
{
    protected override async Task<CommandResult> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var benchmarkProject = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.FindFile(x => x.Name == "Dekaf.Benchmarks.csproj");
        if (benchmarkProject is null)
        {
            throw new InvalidOperationException("Dekaf.Benchmarks.csproj not found");
        }

        return await context.Tools.DotNet.RunAsync(
            new DotNetRunOptions
            {
                Configuration = "Release",
                NoBuild = true,
                // Match the performance gate's allowance for building the generated benchmark harness.
                Arguments = ["--filter", "*Memory*", "--exporters", "GitHub", "CSV", "HTML", "--buildTimeout", "900"]
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = benchmarkProject.Folder!.Path
            },
            cancellationToken);
    }
}
