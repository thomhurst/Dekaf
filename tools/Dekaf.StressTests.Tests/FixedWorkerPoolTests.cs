using System.Diagnostics;
using System.Text.Json;

namespace Dekaf.StressTests.Tests;

public class FixedWorkerPoolTests
{
    [Test]
    public async Task ConfiguredWorkers_SurviveIdleRetirementInterval()
    {
        var result = await RunCheckAsync("2", "0x2", "0x2", "-1");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Output).Contains("retained 2 workers after 25 seconds idle");
        using var snapshot = JsonDocument.Parse(result.Snapshot!);
        await Assert.That(snapshot.RootElement.GetProperty("minimumWorkers").GetInt32()).IsEqualTo(2);
        await Assert.That(snapshot.RootElement.GetProperty("maximumWorkers").GetInt32()).IsEqualTo(2);
        await Assert.That(snapshot.RootElement.GetProperty("liveWorkers").GetInt32()).IsEqualTo(2);
    }

    [Test]
    [Arguments(null, "0x2", "0x2", "-1", "requires a positive")]
    [Arguments("0", "0x2", "0x2", "-1", "requires a positive")]
    [Arguments("00", "0x2", "0x2", "-1", "requires a positive")]
    [Arguments("-1", "0x2", "0x2", "-1", "integer from 0 to 1024")]
    [Arguments("1025", "0x2", "0x2", "-1", "integer from 0 to 1024")]
    [Arguments("invalid", "0x2", "0x2", "-1", "integer from 0 to 1024")]
    [Arguments("2", "0x1", "0x2", "-1", "Fixed worker limits differ")]
    [Arguments("2", "0x2", "0x2", "20000", "prevent idle retirement")]
    public async Task InvalidConfiguration_FailsBeforeWorkload(
        string? requested, string minimum, string maximum, string idleTimeout, string expected)
    {
        var result = await RunCheckAsync(requested, minimum, maximum, idleTimeout);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.Output).Contains(expected);
        await Assert.That(result.Snapshot).IsNull();
    }

    private static async Task<CheckResult> RunCheckAsync(
        string? requested, string minimum, string maximum, string idleTimeout)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"Dekaf-fixed-workers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var testAssembly = typeof(FixedWorkerPoolTests).Assembly.Location;
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Run the maintained entry point in its own process. Never change the
            // shared TUnit process's worker limits or idle-retirement behavior.
            foreach (var argument in new[]
            {
                "exec", "--runtimeconfig", Path.ChangeExtension(testAssembly, "runtimeconfig.json"),
                "--depsfile", Path.ChangeExtension(testAssembly, "deps.json"),
                typeof(Program).Assembly.Location, "check-worker-pool", "--output", directory
            })
                start.ArgumentList.Add(argument);
            start.Environment.Remove("DEKAF_STRESS_FIXED_WORKER_THREADS");
            if (requested is not null)
                start.Environment["DEKAF_STRESS_FIXED_WORKER_THREADS"] = requested;
            start.Environment["DOTNET_ThreadPool_ForceMinWorkerThreads"] = minimum;
            start.Environment["DOTNET_ThreadPool_ForceMaxWorkerThreads"] = maximum;
            start.Environment["DOTNET_ThreadPool_ThreadTimeoutMs"] = idleTimeout;

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75));
            try
            {
                await process.WaitForExitAsync(deadline.Token);
            }
            catch
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(output, errors);
                throw;
            }
            var snapshots = Directory.GetFiles(directory, "fixed-worker-pool-*.json");
            return new CheckResult(process.ExitCode, await output + await errors,
                snapshots.Length == 1 ? await File.ReadAllTextAsync(snapshots[0]) : null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private readonly record struct CheckResult(int ExitCode, string Output, string? Snapshot);
}
