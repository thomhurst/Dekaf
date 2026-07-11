using System.Diagnostics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class KafkaConsumerPrefetchMemorySignalTests
{
    [Test]
    public async Task BudgetGrowth_RatchetsRecordWrapperPools()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            QueuedMinMessages = 2
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        ((IBudgetedInstance)consumer).OnBudgetChanged(512UL * 1024 * 1024);
        var expected = PoolSizing.ForConsumerRecordWrappers(512L * 1024 * 1024);

        await Assert.That(RecordBatch.MaxPoolSizeValue).IsGreaterThanOrEqualTo(expected);
        await Assert.That(LazyRecordList.MaxPoolSizeValue).IsGreaterThanOrEqualTo(expected);
    }

    [Test]
    public async Task ProcessingComplete_DrainsMemoryPressureOnConsumerThread()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAdaptiveFetchSizing = true,
            AdaptiveFetchSizingOptions = new AdaptiveFetchSizingOptions
            {
                InitialPartitionFetchBytes = 4_000_000,
                InitialFetchMaxBytes = 100_000_000,
                StableWindowCount = 1
            }
        };
        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        var sizer = GetPrivateField<AdaptiveFetchSizer>(consumer, "_adaptiveFetchSizer");
        SetPrivateField(consumer, "_adaptiveFetchMemoryPressureSignals", 1);

        GetReportAdaptiveProcessingComplete().Invoke(consumer, [TimeSpan.Zero]);

        await Assert.That(sizer.CurrentPartitionFetchBytes).IsLessThan(4_000_000);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsLessThan(100_000_000);
        await Assert.That(GetPrivateField<int>(consumer, "_adaptiveFetchMemoryPressureSignals")).IsEqualTo(0);
    }

    [Test]
    public async Task AdaptiveFetchGrowth_RatchetsRecordWrapperPools()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            QueuedMinMessages = 2,
            EnableAdaptiveFetchSizing = true,
            AdaptiveFetchSizingOptions = new AdaptiveFetchSizingOptions
            {
                MinPartitionFetchBytes = 1024 * 1024,
                InitialPartitionFetchBytes = 1024 * 1024,
                MaxPartitionFetchBytes = 256 * 1024 * 1024,
                MinFetchMaxBytes = 1024 * 1024,
                InitialFetchMaxBytes = 16 * 1024 * 1024,
                MaxFetchMaxBytes = 512 * 1024 * 1024,
                GrowthFactor = 2,
                StableWindowCount = 1
            }
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        var sizer = GetAdaptiveFetchSizer(consumer);
        SetFetchTiming(sizer, TimeSpan.FromSeconds(1));

        GetReportAdaptiveProcessingComplete().Invoke(consumer, [TimeSpan.FromMilliseconds(1)]);

        var expectedBytes = Math.Min(
            (long)sizer.CurrentFetchMaxBytes,
            64L * sizer.CurrentPartitionFetchBytes);
        var expected = PoolSizing.ForConsumerRecordWrappers(expectedBytes);
        await Assert.That(sizer.CurrentFetchMaxBytes).IsEqualTo(32 * 1024 * 1024);
        await Assert.That(RecordBatch.MaxPoolSizeValue).IsGreaterThanOrEqualTo(expected);
        await Assert.That(LazyRecordList.MaxPoolSizeValue).IsGreaterThanOrEqualTo(expected);
    }

    [Test]
    public async Task TrackPrefetchedBytes_RepeatedRelease_DoesNotAccumulateMemorySignals()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            QueuedMinMessages = 2
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        using var pending = PendingFetchData.Create("test-topic", 0,
        [
            new RecordBatch
            {
                BatchLength = 128,
                Records = []
            }
        ]);

        var semaphore = GetPrefetchMemoryAvailable(consumer);
        var trackPrefetchedBytes = GetTrackPrefetchedBytes();

        for (var i = 0; i < 3; i++)
        {
            trackPrefetchedBytes.Invoke(consumer, [pending, false]);
            trackPrefetchedBytes.Invoke(consumer, [pending, true]);
        }

        await Assert.That(semaphore.CurrentCount).IsEqualTo(1);
    }

    private static SemaphoreSlim GetPrefetchMemoryAvailable(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchMemoryAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchMemoryAvailable field not found - was it renamed?");

        return (SemaphoreSlim)field.GetValue(consumer)!;
    }

    private static MethodInfo GetTrackPrefetchedBytes()
    {
        return typeof(KafkaConsumer<string, string>)
            .GetMethod("TrackPrefetchedBytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TrackPrefetchedBytes method not found - was it renamed?");
    }

    private static AdaptiveFetchSizer GetAdaptiveFetchSizer(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_adaptiveFetchSizer", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_adaptiveFetchSizer field not found - was it renamed?");

        return (AdaptiveFetchSizer)field.GetValue(consumer)!;
    }

    private static void SetFetchTiming(AdaptiveFetchSizer sizer, TimeSpan duration)
    {
        var startField = typeof(AdaptiveFetchSizer)
            .GetField("_lastFetchStartTimestamp", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var endField = typeof(AdaptiveFetchSizer)
            .GetField("_lastFetchEndTimestamp", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var end = Stopwatch.GetTimestamp();
        var start = end - (long)(duration.TotalSeconds * Stopwatch.Frequency);
        startField.SetValue(sizer, start);
        endField.SetValue(sizer, end);
    }

    private static MethodInfo GetReportAdaptiveProcessingComplete()
    {
        return typeof(KafkaConsumer<string, string>)
            .GetMethod("ReportAdaptiveProcessingComplete", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ReportAdaptiveProcessingComplete method not found - was it renamed?");
    }

    private static T GetPrivateField<T>(KafkaConsumer<string, string> consumer, string fieldName) =>
        (T)(typeof(KafkaConsumer<string, string>)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(consumer)
        ?? throw new InvalidOperationException($"{fieldName} field not found - was it renamed?"));

    private static void SetPrivateField<T>(
        KafkaConsumer<string, string> consumer,
        string fieldName,
        T value)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found - was it renamed?");
        field.SetValue(consumer, value);
    }
}
