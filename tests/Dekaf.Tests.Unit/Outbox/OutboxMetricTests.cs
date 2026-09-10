using System.Diagnostics.Metrics;
using Dekaf.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Outbox;

[NotInParallel("MeterListener")]
public partial class OutboxMetricTests
{
    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm")]
    public async Task Options_RejectInvalidMetricsName(string name)
    {
        await Assert.That(() => new OutboxRelayOptions { MetricsName = name }.Validate())
            .Throws<ArgumentException>();
    }

    [Test]
    [Arguments(0, 5)]
    [Arguments(-1, 5)]
    [Arguments(30, 0)]
    [Arguments(30, -1)]
    public async Task Options_RejectNonpositiveSamplingTimes(int interval, int timeout)
    {
        await Assert.That(() => new OutboxRelayOptions
        {
            MetricsCollectionInterval = TimeSpan.FromSeconds(interval),
            MetricsCollectionTimeout = TimeSpan.FromSeconds(timeout)
        }.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Options_RejectSamplingTimesBeyondTimerLimit(bool interval)
    {
        var tooLong = TimeSpan.FromMilliseconds(uint.MaxValue);
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            new OutboxRelayOptions
            {
                MetricsCollectionInterval = interval ? tooLong : TimeSpan.FromSeconds(30),
                MetricsCollectionTimeout = interval ? TimeSpan.FromSeconds(5) : tooLong
            }.Validate();
            return Task.CompletedTask;
        });
        await Assert.That(exception!.ParamName).IsEqualTo(interval
            ? nameof(OutboxRelayOptions.MetricsCollectionInterval)
            : nameof(OutboxRelayOptions.MetricsCollectionTimeout));
    }

    [Test]
    public async Task Options_MaximumSamplingTimesCanCreateRuntimeTimers()
    {
        var maximum = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);
        var options = new OutboxRelayOptions
        {
            MetricsCollectionInterval = maximum, MetricsCollectionTimeout = maximum
        };
        options.Validate();
        using var timeout = new CancellationTokenSource(options.MetricsCollectionTimeout, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var delay = Task.Delay(options.MetricsCollectionInterval, TimeProvider.System, cancellation.Token);
        cancellation.Cancel();
        await Assert.That(async () => await delay).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Relay_ExposesOperationalInstruments()
    {
        var names = new HashSet<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == "Dekaf.Outbox")
                names.Add(instrument.Name);
        };
        listener.Start();
        using var relay = new OutboxRelayService(new EmptyStore(), new EmptyPublisher(),
            new OutboxRelayOptions { MaxPublishDuration = TimeSpan.FromSeconds(1) },
            NullLogger<OutboxRelayService>.Instance);

        await Assert.That(names).Contains("dekaf.outbox.owned_buckets");
        await Assert.That(names).Contains("dekaf.outbox.publish.acknowledged");
        await Assert.That(names).Contains("dekaf.outbox.publish.failures");
        await Assert.That(names).Contains("dekaf.outbox.lease.expirations");
        await Assert.That(names).Contains("dekaf.outbox.publish.duration");
        await Assert.That(names).Contains("dekaf.outbox.cycle.duration");
        await Assert.That(names).Contains("dekaf.outbox.pending.messages");
        await Assert.That(names).Contains("dekaf.outbox.pending.oldest_age");
        await Assert.That(names).Contains("dekaf.outbox.pending.available");
    }

    private sealed class EmptyStore : IOutboxStore
    {
        public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(OutboxLeaseRequest request,
            CancellationToken cancellationToken = default) => new(Array.Empty<int>());
        public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(IReadOnlyList<int> buckets,
            CancellationToken cancellationToken = default) => new(Array.Empty<int>());
        public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(int bucket, int maxCount,
            CancellationToken cancellationToken = default) => new(Array.Empty<OutboxMessage>());
        public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> messages,
            CancellationToken cancellationToken = default) => default;
    }

    private sealed class EmptyPublisher : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default) =>
            new(new OutboxPublishResult(messages.Count, null));
        public ValueTask DisposeAsync() => default;
    }
}
