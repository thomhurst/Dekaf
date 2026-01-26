using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

/// <summary>
/// Restores NuGet packages for the solution.
/// This must run first to avoid parallel restore conflicts.
/// </summary>
public class RestoreModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var solutionFile = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.sln");
        if (solutionFile is null)
        {
            throw new InvalidOperationException("Dekaf.sln not found");
        }

        return await context.DotNet().Restore(new DotNetRestoreOptions
        {
            ProjectSolution = solutionFile.Path
        }, null, cancellationToken);
    }
}
