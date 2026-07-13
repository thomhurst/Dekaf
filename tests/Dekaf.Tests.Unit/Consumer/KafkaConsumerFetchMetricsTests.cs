using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel("MeterListener")]
public sealed class KafkaConsumerFetchMetricsTests
{
    [Test]
    public async Task RecordFetchDuration_WhenDisabled_DoesNotCreateBrokerTags()
    {
        await using var consumer = CreateConsumer();

        InvokeRecordFetchDuration(consumer, brokerId: 7);

        await Assert.That(GetFetchDurationMetricTagsCache(consumer).Count).IsEqualTo(0);
    }

    [Test]
    public async Task RecordFetchDuration_WhenEnabled_ReusesBrokerTags()
    {
        var expectedBrokerId = BitConverter.ToInt32(Guid.NewGuid().ToByteArray());
        double? duration = null;
        int? brokerId = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                instrument.Name == "messaging.consumer.fetch.duration")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "messaging.consumer.fetch.duration" &&
                GetTag(tags, DekafDiagnostics.MessagingKafkaBrokerId) is int metricBrokerId &&
                metricBrokerId == expectedBrokerId)
            {
                duration = measurement;
                brokerId = metricBrokerId;
            }
        });
        listener.Start();

        await Assert.That(DekafMetrics.FetchDuration.Enabled).IsTrue();

        await using var consumer = CreateConsumer();

        InvokeRecordFetchDuration(consumer, expectedBrokerId);
        InvokeRecordFetchDuration(consumer, expectedBrokerId);

        await Assert.That(duration).IsNotNull();
        await Assert.That(brokerId).IsEqualTo(expectedBrokerId);
        await Assert.That(GetFetchDurationMetricTagsCache(consumer).Count).IsEqualTo(1);
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "metrics-test"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
    }

    private static void InvokeRecordFetchDuration(
        KafkaConsumer<string, string> consumer,
        int brokerId)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "RecordFetchDuration",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RecordFetchDuration method not found.");

        method.Invoke(consumer, [Stopwatch.GetTimestamp(), brokerId]);
    }

    private static ConcurrentDictionary<int, TagList> GetFetchDurationMetricTagsCache(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchDurationMetricTagsCache",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchDurationMetricTagsCache field not found.");

        return (ConcurrentDictionary<int, TagList>)field.GetValue(consumer)!;
    }

    private static object? GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
                return tag.Value;
        }

        return null;
    }
}
