using Dekaf.Outbox;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxNotifierTests
{
    [Test]
    public async Task DefaultPollInterval_IsOneSecond_AndCanBeOverridden()
    {
        await Assert.That(new OutboxRelayOptions().PollInterval).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(new OutboxRelayOptions { PollInterval = TimeSpan.FromMilliseconds(20) }.PollInterval)
            .IsEqualTo(TimeSpan.FromMilliseconds(20));
    }

    [Test]
    public async Task NotificationsBeforeWait_CoalesceWithoutLosingPendingWork()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time);
        for (var index = 0; index < 100; index++)
            notifier.NotifyCommitted();
        var first = notifier.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(first.IsCompletedSuccessfully).IsTrue();
        await first;

        var second = notifier.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(second.IsCompleted).IsFalse();
        time.Advance(TimeSpan.FromSeconds(1));
        await second.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task CommitWhileWaiting_InterruptsWait_AndDisarmsPollingTimer()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time);
        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(1));
        await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        notifier.NotifyCommitted();
        await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(1));

        using var cancellation = new CancellationTokenSource();
        var next = notifier.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token);
        await Assert.That(next.IsCompleted).IsFalse();
        cancellation.Cancel();
        await Assert.That(async () => await next).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CommitRacingWithTimerRegistration_IsNotLost()
    {
        var time = new NotifyingTimeProvider();
        using var notifier = new OutboxNotifier(time);
        time.OnSchedule = notifier.NotifyCommitted;
        await notifier.WaitAsync(TimeSpan.FromSeconds(1)).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(time.Schedules).IsEqualTo(1);
    }

    [Test]
    public async Task Cancellation_DoesNotConsumePendingNotification()
    {
        using var notifier = new OutboxNotifier(new ManualTimeProvider());
        notifier.NotifyCommitted();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await notifier.WaitAsync(TimeSpan.FromSeconds(1), cancellation.Token))
            .Throws<OperationCanceledException>();
        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(waiting.IsCompletedSuccessfully).IsTrue();
        await waiting;
    }

    private sealed class NotifyingTimeProvider : TimeProvider
    {
        public Action? OnSchedule { get; set; }
        public int Schedules { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new NotifyingTimer(this);

        private sealed class NotifyingTimer(NotifyingTimeProvider owner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    owner.Schedules++;
                    owner.OnSchedule?.Invoke();
                }
                return true;
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
