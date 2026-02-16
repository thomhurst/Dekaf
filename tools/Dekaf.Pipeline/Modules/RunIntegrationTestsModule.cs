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
public abstract class RunIntegrationTestsModule : Module<IReadOnlyList<CommandResult>>
{
    /// <summary>
    /// The TUnit test category to filter by (matches [Category("X")] on test classes).
    /// </summary>
    protected new abstract string Category { get; }

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

        // When SKIP_INTEGRATION_TESTS is set, skip all integration tests (used by build-and-unit-test CI job)
        if (string.Equals(Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogInformation("Skipping integration tests (SKIP_INTEGRATION_TESTS=true)");
            return null;
        }

        // When INTEGRATION_TEST_CATEGORY is set (CI matrix), only run the matching category
        var targetCategory = Environment.GetEnvironmentVariable("INTEGRATION_TEST_CATEGORY");
        if (!string.IsNullOrEmpty(targetCategory) &&
            !string.Equals(targetCategory, Category, StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogInformation(
                "Skipping {Category} integration tests (INTEGRATION_TEST_CATEGORY={TargetCategory})",
                Category, targetCategory);
            return null;
        }

        var results = new List<CommandResult>();

        var project = context.Git().RootDirectory.FindFile(x => x.Name == "Dekaf.Tests.Integration.csproj");

        if (project is null)
        {
            throw new InvalidOperationException("Dekaf.Tests.Integration.csproj not found");
        }

        var arguments = new List<string>
        {
            "--",
            "--hangdump",
            "--hangdump-timeout", "15m", // Creates diagnostic dump then kills process if it hangs
            "--log-level", "Trace",
            "--output", "Detailed",
            "--treenode-filter", $"/**[Category={Category}]"
        };

        context.Logger.LogInformation("Running integration tests for category: {Category}", Category);

        // Process-level timeout as safety fallback (matches TestBaseModule pattern)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // ThrowOnNonZeroExitCode = false: TUnit 1.14+ (PR #4782) moved test cleanup
            // outside the timeout scope. When producer/consumer disposal is slow, the process
            // hangs after all tests pass until --hangdump-timeout kills it (exit code 7).
            // We handle this by checking for actual test failures in the output.
            var testResult = await context.DotNet().Run(
                new DotNetRunOptions
                {
                    NoBuild = true,
                    Configuration = "Release",
                    Framework = "net10.0",
                    Arguments = arguments
                },
                new CommandExecutionOptions
                {
                    WorkingDirectory = project.Folder!.Path,
                    ThrowOnNonZeroExitCode = false,
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["NET_VERSION"] = "net10.0",
                        ["DOTNET_GCConserveMemory"] = "9", // Aggressive GC to reduce memory pressure on CI
                    }
                },
                linkedCts.Token);

            if (testResult.ExitCode != 0)
            {
                // Microsoft.Testing.Platform exit codes:
                // 0 = success, 2 = test failures, 3 = session timeout, 7 = process killed (hangdump)
                var output = testResult.StandardOutput + "\n" + testResult.StandardError;
                var isCleanupHangExitCode = testResult.ExitCode is 3 or 7;
                var hasTestFailures = output.Contains("failed:") && !output.Contains("failed: 0");

                if (isCleanupHangExitCode && !hasTestFailures)
                {
                    context.Logger.LogWarning(
                        "Integration tests for '{Category}' exited with code {ExitCode} " +
                        "(process didn't exit cleanly after test completion)",
                        Category, testResult.ExitCode);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Integration tests for category '{Category}' failed with exit code {testResult.ExitCode}");
                }
            }

            results.Add(testResult);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Integration tests for category '{Category}' exceeded 20 minute process timeout");
        }

        return results;
    }
}
