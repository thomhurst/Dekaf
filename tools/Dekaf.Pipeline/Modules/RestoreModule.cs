using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;

namespace Dekaf.Pipeline.Modules;

/// <summary>
/// Restores NuGet packages for the solution.
/// This must run first to avoid parallel restore conflicts.
/// </summary>
public class RestoreModule : Module<CommandResult>
{
    protected override async Task<CommandResult> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var solutionFile = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.FindFile(x => x.Name == "Dekaf.sln");
        if (solutionFile is null)
        {
            throw new InvalidOperationException("Dekaf.sln not found");
        }

        return await context.Tools.DotNet.RestoreAsync(new DotNetRestoreOptions
        {
            ProjectSolution = solutionFile.Path
        }, null, cancellationToken);
    }
}
