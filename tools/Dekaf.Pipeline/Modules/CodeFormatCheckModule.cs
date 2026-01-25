using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

public class CodeFormatCheckModule : Module<CommandResult>
{
    protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var solutionFile = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.sln");
        if (solutionFile is null)
        {
            throw new InvalidOperationException("Dekaf.sln not found");
        }

        return await context.DotNet().Format(new DotNetFormatOptions
        {
            ProjectSolution = solutionFile.Path,
            VerifyNoChanges = true
        }, null, cancellationToken);
    }
}
