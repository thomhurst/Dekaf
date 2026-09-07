using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

public sealed partial class OutboxRelayService
{
    private readonly OutboxMetricState _metrics;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var samplingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        Task? sampling = null;
        try
        {
            if (_store is IOutboxMetricsStore metricsStore)
            {
                // One worker for the relay lifetime. Even a synchronously slow custom
                // metrics store cannot run on the publication thread.
                sampling = Task.Run(() => CollectPendingMetricsAsync(metricsStore, samplingCancellation.Token), CancellationToken.None);
            }
            await RunRelayAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                samplingCancellation.Cancel();
                if (sampling is not null)
                    await sampling.ConfigureAwait(false);
            }
            finally
            {
                ResetLeaseState();
                _metrics.Dispose();
            }
        }
    }

    private async Task CollectPendingMetricsAsync(IOutboxMetricsStore store, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (OutboxMetrics.PendingEnabled)
                {
                    using var timeout = new CancellationTokenSource(_options.MetricsCollectionTimeout, _timeProvider);
                    using var queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                    try
                    {
                        var snapshot = await store.GetPendingMetricsAsync(queryCancellation.Token).ConfigureAwait(false);
                        queryCancellation.Token.ThrowIfCancellationRequested();
                        Volatile.Write(ref _metrics.Pending, snapshot);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref _metrics.Pending, null);
                        LogMetricsCollectionFailed(ex);
                    }
                }
                else
                {
                    Volatile.Write(ref _metrics.Pending, null);
                }
                // Delay starts after completion: one outstanding query at most, never a
                // catch-up burst after a slow query or a long process suspension.
                await Task.Delay(_options.MetricsCollectionInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal sampler shutdown; the relay still observes this task before disposal.
        }
        finally
        {
            Volatile.Write(ref _metrics.Pending, null);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> batch,
        CancellationToken cancellationToken) => OutboxMetrics.PublishEnabled
        ? PublishMeasuredAsync(batch, cancellationToken)
        : _publisher.PublishAsync(batch, _options.MessageIdHeaderName, cancellationToken);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<OutboxPublishResult> PublishMeasuredAsync(IReadOnlyList<OutboxMessage> batch,
        CancellationToken cancellationToken)
    {
        var measureDuration = OutboxMetrics.PublishDuration.Enabled;
        var started = measureDuration ? _timeProvider.GetTimestamp() : 0;
        try
        {
            var result = await _publisher.PublishAsync(batch, _options.MessageIdHeaderName, cancellationToken)
                .ConfigureAwait(false);
            // Count the publisher's acknowledged prefix before lease/delete decisions.
            // A retained row acknowledged again on retry counts again, not as a unique delivery.
            OutboxMetrics.Record(OutboxMetrics.Acknowledged, _metrics, result.AckedCount);
            if (result.FirstError is not null &&
                !(result.FirstError is OperationCanceledException && cancellationToken.IsCancellationRequested))
                OutboxMetrics.Record(OutboxMetrics.Failures, _metrics, 1);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            OutboxMetrics.Record(OutboxMetrics.Failures, _metrics, 1);
            throw;
        }
        finally
        {
            if (measureDuration)
                OutboxMetrics.RecordDuration(OutboxMetrics.PublishDuration, _metrics, started);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox metrics collection failed; pending observations are unavailable until the next successful sample")]
    private partial void LogMetricsCollectionFailed(Exception exception);
}
