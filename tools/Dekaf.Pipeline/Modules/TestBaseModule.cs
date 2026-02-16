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

            // ThrowOnNonZeroExitCode = false: TUnit 1.14+ can hang after all tests
            // pass due to cleanup running outside the timeout scope (PR #4782).
            // The --hangdump-timeout kills the process (exit code 7) which we accept
            // when no test failures are detected.
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
                    ThrowOnNonZeroExitCode = false,
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["NET_VERSION"] = framework,
                    }
                },
                cancellationToken);

            if (testResult.ExitCode != 0)
            {
                var output = testResult.StandardOutput + "\n" + testResult.StandardError;
                var isCleanupHangExitCode = testResult.ExitCode is 3 or 7;
                var hasTestFailures = output.Contains("failed:") && !output.Contains("failed: 0");

                if (isCleanupHangExitCode && !hasTestFailures)
                {
                    context.Logger.LogWarning(
                        "Tests exited with code {ExitCode} (process didn't exit cleanly after test completion)",
                        testResult.ExitCode);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Tests failed with exit code {testResult.ExitCode}");
                }
            }

            results.Add(testResult);
        }

        return results;
    }
}
