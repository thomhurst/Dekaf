using System.Collections.Concurrent;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class StallDetectorTests
{
    private static readonly TimeSpan CaptureAfter = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExitAfter = TimeSpan.FromMinutes(5);

    [Test]
    public async Task Observe_BeforeCaptureThreshold_DoesNothing()
    {
        var detector = new StallDetector(CaptureAfter, ExitAfter);
        detector.Reset(messageCount: 42, TimeSpan.Zero);

        var action = detector.Observe(messageCount: 42, TimeSpan.FromSeconds(29));

        await Assert.That(action).IsEqualTo(StallAction.None);
    }

    [Test]
    public async Task Observe_Stalled_CapturesOnceUntilProgressResumes()
    {
        var detector = new StallDetector(CaptureAfter, ExitAfter);
        detector.Reset(messageCount: 42, TimeSpan.Zero);

        var first = detector.Observe(messageCount: 42, CaptureAfter);
        var repeated = detector.Observe(messageCount: 42, TimeSpan.FromMinutes(1));
        var resumed = detector.Observe(messageCount: 43, TimeSpan.FromMinutes(2));
        var secondStall = detector.Observe(messageCount: 43, TimeSpan.FromMinutes(2.5));

        await Assert.That(first).IsEqualTo(StallAction.Capture);
        await Assert.That(repeated).IsEqualTo(StallAction.None);
        await Assert.That(resumed).IsEqualTo(StallAction.None);
        await Assert.That(secondStall).IsEqualTo(StallAction.Capture);
    }

    [Test]
    public async Task Observe_AtExitThreshold_CapturesAndExits()
    {
        var detector = new StallDetector(CaptureAfter, ExitAfter);
        detector.Reset(messageCount: 42, TimeSpan.Zero);
        _ = detector.Observe(messageCount: 42, CaptureAfter);

        var action = detector.Observe(messageCount: 42, ExitAfter);

        await Assert.That(action).IsEqualTo(StallAction.CaptureAndExit);
    }

    [Test]
    public async Task Observe_FirstObservationAfterExitThreshold_CapturesBeforeExit()
    {
        var detector = new StallDetector(CaptureAfter, ExitAfter);
        detector.Reset(messageCount: 42, TimeSpan.Zero);

        var capture = detector.Observe(messageCount: 42, ExitAfter);
        var exit = detector.Observe(messageCount: 42, ExitAfter);

        await Assert.That(capture).IsEqualTo(StallAction.Capture);
        await Assert.That(exit).IsEqualTo(StallAction.CaptureAndExit);
    }

    [Test]
    public async Task Watchdog_Stall_CapturesThreadStacksBeforeProducerDiagnostics()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var throughput = new ThroughputTracker();
            var captureOrder = new ConcurrentQueue<string>();
            var producerCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stacksCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var unexpectedExit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(40),
                exitAfter: TimeSpan.FromSeconds(10),
                pollInterval: TimeSpan.FromMilliseconds(5),
                exitProcess: code => unexpectedExit.TrySetResult(code),
                captureManagedStackReport: () =>
                {
                    captureOrder.Enqueue("stacks");
                    stacksCaptured.TrySetResult();
                    return "test managed stack";
                });
            using var registration = watchdog.Track(
                throughput,
                "Dekaf",
                "producer",
                () =>
                {
                    captureOrder.Enqueue("producer");
                    producerCaptured.TrySetResult();
                    return null;
                });

            await Task.WhenAll(producerCaptured.Task, stacksCaptured.Task)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var diagnosticsDirectory = Path.Combine(outputDirectory, ProgressWatchdog.ArtifactsDirectoryName);
            await Assert.That(() => Directory.GetFiles(diagnosticsDirectory, "*-stacks.txt").Length)
                .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
            await Assert.That(() => Directory.GetFiles(diagnosticsDirectory, "*-producer.json").Length)
                .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));

            var order = captureOrder.ToArray();
            await Assert.That(order.Length).IsEqualTo(2);
            await Assert.That(order[0]).IsEqualTo("stacks");
            await Assert.That(order[1]).IsEqualTo("producer");
            await Assert.That(File.ReadAllText(Directory.GetFiles(diagnosticsDirectory, "*-stacks.txt").Single()))
                .Contains("test managed stack");
            await Assert.That(File.ReadAllText(Directory.GetFiles(diagnosticsDirectory, "*-producer.json").Single()))
                .Contains("producerDeliveryDiagnostics");
            await Assert.That(unexpectedExit.Task.IsCompleted).IsFalse();
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Watchdog_FatalStall_CapturesStacksBeforeNonZeroExit()
    {
        var outputDirectory = CreateOutputDirectory();
        try
        {
            var exitCode = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new ProgressWatchdog(
                outputDirectory,
                captureAfter: TimeSpan.FromMilliseconds(30),
                exitAfter: TimeSpan.FromMilliseconds(80),
                pollInterval: TimeSpan.FromMilliseconds(5),
                exitProcess: code => exitCode.TrySetResult(code),
                captureManagedStackReport: () => "test managed stack");
            using var registration = watchdog.Track(new ThroughputTracker(), "Dekaf", "consumer");

            var actualExitCode = await exitCode.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(actualExitCode).IsEqualTo(1);
            var diagnosticsDirectory = Path.Combine(outputDirectory, ProgressWatchdog.ArtifactsDirectoryName);
            await Assert.That(() => Directory.GetFiles(diagnosticsDirectory, "*-fatal-stacks.txt").Length)
                .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dekaf-watchdog-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
