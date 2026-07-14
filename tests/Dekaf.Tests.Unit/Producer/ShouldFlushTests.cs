using System.Diagnostics;
using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for PartitionBatch.ShouldFlush() short-linger behavior.
/// When LingerMs > 0, awaited produces (with completion sources) use a short linger
/// of min(2ms, LingerMs/2) instead of flushing immediately.
/// When LingerMs == 0, awaited produces flush immediately.
/// </summary>
public class ShouldFlushTests
{
    private static readonly Type PartitionBatchType = typeof(RecordAccumulator).Assembly
        .GetType("Dekaf.Producer.PartitionBatch")!;

    private static readonly FieldInfo RecordCountField = PartitionBatchType
        .GetField("_recordCount", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo CompletionSourceCountField = PartitionBatchType
        .GetField("_completionSourceCount", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo CreatedStopwatchTimestampField = PartitionBatchType
        .GetField("_createdStopwatchTimestamp", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo ShouldFlushMethod = PartitionBatchType
        .GetMethod("ShouldFlush", BindingFlags.Public | BindingFlags.Instance)!;

    private static ProducerOptions CreateTestOptions()
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 1000,
            LingerMs = 10
        };
    }

    private static object CreateBatch()
    {
        var options = CreateTestOptions();
        var tp = new TopicPartition("test-topic", 0);
        var ctor = PartitionBatchType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public,
            new[] { typeof(TopicPartition), typeof(ProducerOptions) })!;
        return ctor.Invoke(new object[] { tp, options });
    }

    private static bool InvokeShouldFlush(object batch, long nowStopwatchTimestamp, int lingerMs)
    {
        return (bool)ShouldFlushMethod.Invoke(batch, new object[] { nowStopwatchTimestamp, lingerMs })!;
    }

    private static void SetRecordCount(object batch, int count)
    {
        RecordCountField.SetValue(batch, count);
    }

    private static void SetCompletionSourceCount(object batch, int count)
    {
        CompletionSourceCountField.SetValue(batch, count);
    }

    private static long SetCreatedAtMillisecondsAgo(object batch, double millisecondsAgo)
    {
        var now = Stopwatch.GetTimestamp();
        var ticksAgo = (long)(millisecondsAgo * Stopwatch.Frequency / 1000.0);
        CreatedStopwatchTimestampField.SetValue(batch, now - ticksAgo);
        return now;
    }

    // --- LingerMs == 0: backwards compatibility (immediate flush) ---

    [Test]
    public async Task LingerMs0_WithCompletionSources_FlushesImmediately()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 0); // Age = 0ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 0);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task LingerMs0_NoCompletionSources_FlushesImmediately()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        var now = SetCreatedAtMillisecondsAgo(batch, 0); // Age = 0ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 0);
        await Assert.That(result).IsTrue();
    }

    // --- LingerMs = 1: short linger uses half the configured linger ---

    [Test]
    [Arguments(0.4, false)]
    [Arguments(0.5, true)]
    public async Task LingerMs1_WithCompletionSources_UsesHalfLinger(
        double ageMilliseconds,
        bool expected)
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, ageMilliseconds);

        var result = InvokeShouldFlush(batch, now, lingerMs: 1);
        await Assert.That(result).IsEqualTo(expected);
    }

    // --- LingerMs = 5: short linger caps at 2ms ---

    [Test]
    public async Task LingerMs5_WithCompletionSources_Age0ms_DoesNotFlush()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 0); // Age = 0ms, short linger = 2ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs5_WithCompletionSources_Age19ms_DoesNotFlush()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 1.9); // Age = 1.9ms < 2ms short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs5_WithCompletionSources_Age2ms_Flushes()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 2.0); // Age = 2ms = capped short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsTrue();
    }

    // --- LingerMs = 20: short linger caps at 2ms ---

    [Test]
    public async Task LingerMs20_WithCompletionSources_Age19ms_DoesNotFlush()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 1.9); // Age = 1.9ms < 2ms short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 20);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs20_WithCompletionSources_Age2ms_Flushes()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 2.0); // Age = 2ms = capped short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 20);
        await Assert.That(result).IsTrue();
    }

    // --- Fire-and-forget (no completion sources) unchanged ---

    [Test]
    public async Task LingerMs5_NoCompletionSources_Age4ms_DoesNotFlush()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        var now = SetCreatedAtMillisecondsAgo(batch, 4.0); // Age = 4ms < 5ms linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs5_NoCompletionSources_Age5ms_Flushes()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        var now = SetCreatedAtMillisecondsAgo(batch, 5.0); // Age = 5ms = full linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsTrue();
    }

    // --- Empty batch always returns false ---

    [Test]
    public async Task EmptyBatch_NeverFlushes()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 0);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 100.0); // Very old, but empty

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    // --- Large LingerMs: short linger caps at 2ms ---

    [Test]
    public async Task LingerMs100_WithCompletionSources_Age19ms_DoesNotFlush()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 1.9); // Age = 1.9ms < 2ms short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 100);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs100_WithCompletionSources_Age2ms_Flushes()
    {
        var batch = CreateBatch();

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        var now = SetCreatedAtMillisecondsAgo(batch, 2.0); // Age = 2ms = capped short linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 100);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments(true, false, false, 0, true)]
    [Arguments(false, true, true, 100, true)]
    [Arguments(false, true, false, 100, false)]
    [Arguments(false, false, true, 100, true)]
    [Arguments(false, false, true, 999, true)]
    [Arguments(false, false, true, 1_000, false)]
    public async Task DeliveryCoupledLinger_DefersOnlyBusyUnderfilledBatches(
        bool isMuted,
        bool serializeBatchesPerPartition,
        bool hasPipelineBatch,
        int currentBatchSize,
        bool expected)
    {
        var result = RecordAccumulator.ShouldDeferPartialBatchSeal(
            isMuted,
            serializeBatchesPerPartition,
            hasPipelineBatch,
            currentBatchSize,
            maximumBatchSize: 1_000);

        await Assert.That(result).IsEqualTo(expected);
    }
}
