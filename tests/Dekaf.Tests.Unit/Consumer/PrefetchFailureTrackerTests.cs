using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PrefetchFailureTrackerTests
{
    private static readonly PrefetchFailureKey Key = new(BrokerId: 1, ConnectionIndex: 0);
    private static readonly PrefetchPosition[] Positions =
    [
        new(new TopicPartition("topic", 0), Offset: 42),
        new(new TopicPartition("topic", 1), Offset: 84)
    ];

    [Test]
    public async Task Observe_DeterministicFailureTripsAtThreshold()
    {
        var tracker = new PrefetchFailureTracker(terminalThreshold: 3, initialDelayMs: 100, maxDelayMs: 5_000);

        var first = tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        var second = tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        var third = tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);

        await Assert.That(first).IsEqualTo(new PrefetchFailureDecision(100, IsTerminal: false, Count: 1));
        await Assert.That(second).IsEqualTo(new PrefetchFailureDecision(200, IsTerminal: false, Count: 2));
        await Assert.That(third).IsEqualTo(new PrefetchFailureDecision(400, IsTerminal: true, Count: 3));
    }

    [Test]
    public async Task Observe_ChangedPositionRestartsFailureCount()
    {
        var tracker = new PrefetchFailureTracker(terminalThreshold: 3, initialDelayMs: 100, maxDelayMs: 5_000);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        PrefetchPosition[] advanced =
        [
            new(new TopicPartition("topic", 0), Offset: 43),
            new(new TopicPartition("topic", 1), Offset: 84)
        ];

        var decision = tracker.Observe(Key, typeof(InvalidDataException), advanced, deterministic: true);

        await Assert.That(decision).IsEqualTo(new PrefetchFailureDecision(100, IsTerminal: false, Count: 1));
    }

    [Test]
    public async Task Observe_ChangedExceptionTypeRestartsFailureCount()
    {
        var tracker = new PrefetchFailureTracker(terminalThreshold: 3, initialDelayMs: 100, maxDelayMs: 5_000);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);

        var decision = tracker.Observe(Key, typeof(FormatException), Positions, deterministic: true);

        await Assert.That(decision).IsEqualTo(new PrefetchFailureDecision(100, IsTerminal: false, Count: 1));
    }

    [Test]
    public async Task Observe_TransientFailureBacksOffWithoutBecomingTerminal()
    {
        var tracker = new PrefetchFailureTracker(terminalThreshold: 3, initialDelayMs: 100, maxDelayMs: 500);
        PrefetchFailureDecision decision = default;

        for (var i = 0; i < 10; i++)
        {
            decision = tracker.Observe(Key, typeof(IOException), Positions, deterministic: false);
        }

        await Assert.That(decision.DelayMs).IsEqualTo(500);
        await Assert.That(decision.IsTerminal).IsFalse();
        await Assert.That(decision.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Reset_RestartsFailureCount()
    {
        var tracker = new PrefetchFailureTracker(terminalThreshold: 3, initialDelayMs: 100, maxDelayMs: 5_000);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);
        tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);

        tracker.Reset(Key);
        var decision = tracker.Observe(Key, typeof(InvalidDataException), Positions, deterministic: true);

        await Assert.That(decision.Count).IsEqualTo(1);
    }
}
