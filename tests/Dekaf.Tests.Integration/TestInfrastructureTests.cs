using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

public sealed partial class TestInfrastructureTests
{
    [Test]
    public async Task CapturingLoggerProvider_CapturesStructuredEntryAndNotifiesObserver()
    {
        CapturedLogEntry? observed = null;
        using var provider = new CapturingLoggerProvider(entry => observed = entry);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = factory.CreateLogger<TestInfrastructureTests>();

        LogCapturedValue(logger, 17);

        var entry = provider.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(42);
        await Assert.That(entry.EventId.Name).IsEqualTo("StructuredEvent");
        await Assert.That(entry.Message).IsEqualTo("Captured value 17");
        await Assert.That(entry.TryGetProperty<int>("Value", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo(17);
        await Assert.That(observed).IsEqualTo(entry);
    }

    [Test]
    public async Task CleanupFailureCollector_ContinuesAndReportsLabeledFailures()
    {
        var collector = new CleanupFailureCollector();
        var secondCleanupRan = false;

        await collector.CaptureTaskAsync(
            "first cleanup",
            () => Task.FromException(new InvalidOperationException("failure")));
        await collector.CaptureTaskAsync(
            "second cleanup",
            () =>
            {
                secondCleanupRan = true;
                return Task.CompletedTask;
            });

        await Assert.That(secondCleanupRan).IsTrue();
        var exception = await Assert.That(collector.ThrowIfAny).Throws<AggregateException>();
        await Assert.That(exception!.InnerExceptions).Count().IsEqualTo(1);
        await Assert.That(exception.InnerExceptions[0].Message).IsEqualTo("first cleanup failed.");
        await Assert.That(exception.InnerExceptions[0].InnerException!.Message).IsEqualTo("failure");
    }

    [LoggerMessage(
        EventId = 42,
        EventName = "StructuredEvent",
        Level = LogLevel.Information,
        Message = "Captured value {Value}")]
    private static partial void LogCapturedValue(ILogger logger, int value);
}
