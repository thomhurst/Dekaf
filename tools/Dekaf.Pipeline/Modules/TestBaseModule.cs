using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace Dekaf.Pipeline.Modules;

[DependsOn<BuildModule>]
public abstract class TestBaseModule : Module<IReadOnlyList<CommandResult>>
{
    protected virtual IEnumerable<string> TestableFrameworks
    {
        get
        {
            yield return "net10.0";
        }
    }

    protected override ModuleConfiguration Configure()
    {
        return new ModuleConfigurationBuilder()
            .WithTimeout(TimeSpan.FromMinutes(30))
            .Build();
    }

    protected abstract string ProjectFileName { get; }

    protected sealed override async Task<IReadOnlyList<CommandResult>?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        // When SKIP_UNIT_TESTS is set, skip unit tests (used by integration-tests CI job)
        if (string.Equals(Environment.GetEnvironmentVariable("SKIP_UNIT_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogInformation("Skipping unit tests (SKIP_UNIT_TESTS=true)");
            return null;
        }

        var results = new List<CommandResult>();

        foreach (var framework in TestableFrameworks)
        {
            var project = context.Git().RootDirectory.FindFile(x => x.Name == ProjectFileName);
            if (project is null)
            {
                throw new InvalidOperationException($"Project {ProjectFileName} not found");
            }

            var testResult = await context.DotNet().Run(
                new DotNetRunOptions
                {
                    NoBuild = true,
                    Configuration = "Release",
                    Framework = framework,
                    Arguments = [
                        "--",
                            "--hangdump",
                            "--hangdump-timeout", "15m",
                            "--log-level", "Trace",
                            "--output", "Detailed"
                    ]
                },
                new CommandExecutionOptions
                {
                    WorkingDirectory = project.Folder!.Path,
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["NET_VERSION"] = framework,
                    }
                },
                cancellationToken);

            results.Add(testResult);
        }

        return results;
    }
}
