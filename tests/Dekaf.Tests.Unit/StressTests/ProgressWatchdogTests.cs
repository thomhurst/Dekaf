using Dekaf.Producer;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ProgressWatchdogTests
{
    [Test]
    public async Task Track_Stall_CapturesStacksAndProducerDiagnosticsThenExits()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(40),
                exitAfter: TimeSpan.FromMilliseconds(500),
                pollInterval: TimeSpan.FromMilliseconds(10),
                exitProcess: code => exited.TrySetResult(code),
                captureManagedStackReport: () => "fake managed stack");
            using var registration = watchdog.Track(
                throughput,
                "Dekaf",
                "producer",
                () => new ProducerDeliveryDiagnosticsSnapshot
                {
                    DiagnosticsEnabled = true,
                    CapturedAtUtc = DateTimeOffset.UtcNow
                });

            var exitCode = await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(exitCode).IsEqualTo(1);
            var diagnosticsDirectory = GetDiagnosticsDirectory(outputDirectory);
            var stackArtifacts = Directory.GetFiles(diagnosticsDirectory, "*-stacks.txt");
            var producerArtifacts = Directory.GetFiles(diagnosticsDirectory, "*-producer.json");
            await Assert.That(stackArtifacts.Length).IsEqualTo(2);
            await Assert.That(producerArtifacts.Length).IsEqualTo(2);
            await Assert.That(Directory.GetFiles(outputDirectory, "*.json")).IsEmpty();
            await Assert.That(await File.ReadAllTextAsync(stackArtifacts[0])).Contains("fake managed stack");
            await Assert.That(await File.ReadAllTextAsync(producerArtifacts[0])).Contains("\"diagnosticsEnabled\": true");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Track_FatalStall_ExitsWhenStackCaptureFails()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(20),
                exitAfter: TimeSpan.FromMilliseconds(60),
                pollInterval: TimeSpan.FromMilliseconds(10),
                exitProcess: code => exited.TrySetResult(code),
                captureManagedStackReport: () => throw new InvalidOperationException("capture unavailable"));
            using var registration = watchdog.Track(throughput, "Dekaf", "consumer");

            var exitCode = await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(exitCode).IsEqualTo(1);
            var fatalArtifact = Directory.GetFiles(
                GetDiagnosticsDirectory(outputDirectory),
                "*-fatal-stacks.txt").Single();
            await Assert.That(await File.ReadAllTextAsync(fatalArtifact)).Contains("capture unavailable");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Track_FatalStall_ExitsWhenProducerDiagnosticsBlocks()
    {
        var outputDirectory = CreateOutputDirectory();
        using var releaseCapture = new ManualResetEventSlim(initialState: false);
        using var capturesCompleted = new CountdownEvent(initialCount: 2);
        try
        {
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var throughput = new ThroughputTracker();
            throughput.Start();

            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(20),
                exitAfter: TimeSpan.FromMilliseconds(100),
                pollInterval: TimeSpan.FromMilliseconds(10),
                exitProcess: code => exited.TrySetResult(code),
                captureManagedStackReport: () => "fake managed stack",
                producerDiagnosticsTimeout: TimeSpan.FromMilliseconds(30));
            using var registration = watchdog.Track(
                throughput,
                "Dekaf",
                "producer",
                () =>
                {
                    releaseCapture.Wait();
                    capturesCompleted.Signal();
                    return null;
                });

            var exitCode = await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(exitCode).IsEqualTo(1);
            var diagnosticsDirectory = GetDiagnosticsDirectory(outputDirectory);
            var fatalStackArtifact = Directory.GetFiles(diagnosticsDirectory, "*-fatal-stacks.txt").Single();
            var fatalProducerArtifact = Directory.GetFiles(diagnosticsDirectory, "*-fatal-producer.json").Single();
            await Assert.That(await File.ReadAllTextAsync(fatalStackArtifact)).Contains("fake managed stack");
            await Assert.That(await File.ReadAllTextAsync(fatalProducerArtifact)).Contains("timed out");
        }
        finally
        {
            releaseCapture.Set();
            capturesCompleted.Wait(TimeSpan.FromSeconds(5));
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dekaf-watchdog-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetDiagnosticsDirectory(string outputDirectory) =>
        Path.Combine(outputDirectory, ProgressWatchdog.ArtifactsDirectoryName);
}
