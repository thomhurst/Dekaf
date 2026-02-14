using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Base class for hosted services that consume from Kafka.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public abstract partial class KafkaConsumerService<TKey, TValue> : BackgroundService
{
    private readonly IKafkaConsumer<TKey, TValue> _consumer;
    private readonly ILogger _logger;
    private readonly DeadLetterOptions? _deadLetterOptions;
    private readonly IDeadLetterPolicy<TKey, TValue>? _deadLetterPolicy;
    private readonly IRetryPolicy? _retryPolicy;
    private IKafkaProducer<byte[]?, byte[]?>? _dlqProducer;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerService{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="consumer">The Kafka consumer instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="deadLetterOptions">Optional dead letter queue configuration.</param>
    /// <param name="retryPolicy">Optional retry policy for message processing failures.</param>
    protected KafkaConsumerService(
        IKafkaConsumer<TKey, TValue> consumer,
        ILogger logger,
        DeadLetterOptions? deadLetterOptions = null,
        IRetryPolicy? retryPolicy = null)
    {
        _consumer = consumer;
        _logger = logger;
        _deadLetterOptions = deadLetterOptions;
        _retryPolicy = retryPolicy;
        if (deadLetterOptions is not null)
        {
            _deadLetterPolicy = new DefaultDeadLetterPolicy<TKey, TValue>(deadLetterOptions);
        }
    }

    /// <summary>
    /// Gets the topics to subscribe to.
    /// </summary>
    protected abstract IEnumerable<string> Topics { get; }

    /// <summary>
    /// Processes a consumed message.
    /// </summary>
    protected abstract ValueTask ProcessAsync(ConsumeResult<TKey, TValue> result, CancellationToken cancellationToken);

    /// <summary>
    /// Called when an error occurs during message processing.
    /// </summary>
    protected virtual ValueTask OnErrorAsync(Exception exception, ConsumeResult<TKey, TValue>? result, CancellationToken cancellationToken)
    {
        LogProcessingError(exception, result?.Topic);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called when routing a message to the dead letter queue fails.
    /// Override to implement custom failure handling (e.g., metrics, alerts).
    /// Default implementation logs the error.
    /// </summary>
    protected virtual ValueTask OnDeadLetterRoutingFailedAsync(
        Exception exception, ConsumeResult<TKey, TValue> result, CancellationToken cancellationToken)
    {
        LogDeadLetterRoutingFailed(exception, result.Topic, result.Partition, result.Offset);
        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Enable raw byte capture and create DLQ producer if configured
        if (_deadLetterOptions is not null && _consumer is IRawRecordAccessor rawAccessor)
        {
            rawAccessor.EnableRawRecordTracking();
            _dlqProducer = BuildDlqProducer();
            await _dlqProducer.InitializeAsync(stoppingToken).ConfigureAwait(false);
        }

        await _consumer.InitializeAsync(stoppingToken).ConfigureAwait(false);

        _consumer.Subscribe(Topics.ToArray());

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var topics = string.Join(", ", Topics);
            LogStartedConsuming(topics);
        }

        try
        {
            await foreach (var result in _consumer.ConsumeAsync(stoppingToken).ConfigureAwait(false))
            {
                await ProcessWithRetriesAsync(result, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogConsumerServiceStopping();
        }
        catch (Exception ex)
        {
            LogConsumerServiceFailed(ex);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogStoppingConsumerService();

        // Commit any pending offsets
        try
        {
            await _consumer.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCommitOffsetsFailed(ex);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        // Flush and dispose DLQ producer
        if (_dlqProducer is not null)
        {
            try
            {
                _dlqProducer.FlushAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                LogDlqProducerFlushError(ex);
            }

            try
            {
                _dlqProducer.DisposeAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                LogDlqProducerDisposalError(ex);
            }
        }

        // Add timeout to prevent indefinite hang during disposal
        // This is necessary because BackgroundService.Dispose is synchronous
        // but consumer disposal may involve network operations
        try
        {
            _consumer.DisposeAsync().AsTask()
                .WaitAsync(TimeSpan.FromSeconds(30))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (TimeoutException)
        {
            LogConsumerDisposalTimedOut();
        }
        catch (Exception ex)
        {
            LogConsumerDisposalError(ex);
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private async ValueTask ProcessWithRetriesAsync(
        ConsumeResult<TKey, TValue> result, CancellationToken stoppingToken)
    {
        byte[]? rawKey = null;
        byte[]? rawValue = null;

        if (_retryPolicy is not null)
        {
            // Retry policy drives retry count and delays
            var attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    await ProcessAsync(result, stoppingToken).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    CaptureRawBytesOnFirstFailure(attempt, ref rawKey, ref rawValue);

                    await OnErrorAsync(ex, result, stoppingToken).ConfigureAwait(false);

                    var delay = _retryPolicy.GetNextDelay(attempt, ex);
                    if (delay is not null)
                    {
                        await Task.Delay(delay.Value, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    // Policy says stop retrying - check DLQ
                    if (_deadLetterPolicy is not null &&
                        _deadLetterPolicy.ShouldDeadLetter(result, ex, attempt))
                    {
                        await RouteToDeadLetterAsync(result, ex, rawKey, rawValue, attempt, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    return;
                }
            }
        }

        // No retry policy: existing behavior (immediate retry up to MaxFailures)
        var maxAttempts = _deadLetterOptions?.MaxFailures ?? 1;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await ProcessAsync(result, stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                CaptureRawBytesOnFirstFailure(attempt, ref rawKey, ref rawValue);

                await OnErrorAsync(ex, result, stoppingToken).ConfigureAwait(false);

                if (_deadLetterPolicy is not null &&
                    _deadLetterPolicy.ShouldDeadLetter(result, ex, attempt))
                {
                    await RouteToDeadLetterAsync(result, ex, rawKey, rawValue, attempt, stoppingToken)
                        .ConfigureAwait(false);
                    return;
                }
            }
        }
    }

    private void CaptureRawBytesOnFirstFailure(int attempt, ref byte[]? rawKey, ref byte[]? rawValue)
    {
        if (attempt == 1 &&
            _deadLetterPolicy is not null &&
            _consumer is IRawRecordAccessor accessor &&
            accessor.TryGetCurrentRawRecord(out var rawKeyMemory, out var rawValueMemory))
        {
            rawKey = rawKeyMemory.IsEmpty ? null : rawKeyMemory.ToArray();
            rawValue = rawValueMemory.IsEmpty ? null : rawValueMemory.ToArray();
        }
    }

    private async ValueTask RouteToDeadLetterAsync(
        ConsumeResult<TKey, TValue> result, Exception exception,
        byte[]? rawKey, byte[]? rawValue, int failureCount,
        CancellationToken cancellationToken)
    {
        if (_dlqProducer is null || _deadLetterPolicy is null || _deadLetterOptions is null)
            return;

        try
        {
            var dlqTopic = _deadLetterPolicy.GetDeadLetterTopic(result.Topic);

            var headers = DeadLetterHeaders.Build(
                result, exception, failureCount,
                _deadLetterOptions.IncludeExceptionInHeaders);

            var message = new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = dlqTopic,
                Key = rawKey,
                Value = rawValue ?? [],
                Headers = headers
            };

            if (_deadLetterOptions.AwaitDelivery)
            {
                await _dlqProducer.ProduceAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _dlqProducer.Send(message);
            }

            LogMessageRoutedToDeadLetter(result.Topic, result.Partition, result.Offset, dlqTopic);
        }
        catch (Exception dlqEx)
        {
            await OnDeadLetterRoutingFailedAsync(dlqEx, result, cancellationToken).ConfigureAwait(false);
        }
    }

    private IKafkaProducer<byte[]?, byte[]?> BuildDlqProducer()
    {
        if (_deadLetterOptions?.BootstrapServers is null && _deadLetterOptions?.ConfigureProducer is null)
        {
            throw new InvalidOperationException(
                "Dead letter queue bootstrap servers must be configured. " +
                "Call WithBootstrapServers() on the dead letter queue builder, " +
                "or provide a ConfigureProducer action that sets bootstrap servers.");
        }

        var builder = Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithClientId("dekaf-dlq-producer")
            .WithAcks(Acks.All);

        if (_deadLetterOptions?.BootstrapServers is not null)
        {
            builder.WithBootstrapServers(_deadLetterOptions.BootstrapServers);
        }

        _deadLetterOptions?.ConfigureProducer?.Invoke(builder);

        return builder.Build();
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing message from {Topic}")]
    private partial void LogProcessingError(Exception ex, string? topic);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started consuming from topics: {Topics}")]
    private partial void LogStartedConsuming(string topics);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumer service stopping")]
    private partial void LogConsumerServiceStopping();

    [LoggerMessage(Level = LogLevel.Error, Message = "Consumer service failed")]
    private partial void LogConsumerServiceFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping consumer service")]
    private partial void LogStoppingConsumerService();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to commit offsets during shutdown")]
    private partial void LogCommitOffsetsFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer disposal timed out after 30 seconds")]
    private partial void LogConsumerDisposalTimedOut();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during consumer disposal")]
    private partial void LogConsumerDisposalError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message from {Topic}[{Partition}]@{Offset} routed to dead letter topic {DlqTopic}")]
    private partial void LogMessageRoutedToDeadLetter(string topic, int partition, long offset, string dlqTopic);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to route message from {Topic}[{Partition}]@{Offset} to dead letter queue")]
    private partial void LogDeadLetterRoutingFailed(Exception ex, string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to flush DLQ producer during shutdown")]
    private partial void LogDlqProducerFlushError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during DLQ producer disposal")]
    private partial void LogDlqProducerDisposalError(Exception ex);

    #endregion
}

/// <summary>
/// Extension methods for adding Kafka consumer hosted services.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Adds a Kafka consumer hosted service.
    /// </summary>
    public static IHostBuilder UseKafkaConsumer<TService, TKey, TValue>(this IHostBuilder builder)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        return builder.ConfigureServices((context, services) =>
        {
            services.AddHostedService<TService>();
        });
    }
}
