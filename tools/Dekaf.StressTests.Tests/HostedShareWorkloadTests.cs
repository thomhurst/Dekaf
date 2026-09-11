using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Tests;

public class HostedShareWorkloadTests
{
    [Test]
    [Arguments(0L)]
    [Arguments(4500L)]
    public async Task WorkerWithoutMeasuredProgress_FailsEvenAfterWarmup(long warmupProcessed)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            HostedShareStressTest.ValidateWorkerProgress(warmupProcessed, warmupProcessed);
            return Task.CompletedTask;
        });
    }

    [Test]
    public void WorkerWithMeasuredProgress_Passes()
    {
        HostedShareStressTest.ValidateWorkerProgress(4500, 4501);
    }

    [Test]
    public async Task MissingRecord_BlocksSlotReuseEvenWhenLaterRecordsComplete()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        var tracker = new ThroughputTracker();
        state.BeginCycle(tracker);
        for (var sequence = 0; sequence < HostedShareWorkloadState.WindowSize; sequence++)
        {
            state.RecordProduced();
            if (sequence != 0) state.Process(Payload(sequence));
        }
        await Assert.That(state.CanProduce).IsFalse();
        state.Process(Payload(0));
        await Assert.That(state.CanProduce).IsTrue();
        await Assert.That(state.Completed).IsEqualTo((long)HostedShareWorkloadState.WindowSize);
        await Assert.That(tracker.MessageCount).IsEqualTo(state.Completed);
    }

    [Test]
    public async Task ConcurrentRedelivery_CountsOnlyOneCompletion()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        var tracker = new ThroughputTracker();
        state.BeginCycle(tracker);
        state.RecordProduced();
        var payload = Payload(0);
        await Task.WhenAll(Task.Run(() => state.Process(payload)), Task.Run(() => state.Process(payload)));
        await Assert.That(state.Completed).IsEqualTo(1L);
        await Assert.That(state.Duplicates).IsEqualTo(1L);
        await Assert.That(tracker.MessageCount).IsEqualTo(1L);
        await Assert.That(state.Latency.GetSnapshot().Count).IsEqualTo(1L);
    }

    [Test]
    public async Task OldDuplicate_AfterSlotReuse_DoesNotCompleteNewWork()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        state.BeginCycle(new ThroughputTracker());
        state.Process(Payload(0));
        state.Process(Payload(HostedShareWorkloadState.WindowSize));
        await Assert.That(state.Process(Payload(0))).IsFalse();
        await Assert.That(state.Completed).IsEqualTo(2L);
    }

    [Test]
    public async Task SkippedGeneration_FailsInsteadOfHidingMissingRecord()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        state.BeginCycle(new ThroughputTracker());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => state.Process(Payload(HostedShareWorkloadState.WindowSize))));
    }

    [Test]
    public async Task UndrainedCycle_CannotResetMeasuredCounters()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        state.BeginCycle(new ThroughputTracker());
        state.RecordProduced();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => state.BeginCycle(new ThroughputTracker())));
    }

    [Test]
    public async Task BackgroundFailure_RemainsVisibleAcrossCycleBoundary()
    {
        var state = new HostedShareWorkloadState("topic", 16);
        state.RecordFailure(new IOException("acknowledgement failed"));
        await Assert.ThrowsAsync<IOException>(() => Task.Run(() => state.BeginCycle(new ThroughputTracker())));
    }

    [Test]
    public async Task Result_RetainsProducerDiagnosticsWithProcessingLatency()
    {
        using var watchdog = new ProgressWatchdog(Path.GetTempPath());
        var options = new StressTestOptions
        {
            BootstrapServers = "localhost:1", Topic = "topic", DurationMinutes = 1,
            MessageSizeBytes = 16, EnableProducerDeliveryDiagnostics = true,
            ProgressWatchdog = watchdog
        };
        var builder = Kafka.CreateProducer<string, byte[]>().WithBootstrapServers(options.BootstrapServers);
        StressTestHelpers.ConfigureProducerDeliveryDiagnostics(builder, options);
        await using var producer = builder.Build();
        var throughput = new ThroughputTracker();
        var latency = new LatencyTracker();
        using var gc = new GcStats();
        throughput.Start();
        throughput.RecordMessage(16);
        latency.RecordTicks(100);
        throughput.Stop();
        gc.Capture();
        var run = new ProducerWorkloadResult(1, throughput.GetSnapshot(), gc.ToSnapshot());

        var result = new HostedShareStressTest().CreateResult(options, DateTime.UtcNow, run, throughput, latency, producer);
        using var json = JsonDocument.Parse(result.ToJson());

        await Assert.That(json.RootElement.GetProperty("producerDeliveryDiagnostics").ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(json.RootElement.GetProperty("producerDeliveryDiagnostics").GetProperty("diagnosticsEnabled").GetBoolean()).IsTrue();
        await Assert.That(json.RootElement.GetProperty("latency").GetProperty("count").GetInt64()).IsEqualTo(1L);
        await Assert.That(json.RootElement.GetProperty("idempotent").GetBoolean()).IsTrue();
        await Assert.That(json.RootElement.GetProperty("consumedMessages").GetInt64()).IsEqualTo(1L);
        await Assert.That(json.RootElement.TryGetProperty("deliveredMessages", out _)).IsFalse();
    }

    private static byte[] Payload(long sequence)
    {
        var payload = new byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(payload, sequence);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), Stopwatch.GetTimestamp());
        return payload;
    }
}
