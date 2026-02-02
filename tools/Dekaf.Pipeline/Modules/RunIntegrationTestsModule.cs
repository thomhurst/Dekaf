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

        // Add 15-minute pipeline timeout as safety fallback
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var testResult = await context.DotNet().Run(
                new DotNetRunOptions
                {
                    NoBuild = true,
                    Configuration = "Release",
                    Framework = "net10.0",
                    Arguments = ["--", "--timeout", "10m", "--log-level", "Trace", "--output", "Detailed"]
                },
                new CommandExecutionOptions
                {
                    WorkingDirectory = project.Folder!.Path,
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["NET_VERSION"] = "net10.0",
                    }
                },
                linkedCts.Token);

            results.Add(testResult);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Integration test execution exceeded 15 minute pipeline timeout");
        }

        return results;
    }
}
