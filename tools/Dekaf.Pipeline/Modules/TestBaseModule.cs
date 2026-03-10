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
                    Configuration = "Debug",
                    Framework = framework,
                    Arguments = [
                        "--",
                            "--hangdump",
                            "--hangdump-timeout", "15m",
                            "--log-level", "Debug",
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
