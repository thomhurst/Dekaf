using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for PartitionBatch.ShouldFlush() micro-linger behavior.
/// When LingerMs > 0, awaited produces (with completion sources) use a micro-linger
/// of min(1ms, LingerMs/10) instead of flushing immediately.
/// When LingerMs == 0 (default), awaited produces flush immediately (backwards compatible).
/// </summary>
public class ShouldFlushTests
{
    private static readonly Type PartitionBatchType = typeof(RecordAccumulator).Assembly
        .GetType("Dekaf.Producer.PartitionBatch")!;

    private static readonly FieldInfo RecordCountField = PartitionBatchType
        .GetField("_recordCount", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo CompletionSourceCountField = PartitionBatchType
        .GetField("_completionSourceCount", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo CreatedAtField = PartitionBatchType
        .GetField("_createdAt", BindingFlags.NonPublic | BindingFlags.Instance)!;

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

    private static bool InvokeShouldFlush(object batch, DateTimeOffset now, int lingerMs)
    {
        return (bool)ShouldFlushMethod.Invoke(batch, new object[] { now, lingerMs })!;
    }

    private static void SetRecordCount(object batch, int count)
    {
        RecordCountField.SetValue(batch, count);
    }

    private static void SetCompletionSourceCount(object batch, int count)
    {
        CompletionSourceCountField.SetValue(batch, count);
    }

    private static void SetCreatedAt(object batch, DateTimeOffset createdAt)
    {
        CreatedAtField.SetValue(batch, createdAt);
    }

    // --- LingerMs == 0: backwards compatibility (immediate flush) ---

    [Test]
    public async Task LingerMs0_WithCompletionSources_FlushesImmediately()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now); // Age = 0ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 0);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task LingerMs0_NoCompletionSources_FlushesImmediately()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        SetCreatedAt(batch, now); // Age = 0ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 0);
        await Assert.That(result).IsTrue();
    }

    // --- LingerMs = 5: micro-linger = 0.5ms ---

    [Test]
    public async Task LingerMs5_WithCompletionSources_Age0ms_DoesNotFlush()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now); // Age = 0ms, micro-linger = 0.5ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs5_WithCompletionSources_AgeAtMicroLinger_Flushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-0.5)); // Age = 0.5ms = micro-linger threshold

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task LingerMs5_WithCompletionSources_Age1ms_Flushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-1.0)); // Age = 1ms, well past 0.5ms micro-linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsTrue();
    }

    // --- LingerMs = 20: micro-linger = min(1ms, 20/10) = 1ms (capped) ---

    [Test]
    public async Task LingerMs20_WithCompletionSources_Age09ms_DoesNotFlush()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-0.9)); // Age = 0.9ms, micro-linger = 1ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 20);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs20_WithCompletionSources_Age1ms_Flushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-1.0)); // Age = 1ms = capped micro-linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 20);
        await Assert.That(result).IsTrue();
    }

    // --- Fire-and-forget (no completion sources) unchanged ---

    [Test]
    public async Task LingerMs5_NoCompletionSources_Age4ms_DoesNotFlush()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        SetCreatedAt(batch, now.AddMilliseconds(-4.0)); // Age = 4ms < 5ms linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs5_NoCompletionSources_Age5ms_Flushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 0);
        SetCreatedAt(batch, now.AddMilliseconds(-5.0)); // Age = 5ms = full linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsTrue();
    }

    // --- Empty batch always returns false ---

    [Test]
    public async Task EmptyBatch_NeverFlushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 0);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-100.0)); // Very old, but empty

        var result = InvokeShouldFlush(batch, now, lingerMs: 5);
        await Assert.That(result).IsFalse();
    }

    // --- Large LingerMs: micro-linger caps at 1ms ---

    [Test]
    public async Task LingerMs100_WithCompletionSources_Age09ms_DoesNotFlush()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-0.9)); // micro-linger = min(1ms, 100/10) = 1ms

        var result = InvokeShouldFlush(batch, now, lingerMs: 100);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LingerMs100_WithCompletionSources_Age1ms_Flushes()
    {
        var batch = CreateBatch();
        var now = DateTimeOffset.UtcNow;

        SetRecordCount(batch, 1);
        SetCompletionSourceCount(batch, 1);
        SetCreatedAt(batch, now.AddMilliseconds(-1.0)); // Age = 1ms = capped micro-linger

        var result = InvokeShouldFlush(batch, now, lingerMs: 100);
        await Assert.That(result).IsTrue();
    }
}
