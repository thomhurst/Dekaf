using Microsoft.Extensions.Logging;
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
public class RunIntegrationTestsModule : Module<IReadOnlyList<CommandResult>>
{
    protected override async Task<IReadOnlyList<CommandResult>?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();

        var project = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.Tests.Integration.csproj");
        if (project is null)
        {
            throw new InvalidOperationException("Dekaf.Tests.Integration.csproj not found");
        }

        var testResult = await context.DotNet().Run(
            new DotNetRunOptions
            {
                NoBuild = true,
                Configuration = "Release",
                Framework = "net10.0",
                Arguments = ["--", "--log-level", "Trace", "--output", "Detailed"]
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = project.Folder!.Path,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["NET_VERSION"] = "net10.0",
                }
            },
            cancellationToken);

        results.Add(testResult);

        return results;
    }
}
