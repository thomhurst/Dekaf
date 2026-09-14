using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class StandardClientTelemetryMetricsTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ConcurrentRecording_CollectsConsistentAverageAndMaximum(bool queue)
    {
        var recorder = new StandardClientTelemetryMetrics(queue, () => 1000, 1000);
        var prefix = queue ? StandardClientTelemetryMetrics.QueuePrefix : StandardClientTelemetryMetrics.FetchPrefix;
        string[] prefixes = [prefix];
        recorder.Subscribe(prefixes, 0);
        var subscription = new ClientTelemetrySubscription(Guid.NewGuid(), 1, 0, 1000, 10000, false, prefixes);
        var metrics = new List<ClientTelemetryMetric>(2);
        if (queue)
        {
            var batch = recorder.BeginQueueTimeBatch();
            batch.Record(1000, 1010);
            batch.Complete();
        }
        else
        {
            recorder.RecordRequest(ApiKey.Fetch, 1000, 10);
        }
        recorder.Collect(subscription, metrics, 0);
        using var start = new Barrier(5);
        var stop = 0;
        var writers = new Thread[4];
        for (var index = 0; index < writers.Length; index++)
        {
            writers[index] = new Thread(() =>
            {
                start.SignalAndWait();
                while (Volatile.Read(ref stop) == 0)
                {
                    if (queue)
                    {
                        var batch = recorder.BeginQueueTimeBatch();
                        batch.Record(1000, 1010);
                        batch.Record(1000, 1010);
                        batch.Complete();
                    }
                    else
                    {
                        recorder.RecordRequest(ApiKey.Fetch, 1000, 10);
                    }
                }
            }) { IsBackground = true };
            writers[index].Start();
        }

        var observations = 0;
        double? inconsistent = null;
        try
        {
            start.SignalAndWait();
            for (var sample = 0; sample < 10000; sample++)
            {
                metrics.Clear();
                recorder.Collect(subscription, metrics, 0);
                foreach (var metric in metrics)
                {
                    observations++;
                    if (metric.Value != 10d) inconsistent = metric.Value;
                }
                if (inconsistent.HasValue) break;
            }
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            foreach (var writer in writers) writer.Join();
        }

        await Assert.That(observations).IsGreaterThan(0);
        await Assert.That(inconsistent).IsNull();
        await Assert.That(Value(Collect(recorder, prefixes), prefix + "avg")).IsEqualTo(10d);
    }

    [Test]
    public async Task RequestClock_IsReadOnlyForSubscribedRequestsWithinTheMeasurementWindow()
    {
        long now = 1000;
        var reads = 0;
        var recorder = new StandardClientTelemetryMetrics(false, () => { reads++; return now; }, 1000);
        var prefix = StandardClientTelemetryMetrics.FetchPrefix;
        var before = reads;
        recorder.RecordRequest(ApiKey.Fetch, 1000);
        await Assert.That(reads).IsEqualTo(before);
        recorder.Subscribe([prefix], 0);
        before = reads;
        recorder.RecordRequest(ApiKey.Fetch, 999);
        recorder.RecordRequest(ApiKey.OffsetCommit, 1000);
        await Assert.That(reads).IsEqualTo(before);
        now = 1025;
        recorder.RecordRequest(ApiKey.Fetch, 1000);
        await Assert.That(reads).IsEqualTo(before + 1);
        await Assert.That(Collect(recorder, [prefix + "avg"]).Single().Value).IsEqualTo(25d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConnectionRate_UsesElapsedWindowAndDoesNotReplayUnsubscribedConnections(bool producer)
    {
        long now = 1000;
        var recorder = new StandardClientTelemetryMetrics(producer, () => now, 1000);
        var name = (producer ? StandardClientTelemetryMetrics.ProducerPrefix : StandardClientTelemetryMetrics.ConsumerPrefix)
            + "connection.creation.rate";
        recorder.Subscribe([name], 5);
        await Assert.That(Collect(recorder, [name], connections: 5).Count).IsEqualTo(0);
        now += 2000;
        var metric = Collect(recorder, [name], connections: 9).Single();
        await Assert.That(metric.Value).IsEqualTo(2d);
        await Assert.That(metric.Kind).IsEqualTo(ClientTelemetryMetricKind.Gauge);
        await Assert.That(metric.Unit).IsEqualTo("1/s");
        now += 1000;
        await Assert.That(Collect(recorder, [name], connections: 9).Single().Value).IsEqualTo(0d);
        recorder.Disable();
        now += 1000;
        recorder.Subscribe([name], 20);
        now += 1000;
        await Assert.That(Collect(recorder, [name], connections: 21).Single().Value).IsEqualTo(1d);
    }

    [Test]
    public async Task QueueTime_WeightsBatchesAndResetsAfterUnsubscribe()
    {
        long now = 1000;
        var recorder = new StandardClientTelemetryMetrics(true, () => now, 1000);
        var prefix = StandardClientTelemetryMetrics.QueuePrefix;
        recorder.Subscribe([prefix], 0);
        await Assert.That(Collect(recorder, [prefix]).Count).IsEqualTo(0);
        var samples = recorder.BeginQueueTimeBatch();
        samples.Record(1000, 1010);
        samples.Record(1000, 1030);
        await Assert.That(Collect(recorder, [prefix]).Count).IsEqualTo(0);
        samples.Complete();
        var metrics = Collect(recorder, [prefix]);
        await Assert.That(metrics.Single(m => m.Name.EndsWith("avg", StringComparison.Ordinal)).Value).IsEqualTo(20d);
        await Assert.That(metrics.Single(m => m.Name.EndsWith("max", StringComparison.Ordinal)).Value).IsEqualTo(30d);
        await Assert.That(metrics.All(m => m.Unit == "ms" && m.Kind == ClientTelemetryMetricKind.Gauge)).IsTrue();
        // Reading one gauge does not consume the other gauge's samples.
        await Assert.That(Collect(recorder, [prefix + "avg"]).Count).IsEqualTo(1);
        await Assert.That(Collect(recorder, [prefix + "max"]).Single().Value).IsEqualTo(30d);
        var oldSubscriptionSamples = recorder.BeginQueueTimeBatch();
        oldSubscriptionSamples.Record(1000, 2000);
        recorder.Disable();
        samples = recorder.BeginQueueTimeBatch();
        samples.Record(1000, 2000);
        samples.Complete();
        now = 3000;
        recorder.Subscribe([prefix], 0);
        oldSubscriptionSamples.Complete();
        samples = recorder.BeginQueueTimeBatch();
        samples.Record(2999, 4000);
        samples.Complete();
        await Assert.That(Collect(recorder, [prefix]).Count).IsEqualTo(0);
        samples = recorder.BeginQueueTimeBatch();
        samples.Record(3000, 3005);
        samples.Complete();
        await Assert.That(Collect(recorder, [prefix + "avg"]).Single().Value).IsEqualTo(5d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RequestLatency_ClassifiesFetchAndCommitAndKeepsGaugesAcrossPushes(bool delta)
    {
        var recorder = new StandardClientTelemetryMetrics(false, () => 1000, 1000);
        string[] prefixes = [StandardClientTelemetryMetrics.CommitPrefix, StandardClientTelemetryMetrics.FetchPrefix];
        recorder.Subscribe(prefixes, 0);
        recorder.RecordRequest(ApiKey.Fetch, 1000, 10);
        recorder.RecordRequest(ApiKey.Fetch, 1000, 30);
        recorder.RecordRequest(ApiKey.OffsetCommit, 1000, 50);
        recorder.RecordRequest(ApiKey.Produce, 1000, 1000);
        recorder.RecordRequest(ApiKey.Fetch, 999, 1000);
        var metrics = Collect(recorder, prefixes, delta);
        await Assert.That(metrics.Count).IsEqualTo(4);
        await Assert.That(Value(metrics, StandardClientTelemetryMetrics.FetchPrefix + "avg")).IsEqualTo(20d);
        await Assert.That(Value(metrics, StandardClientTelemetryMetrics.FetchPrefix + "max")).IsEqualTo(30d);
        await Assert.That(Value(metrics, StandardClientTelemetryMetrics.CommitPrefix + "avg")).IsEqualTo(50d);
        await Assert.That(Collect(recorder, prefixes, delta)).IsEquivalentTo(metrics);
    }

    [Test]
    public async Task RebalanceTotal_HonorsTemporalityAndSubscriptionChanges()
    {
        long now = 1000;
        var recorder = new StandardClientTelemetryMetrics(false, () => now, 1000);
        var prefix = StandardClientTelemetryMetrics.RebalancePrefix;
        recorder.Subscribe([prefix], 0);
        var started = recorder.RebalanceStarted();
        now += 20;
        recorder.RebalanceCompleted(started);
        await Assert.That(Value(Collect(recorder, [prefix], delta: true), prefix + "total")).IsEqualTo(20d);
        started = recorder.RebalanceStarted();
        now += 40;
        recorder.RebalanceCompleted(started);
        var delta = Collect(recorder, [prefix], delta: true);
        await Assert.That(Value(delta, prefix + "total")).IsEqualTo(40d);
        await Assert.That(Value(delta, prefix + "avg")).IsEqualTo(30d);
        await Assert.That(Value(delta, prefix + "max")).IsEqualTo(40d);
        await Assert.That(Value(Collect(recorder, [prefix]), prefix + "total")).IsEqualTo(60d);
        await Assert.That(Value(Collect(recorder, [prefix], delta: true), prefix + "total")).IsEqualTo(0d);
        recorder.Subscribe([prefix + "avg"], 0);
        started = recorder.RebalanceStarted();
        now += 10;
        recorder.RebalanceCompleted(started);
        recorder.Subscribe([prefix], 0);
        await Assert.That(Value(Collect(recorder, [prefix], delta: true), prefix + "total")).IsEqualTo(0d);
        await Assert.That(Value(Collect(recorder, [prefix]), prefix + "total")).IsEqualTo(70d);
        recorder.Disable();
        await Assert.That(recorder.RebalanceStarted()).IsEqualTo(-1L);
        recorder.RebalanceCompleted(started);
        recorder.Subscribe([prefix], 0);
        recorder.RebalanceCompleted(started);
        await Assert.That(Value(Collect(recorder, [prefix]), prefix + "total")).IsEqualTo(70d);
    }

    [Test]
    public async Task PollRatio_IncludesActiveAndNestedWaitsAndExcludesProcessingTime()
    {
        long now = 1000;
        var recorder = new StandardClientTelemetryMetrics(false, () => now, 1000);
        var name = StandardClientTelemetryMetrics.PollIdleRatio;
        recorder.Subscribe([name], 0);
        now += 100;
        await Assert.That(Collect(recorder, [name]).Count).IsEqualTo(0);
        recorder.BeginPollWait();
        now += 100;
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(1d);
        recorder.BeginPollWait();
        now += 100;
        recorder.EndPollWait();
        now += 100;
        recorder.EndPollWait();
        now += 200;
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(0.5d);
        now += 100;
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(0d);
        recorder.BeginPollWait();
        now += 100;
        recorder.Disable();
        now += 100;
        recorder.Subscribe([name], 0);
        now += 100;
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(1d);
        recorder.EndPollWait();
        recorder.Disable();
        now += 100;
        recorder.Subscribe([name], 0);
        now += 100;
        await Assert.That(Collect(recorder, [name]).Count).IsEqualTo(0);
        recorder.Disable();
        recorder.BeginPollWait();
        recorder.Subscribe([name], 0);
        now += 100;
        recorder.EndPollWait();
        await Assert.That(Collect(recorder, [name]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task AssignmentGauge_ReflectsManualAssignmentAndClearWithoutSamples()
    {
        var assigned = 2;
        var recorder = new StandardClientTelemetryMetrics(false)
        {
            AssignedPartitionCountProvider = () => assigned
        };
        var name = StandardClientTelemetryMetrics.AssignedPartitions;
        recorder.Subscribe([name], 0);
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(2d);
        assigned = 0;
        await Assert.That(Value(Collect(recorder, [name]), name)).IsEqualTo(0d);
        await Assert.That(Collect(recorder, []).Count).IsEqualTo(0);
        await Assert.That(Collect(recorder, ["org.apache.kafka.consumer.share."]).Count).IsEqualTo(0);
    }

    private static double Value(List<ClientTelemetryMetric> metrics, string name) => metrics.Single(m => m.Name == name).Value;

    private static List<ClientTelemetryMetric> Collect(StandardClientTelemetryMetrics recorder, string[] prefixes,
        bool delta = false, long connections = 0)
    {
        var metrics = new List<ClientTelemetryMetric>();
        recorder.Collect(new ClientTelemetrySubscription(Guid.NewGuid(), 1, 0, 1000, 10000, delta, prefixes), metrics, connections);
        return metrics;
    }
}
