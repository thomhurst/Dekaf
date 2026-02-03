using ModularPipelines.Attributes;
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
                        Framework = framework,
                        Arguments = ["--", "--timeout", "10m", "--log-level", "Trace", "--output", "Detailed"]
                    },
                    new CommandExecutionOptions
                    {
                        WorkingDirectory = project.Folder!.Path,
                        EnvironmentVariables = new Dictionary<string, string?>
                        {
                            ["NET_VERSION"] = framework,
                        }
                    },
                    linkedCts.Token);

                results.Add(testResult);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Test execution for {ProjectFileName} ({framework}) exceeded 15 minute pipeline timeout");
            }
        }

        return results;
    }
}
