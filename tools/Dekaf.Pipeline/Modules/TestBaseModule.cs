using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Options;

namespace Dekaf.Pipeline.Modules;

[DependsOn<BuildModule>]
public abstract class TestBaseModule : Module<IReadOnlyList<CommandResult>>
{
    protected virtual IEnumerable<string> TestableFrameworks
    {
        get
        {
            yield return GetTargetFramework();
        }
    }

    internal static string GetTargetFramework()
    {
        var framework = Environment.GetEnvironmentVariable("NET_VERSION");
        return string.IsNullOrWhiteSpace(framework) ? "net10.0" : framework;
    }

    protected override void Configure(ModuleConfigurationBuilder module)
    {
        module.WithTimeout(TimeSpan.FromMinutes(30));
    }

    protected abstract string ProjectFileName { get; }

    protected sealed override async Task<IReadOnlyList<CommandResult>> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();

        foreach (var framework in TestableFrameworks)
        {
            var project = (await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.FindFile(x => x.Name == ProjectFileName);
            if (project is null)
            {
                throw new InvalidOperationException($"Project {ProjectFileName} not found");
            }

            var testResult = await context.Tools.DotNet.RunAsync(
                new DotNetRunOptions
                {
                    NoBuild = true,
                    Configuration = "Release",
                    Framework = framework,
                    Arguments = [
                        "--",
                            "--hangdump",
                            "--hangdump-timeout", "5m",
                            "--log-level", "Debug"
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
