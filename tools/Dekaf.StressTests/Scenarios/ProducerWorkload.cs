using System.Diagnostics;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

/// <summary>The same load loop and observers run during warmup and measurement.</summary>
internal static class ProducerWorkload
{
    internal static Task<ProducerWorkloadResult> RunAsync(
        IKafkaProducer<string, string> producer, StressTestOptions options, string client, string scenario,
        ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration,
        bool awaitDelivery, CancellationToken cancellationToken, TimeSpan? drainTimeout = null) =>
        MeasureAsync(options, throughput, latency, duration, client, scenario,
            (ingress, delivery) => ProduceDekafAsync(producer, options, throughput, latency, awaitDelivery, ingress, delivery),
            (timeout, token) => StressTestHelpers.FlushWithinDeadlineAsync(producer, throughput, timeout, token),
            !awaitDelivery, drainTimeout ?? StressTestHelpers.OperationTimeout, cancellationToken,
            () => StressTestHelpers.CaptureProducerDeliveryDiagnostics(producer, options));

    // Separate overloads keep calls to each client direct inside the loop. Delegates below
    // run once per load phase, never once per message.
    private static async Task ProduceDekafAsync(
        IKafkaProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, bool awaitDelivery,
        CancellationToken token, CancellationToken deliveryToken)
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
                    await producer.ProduceAsync(options.Topic, StressTestHelpers.GetKey(index), value, deliveryToken).ConfigureAwait(false);
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
            catch (Exception error) when (error is Dekaf.Errors.KafkaException or TimeoutException)
            {
                // Expected client failures remain visible in the result. Harness bugs
                // and unexpected runtime failures must fault the workload instead.
                throughput.RecordError(error, "Produce loop", index);
            }
        }
    }

    internal static Task<ProducerWorkloadResult> RunAsync(
        ConfluentKafka.IProducer<string, string> producer, StressTestOptions options, string client, string scenario,
        ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration,
        bool awaitDelivery, CancellationToken cancellationToken, TimeSpan? drainTimeout = null) =>
        MeasureAsync(options, throughput, latency, duration, client, scenario,
            (ingress, delivery) => ProduceConfluentAsync(producer, options, throughput, latency, awaitDelivery, ingress, delivery),
            (timeout, _) => { ConfluentStressTestHelpers.FlushWithTimeout(producer, throughput, timeout); return Task.CompletedTask; },
            !awaitDelivery, drainTimeout ?? StressTestHelpers.OperationTimeout, cancellationToken);

    private static async Task ProduceConfluentAsync(
        ConfluentKafka.IProducer<string, string> producer, StressTestOptions options,
        ThroughputTracker throughput, LatencyTracker latency, bool awaitDelivery,
        CancellationToken token, CancellationToken deliveryToken)
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
                    await producer.ProduceAsync(options.Topic, message, deliveryToken).ConfigureAwait(false);
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
            catch (Exception error) when (error is ConfluentKafka.KafkaException or TimeoutException)
            {
                throughput.RecordError(error, "Produce loop", index);
            }
        }
    }

    private static async Task<ProducerWorkloadResult> MeasureAsync(
        StressTestOptions options, ThroughputTracker throughput, LatencyTracker latency, TimeSpan duration, string client, string scenario,
        Func<CancellationToken, CancellationToken, Task> produce, Func<TimeSpan, CancellationToken, Task> drain,
        bool flushAfterAdmission, TimeSpan drainTimeout, CancellationToken cancellationToken,
        Func<ProducerDeliveryDiagnosticsSnapshot?>? captureProducerDiagnostics = null)
    {
        if (throughput.Warmup is not null)
        {
            // profile-stress-test.sh anchors trace windows to this measured-phase marker.
            Console.WriteLine($"  Running {client} {scenario} stress test for {options.DurationMinutes} minutes...");
        }
        latency.Reset();
        using var gc = new GcStats();
        using var ingress = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var sampling = new CancellationTokenSource();
        throughput.Start();
        var stopIngress = StopIngressAsync(ingress, duration);
        using var watchdog = options.ProgressWatchdog.Track(throughput, client, scenario, captureProducerDiagnostics);
        var sampler = StressTestHelpers.RunSamplerAsync(throughput, sampling.Token);
        var resources = StressTestHelpers.RunResourceMonitorAsync(sampling.Token);
        var producing = produce(ingress.Token, delivery.Token);
        double workloadSeconds;
        try
        {
            try
            {
                await producing.WaitAsync(ingress.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ingress.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Stop admission at the deadline, then observe the final in-flight send.
                // Canceling ProduceAsync here could hide a message already appended.
            }
            workloadSeconds = throughput.Elapsed.TotalSeconds;
            var drainStarted = Stopwatch.GetTimestamp();
            delivery.CancelAfter(drainTimeout);
            try
            {
                // The first flush can release buffer admission. A final FireAsync may
                // append after it returns, so drain again once ingress has fully exited.
                await FlushRemainingAsync().ConfigureAwait(false);
                await producing.WaitAsync(delivery.Token).ConfigureAwait(false);
                if (flushAfterAdmission)
                    await FlushRemainingAsync().ConfigureAwait(false);
                await latency.WaitForDeliverySamplesAsync().WaitAsync(delivery.Token).ConfigureAwait(false);
                delivery.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (delivery.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Preserve timeout counters and runtime samples in an explicitly failed
                // result. Caller cancellation still propagates; it is not a drain timeout.
                throughput.RecordError("DeliveryDrainTimeout", "Producer delivery drain exceeded its deadline", "Delivery drain");
            }

            Task FlushRemainingAsync()
            {
                var remaining = drainTimeout - Stopwatch.GetElapsedTime(drainStarted);
                if (remaining <= TimeSpan.Zero) delivery.Cancel();
                delivery.Token.ThrowIfCancellationRequested();
                return drain(remaining, delivery.Token);
            }
        }
        finally
        {
            ingress.Cancel();
            delivery.Cancel();
            // A producer ignoring cancellation may finish after a failed drain. Observe
            // that failure without holding up the bounded phase or retaining a task list.
            if (!producing.IsCompletedSuccessfully)
                _ = producing.ContinueWith(static task => _ = task.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
        catch (OperationCanceledException) when (ingress.IsCancellationRequested)
        {
            // Phase cleanup cancels this delay after ingress has already stopped.
        }
    }
}

internal sealed record ProducerWorkloadResult(double WorkloadSeconds, ThroughputSnapshot Throughput, GcSnapshot Gc);
