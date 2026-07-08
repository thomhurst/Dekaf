using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerDeliveryDiagnosticsTests
{
    private static ProducerOptions CreateOptions(bool enableDeliveryDiagnostics = false) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "diagnostics-test",
        BufferMemory = ulong.MaxValue,
        BatchSize = 60,
        LingerMs = 10_000,
        EnableDeliveryDiagnostics = enableDeliveryDiagnostics
    };

    [Test]
    public async Task SnapshotDeliveryDiagnostics_DefaultDisabled_ReturnsDisabledSnapshot()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsFalse();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
        await Assert.That(batches[0].PipelineGeneration).IsEqualTo(0);

        CompleteAndReturn(accumulator, batches);
    }

    [Test]
    public async Task DeliveryDiagnostics_Disabled_DoesNotTrackTrace()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        var batches = await AppendAndDrainBatchesAsync(accumulator);
        var batch = batches[0];

        await Assert.That(batch.PipelineGeneration).IsEqualTo(0);
        await Assert.That(batch.DiagTrace).IsEqualTo("");

        batch.CompleteSend(baseOffset: 42, DateTimeOffset.UtcNow);
        accumulator.OnBatchExitsPipeline(batch);

        await Assert.That(batch.DiagTrace).IsEqualTo("");

        accumulator.ReturnReadyBatch(batch);
        CompleteAndReturn(accumulator, batches, startIndex: 1);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_CapturesLiveInFlightBatch()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();
        var diagnostic = snapshot.Batches.First();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(snapshot.Batches.Count);
        await Assert.That(diagnostic.Topic).IsEqualTo("diagnostics-topic");
        await Assert.That(diagnostic.Partition).IsEqualTo(2);
        await Assert.That(diagnostic.RecordCount).IsGreaterThan(0);
        await Assert.That(diagnostic.DataSize).IsGreaterThan(0);
        await Assert.That(diagnostic.EncodedSize).IsGreaterThan(0);
        await Assert.That(diagnostic.PipelineGeneration).IsEqualTo(diagnostic.CurrentGeneration);
        await Assert.That(diagnostic.ReadyBatchId).IsNotEqualTo(0);
        await Assert.That(diagnostic.RecordBatchId).IsNotNull();
        await Assert.That(diagnostic.ArenaId).IsNotNull();
        await Assert.That(diagnostic.Trace).Contains("E");
        await Assert.That(diagnostic.LastTouchedBy).IsNotEqualTo("");
        await Assert.That(diagnostic.LifecycleState).IsNotEqualTo("");
        await Assert.That(diagnostic.IsStale).IsFalse();

        CompleteAndReturn(accumulator, batches);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_AfterBatchExitsPipeline_IsEmpty()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        CompleteAndReturn(accumulator, batches);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_ThroughProducerDiagnosticsInterface_UsesProducerAccumulator()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithDeliveryDiagnostics()
            .Build();

        var diagnostics = (IProducerDiagnostics)producer;

        var snapshot = diagnostics.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateDeliveryDiagnostic_WhenBatchCannotBePinned_ReturnsStaleDiagnostic()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);
        var batch = batches[0];
        var generation = batch.Generation;
        var topicPartition = batch.TopicPartition;

        batch.CompleteSend(baseOffset: 42, DateTimeOffset.UtcNow);

        var diagnostic = batch.CreateDeliveryDiagnostic(generation, topicPartition);

        await Assert.That(diagnostic.IsStale).IsTrue();
        await Assert.That(diagnostic.LifecycleState).IsEqualTo("stale");
        await Assert.That(diagnostic.Topic).IsEqualTo(topicPartition.Topic);
        await Assert.That(diagnostic.Partition).IsEqualTo(topicPartition.Partition);

        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
        CompleteAndReturn(accumulator, batches, startIndex: 1);
    }

    [Test]
    public async Task DiagTrace_KeepsMostRecentTransitionsWhenCapacityIsExceeded()
    {
        var batch = new ReadyBatch();
        for (var i = 0; i < 39; i++)
            batch.AppendDiag('D');
        batch.AppendDiag('X');

        var trace = batch.DiagTrace;

        await Assert.That(trace.Length).IsEqualTo(32);
        await Assert.That(trace[^1]).IsEqualTo('X');
    }

    private static async Task<List<ReadyBatch>> AppendAndDrainBatchesAsync(RecordAccumulator accumulator)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 3; i++)
        {
            var value = new byte[30];
            value[0] = (byte)i;

            var appended = await accumulator.AppendFromSpansAsync(
                "diagnostics-topic",
                partition: 2,
                timestamp + i,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                value,
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                callback: null,
                CancellationToken.None,
                partitionCount: 3);

            await Assert.That(appended).IsTrue();
        }

        var batches = new List<ReadyBatch>();
        while (accumulator.TryDrainBatch(out var batch))
            batches.Add(batch);

        await Assert.That(batches.Count).IsGreaterThan(0);
        return batches;
    }

    private static void CompleteAndReturn(RecordAccumulator accumulator, List<ReadyBatch> batches, int startIndex = 0)
    {
        for (var i = startIndex; i < batches.Count; i++)
        {
            var batch = batches[i];
            batch.CompleteSend(baseOffset: 42 + i, DateTimeOffset.UtcNow);
            accumulator.OnBatchExitsPipeline(batch);
            accumulator.ReturnReadyBatch(batch);
        }
    }
}
