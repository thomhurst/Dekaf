using System.Buffers;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for adaptive connection scaling features:
/// FIFO waiter queue behavior, buffer pressure tracking, and builder validation.
/// </summary>
public class AdaptiveScalingTests
{
    #region Helpers

    private static RecordAccumulator CreateAccumulator(ulong bufferMemory, int maxBlockMs = 10_000)
    {
        return new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = maxBlockMs
        });
    }

    private static async ValueTask AppendOneRecordAsync(RecordAccumulator accumulator)
    {
        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var valueBytes = ArrayPool<byte>.Shared.Rent(200);
        var pooledValue = new PooledMemory(valueBytes, 200);

        await accumulator.AppendAsync(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pooledKey, pooledValue, null, 0, null, CancellationToken.None);
    }

    /// <summary>
    /// Measures the actual BufferedBytes consumed by a single record append.
    /// Avoids hardcoding record overhead assumptions.
    /// </summary>
    private static async Task<int> MeasureRecordSizeAsync()
    {
        var accumulator = CreateAccumulator(bufferMemory: 10_000_000);
        await AppendOneRecordAsync(accumulator);
        var size = (int)accumulator.BufferedBytes;

        accumulator.ClearCurrentBatch("test-topic", 0);
        accumulator.ReleaseMemory(size);

        await accumulator.DisposeAsync();
        return size;
    }

    #endregion

    #region Pressure Tracking Tests

    [Test]
    public async Task BufferUtilization_ReturnsCorrectRatio()
    {
        var recordSize = await MeasureRecordSizeAsync();
        // Buffer that can hold exactly 2 records
        var accumulator = CreateAccumulator(bufferMemory: (ulong)(recordSize * 2));

        try
        {
            // Empty buffer: utilization should be 0
            await Assert.That(accumulator.BufferUtilization).IsEqualTo(0.0);

            // Append one record: utilization should be ~0.5
            await AppendOneRecordAsync(accumulator);
            var utilization = accumulator.BufferUtilization;
            await Assert.That(utilization).IsGreaterThan(0.0);
            await Assert.That(utilization).IsLessThanOrEqualTo(1.0);

            // The utilization should be approximately 0.5 (one record in a 2-record buffer)
            await Assert.That(utilization).IsGreaterThanOrEqualTo(0.4);
            await Assert.That(utilization).IsLessThanOrEqualTo(0.6);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    #endregion

    #region Scale Step Calculation Tests

    [Test]
    public async Task ComputeScaleTarget_MinimumPressure_AddsOneConnection()
    {
        // pressureDelta = 100 (exactly threshold) → step = 1
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeScaleTarget_ModeratePressure_AddsTwoConnections()
    {
        // pressureDelta = 250 → step = 2
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 250, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(3);
    }

    [Test]
    public async Task ComputeScaleTarget_HighPressure_CapsAtMaxStep()
    {
        // pressureDelta = 10_000 → step capped at 3 (MaxScaleStep)
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 10_000, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(4);
    }

    [Test]
    public async Task ComputeScaleTarget_NearMaxConnections_ClampsToMax()
    {
        // step would be 3, but maxConnections caps at 10
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 500, currentConnections: 9, maxConnections: 10);
        await Assert.That(target).IsEqualTo(10);
    }

    [Test]
    public async Task ComputeScaleTarget_AtMaxConnections_ReturnsMax()
    {
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 500, currentConnections: 10, maxConnections: 10);
        await Assert.That(target).IsEqualTo(10);
    }

    #endregion

    #region Scale-Down Computation Tests

    [Test]
    public async Task ComputeScaleTarget_ScaleUp_FromOneToTwo()
    {
        // Verify scale-up still works correctly alongside scale-down logic
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 5);
        await Assert.That(target).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeScaleTarget_AtMinConnections_DoesNotGoBelow()
    {
        // ComputeScaleTarget is only for scale-up; scale-down uses different logic.
        // Verify it does not produce targets below current.
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 1);
        await Assert.That(target).IsEqualTo(1);
    }

    #endregion

    #region Builder Validation Tests

    [Test]
    public async Task WithAdaptiveConnections_MaxLessThanConnectionsPerBroker_ThrowsOnBuild()
    {
        await Assert.That(() =>
        {
            Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithConnectionsPerBroker(5)
                .WithAdaptiveConnections(maxConnections: 3)
                .Build();
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IdempotentProducer_WithAdaptiveScaling_DoesNotThrow()
    {
        // Idempotent producers should support adaptive scaling (partition affinity
        // preserves sequence ordering across connections).
        await Assert.That(async () =>
        {
            await using var producer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithIdempotence(true)
                .WithAdaptiveConnections(maxConnections: 5)
                .Build();
        }).ThrowsNothing();
    }

    [Test]
    public async Task TransactionalProducer_WithConnectionsPerBrokerGreaterThan1_Throws()
    {
        // Transactional producers require a single connection per broker for
        // transaction coordinator requests.
        await Assert.That(() =>
        {
            Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithTransactionalId("txn-1")
                .WithConnectionsPerBroker(2)
                .Build();
        }).Throws<InvalidOperationException>();
    }

    #endregion
}
