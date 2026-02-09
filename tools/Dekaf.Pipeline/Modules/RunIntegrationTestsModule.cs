using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
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
    protected override ModuleConfiguration Configure()
    {
        return new ModuleConfigurationBuilder()
            .WithTimeout(TimeSpan.FromMinutes(30))
            .Build();
    }

    protected override async Task<IReadOnlyList<CommandResult>?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        // Integration tests require Docker with Linux containers
        // Skip on Windows and macOS where Kafka containers don't work
        if (!OperatingSystem.IsLinux())
        {
            context.Logger.LogInformation("Skipping integration tests on {OS} (requires Linux for Docker containers)",
                OperatingSystem.IsWindows() ? "Windows" : "macOS");
            return null;
        }

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
                Arguments = [
                    "--",
                    "--timeout", "22m",
                    "--hangdump",
                    "--hangdump-timeout", "20m", // Dump before the timeout kills the run
                    "--maximum-parallel-tests", "2", // Limit parallelism to avoid OOM on CI runners
                    "--log-level", "Trace",
                    "--output", "Detailed"
                ]
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = project.Folder!.Path,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["NET_VERSION"] = "net10.0",
                    ["DOTNET_GCConserveMemory"] = "9", // Aggressive GC to reduce memory pressure on CI
                    ["DOTNET_GCHeapHardLimit"] = "0xC0000000", // 3GB hard limit on managed heap
                }
            },
            cancellationToken);

        results.Add(testResult);

        return results;
    }
}
