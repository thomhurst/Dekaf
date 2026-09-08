using System.Diagnostics;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

/// <summary>The same load loop and observers run during warmup and measurement.</summary>
internal static class ProducerWorkload
{
    internal static Task<ProducerWorkloadResult> RunAsync(
        IKafkaProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration,
        bool awaitDelivery, CancellationToken cancellationToken) =>
        MeasureAsync(options, throughput, latency, duration, "Dekaf",
            token => ProduceDekafAsync(producer, options, throughput, latency, awaitDelivery, token, cancellationToken),
            () => StressTestHelpers.FlushWithTimeoutAsync(producer, throughput), cancellationToken,
            () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options));

    // Separate overloads keep calls to each client direct inside the loop. Delegates below
    // run once per load phase, never once per message.
    private static async Task ProduceDekafAsync(
        IKafkaProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, bool awaitDelivery,
        CancellationToken token, CancellationToken callerToken)
    {
        var value = new string('x', options.MessageSizeBytes);
        var index = 0L;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (awaitDelivery)
                {
                    var started = Stopwatch.GetTimestamp();
                    await producer.ProduceAsync(options.Topic, StressTestHelpers.GetKey(index), value, callerToken).ConfigureAwait(false);
                    latency.RecordTicks(Stopwatch.GetTimestamp() - started);
                }
                else if (index % StressTestHelpers.LatencySampleInterval == 0)
                {
                    StressTestHelpers.SampleDeliveryLatency(producer, options.Topic,
                        StressTestHelpers.GetKey(index), value, latency, throughput, index);
                }
                else
                {
                    await producer.FireAsync(options.Topic, StressTestHelpers.GetKey(index), value).ConfigureAwait(false);
                }
                throughput.RecordMessage(options.MessageSizeBytes);
                index++;
                if (index % 100_000 == 0)
                {
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) { throughput.RecordError(error, "Produce loop", index); }
        }
    }

    internal static Task<ProducerWorkloadResult> RunAsync(
        ConfluentKafka.IProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration,
        bool awaitDelivery, CancellationToken cancellationToken) =>
        MeasureAsync(options, throughput, latency, duration, "Confluent",
            token => ProduceConfluentAsync(producer, options, throughput, latency, awaitDelivery, token, cancellationToken),
            () => { ConfluentStressTestHelpers.FlushWithTimeout(producer, throughput); return Task.CompletedTask; },
            cancellationToken);

    private static async Task ProduceConfluentAsync(
        ConfluentKafka.IProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, bool awaitDelivery,
        CancellationToken token, CancellationToken callerToken)
    {
        var value = new string('x', options.MessageSizeBytes);
        var index = 0L;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var message = new ConfluentKafka.Message<string, string>
                {
                    Key = StressTestHelpers.GetKey(index), Value = value
                };
                if (awaitDelivery)
                {
                    var started = Stopwatch.GetTimestamp();
                    await producer.ProduceAsync(options.Topic, message, callerToken).ConfigureAwait(false);
                    latency.RecordTicks(Stopwatch.GetTimestamp() - started);
                }
                else if (index % StressTestHelpers.LatencySampleInterval == 0)
                {
                    ConfluentStressTestHelpers.SampleDeliveryLatency(producer, options.Topic, message,
                        latency, throughput, token, index);
                }
                else
                {
                    ConfluentStressTestHelpers.ProduceWithBackpressure(producer, options.Topic, message, null, token);
                }
                throughput.RecordMessage(options.MessageSizeBytes);
                index++;
                if (index % 100_000 == 0)
                {
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) { throughput.RecordError(error, "Produce loop", index); }
        }
    }

    private static async Task<ProducerWorkloadResult> MeasureAsync(
        StressTestOptions options, ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration, string client,
        Func<CancellationToken, Task> produce, Func<Task> drain, CancellationToken cancellationToken,
        Func<ProducerDeliveryDiagnosticsSnapshot?>? captureProducerDiagnostics = null)
    {
        if (throughput.Warmup is not null)
        {
            // profile-stress-test.sh anchors trace windows to this measured-phase marker.
            Console.WriteLine($"  Running {client} producer stress test for {options.DurationMinutes} minutes...");
        }
        using var gc = new GcStats();
        using var ingress = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var sampling = new CancellationTokenSource();
        throughput.Start();
        var stopIngress = StopIngressAsync(ingress, duration);
        using var watchdog = options.ProgressWatchdog.Track(throughput, client, "producer workload", captureProducerDiagnostics);
        var sampler = StressTestHelpers.RunSamplerAsync(throughput, sampling.Token);
        var resources = StressTestHelpers.RunResourceMonitorAsync(sampling.Token);
        double workloadSeconds;
        try
        {
            await produce(ingress.Token).ConfigureAwait(false);
            workloadSeconds = throughput.Elapsed.TotalSeconds;
            await drain().ConfigureAwait(false);
            await latency.WaitForDeliverySamplesAsync().ConfigureAwait(false);
        }
        finally
        {
            ingress.Cancel();
            sampling.Cancel();
            await stopIngress.ConfigureAwait(false);
            await Task.WhenAll(sampler, resources).ConfigureAwait(false);
            throughput.Stop();
            gc.Capture();
        }
        return new ProducerWorkloadResult(workloadSeconds, throughput.GetSnapshot(), gc.ToSnapshot());
    }

    // Timer callbacks can arrive slightly before their nominal duration. Recheck an
    // elapsed monotonic clock instead of counting an early timer as full workload warmup.
    private static async Task StopIngressAsync(CancellationTokenSource ingress, TimeSpan duration)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            while (!ingress.IsCancellationRequested)
            {
                var remaining = duration - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero) break;
                var delay = remaining < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : remaining;
                await Task.Delay(delay, ingress.Token).ConfigureAwait(false);
            }
            ingress.Cancel();
        }
        catch (OperationCanceledException) when (ingress.IsCancellationRequested) { }
    }
}

internal sealed record ProducerWorkloadResult(double WorkloadSeconds, ThroughputSnapshot Throughput, GcSnapshot Gc);
