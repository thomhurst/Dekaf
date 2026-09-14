using System.Threading.Channels;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

public sealed class OutboxNotificationTransportTests
{
    [Test]
    public async Task RemoteCommit_WakesOwner_WithoutEchoingReceivedHints()
    {
        var time = new ManualTimeProvider();
        using var writer = new OutboxNotifier(time, 2);
        using var owner = new OutboxNotifier(time, 2);
        writer.SetOwnedBuckets([0]);
        owner.SetOwnedBuckets([1]);
        var writerTransport = new Transport();
        var ownerTransport = new Transport();
        using var sender = Service(writerTransport, writer, time);
        using var receiver = Service(ownerTransport, owner, time);
        await sender.StartAsync(default);
        await receiver.StartAsync(default);
        try
        {
            var incoming = await ownerTransport.Listener.Task.WaitAsync(TimeSpan.FromSeconds(30));
            writer.NotifyCommitted(1);
            var sent = await writerTransport.Sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(sent).IsEquivalentTo([1]);
            incoming(sent[0]);
            await owner.WaitAsync(TimeSpan.FromHours(1)).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            var hints = new int[2];
            await Assert.That(owner.DrainHints(hints, out var unknown)).IsEqualTo(1);
            await Assert.That(hints[0]).IsEqualTo(1);
            await Assert.That(unknown).IsFalse();
        }
        finally
        {
            await sender.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(30));
            await receiver.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(30));
        }
        await Assert.That(ownerTransport.Sent.Reader.TryRead(out _)).IsFalse();
        await Assert.That(writerTransport.Stopped).IsTrue();
        await Assert.That(ownerTransport.Stopped).IsTrue();
    }

    [Test]
    public async Task BlockedTransport_CoalescesCommitsWithoutBlockingCaller_AndCancelsOnShutdown()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time, 2);
        var transport = new Transport { BlockSend = true };
        using var service = Service(transport, notifier, time);
        await service.StartAsync(default);
        try
        {
            notifier.NotifyCommitted(0);
            await transport.Sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            for (var index = 0; index < 10000; index++)
                notifier.NotifyCommitted(index % 2);
            await Assert.That(transport.SendCalls).IsEqualTo(1);
        }
        finally
        {
            await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(30));
        }
        await Assert.That(transport.SendCancelled).IsTrue();
    }

    [Test]
    public async Task FailedTransport_BackoffDoesNotBlockLocalHints()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time, 2);
        notifier.SetOwnedBuckets([0]);
        var transport = new Transport { FailSend = true };
        using var service = Service(transport, notifier, time);
        await service.StartAsync(default);
        try
        {
            notifier.NotifyCommitted(0);
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            notifier.NotifyCommitted(0);
            await notifier.WaitAsync(TimeSpan.FromSeconds(10)).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(transport.SendCalls).IsEqualTo(1);
            transport.FailSend = false;
            time.Advance(TimeSpan.FromSeconds(1));
            await transport.Sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await transport.Sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(transport.SendCalls).IsEqualTo(2);
        }
        finally
        {
            await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task FailedSubscription_RetriesWithBackoff_WhilePollingRemainsAvailable()
    {
        var time = new ManualTimeProvider();
        using var notifier = new OutboxNotifier(time, 2);
        var transport = new Transport { FailListen = true };
        using var service = Service(transport, notifier, time);
        await service.StartAsync(default);
        try
        {
            await time.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            await Assert.That(transport.ListenCalls).IsEqualTo(1);
            var poll = notifier.WaitAsync(TimeSpan.FromMilliseconds(100));
            time.Advance(TimeSpan.FromMilliseconds(100));
            await poll.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(transport.ListenCalls).IsEqualTo(1);
            transport.FailListen = false;
            time.Advance(TimeSpan.FromMilliseconds(900));
            await transport.Listener.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(transport.ListenCalls).IsEqualTo(2);
        }
        finally
        {
            await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task RemoteBuffer_UnknownHintsCoalesceAndBoundStorage()
    {
        var buffer = new OutboxRemoteNotifications(130);
        for (var index = 0; index < 10000; index++)
            buffer.Notify(index % 130);
        var buckets = new int[130];
        await Assert.That(await buffer.ReadAsync(buckets, default)).IsEqualTo(130);
        buffer.Notify(-1);
        buffer.Notify(65);
        await Assert.That(await buffer.ReadAsync(buckets, default)).IsEqualTo(1);
        await Assert.That(buckets[0]).IsEqualTo(-1);
    }

    private static OutboxNotificationService Service(Transport transport, OutboxNotifier notifier, TimeProvider time) =>
        new(transport, notifier, new OutboxRelayOptions { BucketCount = 2 }, time, NullLogger<OutboxNotificationService>.Instance);

    private sealed class Transport : IOutboxNotificationTransport
    {
        public TaskCompletionSource<Action<int>> Listener { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<int[]> Sent { get; } = Channel.CreateUnbounded<int[]>();
        public bool BlockSend;
        public bool FailSend;
        public bool FailListen;
        public bool Stopped;
        public bool SendCancelled;
        public int SendCalls;
        public int ListenCalls;

        public async ValueTask PublishAsync(ReadOnlyMemory<int> buckets, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SendCalls);
            Sent.Writer.TryWrite(buckets.ToArray());
            if (FailSend)
                throw new InvalidOperationException("transport unavailable");
            if (!BlockSend)
                return;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { SendCancelled = cancellationToken.IsCancellationRequested; }
        }

        public async Task ListenAsync(Action<int> notifyCommitted, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ListenCalls);
            if (FailListen)
                throw new InvalidOperationException("subscription unavailable");
            Listener.TrySetResult(notifyCommitted);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { Stopped = true; }
        }
    }
}
