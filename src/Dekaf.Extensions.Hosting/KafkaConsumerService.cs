using System.Diagnostics.CodeAnalysis;
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
public abstract partial class KafkaConsumerService<TKey, TValue> : BackgroundService, IAsyncDisposable
{
    private readonly IKafkaConsumer<TKey, TValue> _consumer;
    private readonly ILogger _logger;
    private readonly DeadLetterOptions? _deadLetterOptions;
    private readonly IDeadLetterPolicy<TKey, TValue>? _deadLetterPolicy;
    private readonly IRetryPolicy? _retryPolicy;
    private readonly RetryTopicOptions? _retryTopicOptions;
    private readonly KafkaConsumerServiceOptions _serviceOptions;
    private readonly object _retryTopicPostponementsLock = new();
    private readonly Dictionary<TopicPartition, RetryTopicPostponement> _retryTopicPostponements = [];
    private IKafkaProducer<byte[]?, byte[]?>? _dlqProducer;
    private int _disposeStarted;
    private static readonly TimeSpan MaxRetryTopicDelayChunk = TimeSpan.FromMilliseconds(int.MaxValue - 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaConsumerService{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="consumer">The Kafka consumer instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="deadLetterOptions">Optional dead letter queue configuration.</param>
    /// <param name="retryPolicy">Optional retry policy for message processing failures.</param>
    /// <param name="serviceOptions">Optional shutdown and service behavior configuration.</param>
    protected KafkaConsumerService(
        IKafkaConsumer<TKey, TValue> consumer,
        ILogger logger,
        DeadLetterOptions? deadLetterOptions = null,
        IRetryPolicy? retryPolicy = null,
        KafkaConsumerServiceOptions? serviceOptions = null)
    {
        _consumer = consumer;
        _logger = logger;
        _deadLetterOptions = deadLetterOptions;
        _retryPolicy = retryPolicy;
        _retryTopicOptions = deadLetterOptions?.RetryTopics;
        _serviceOptions = serviceOptions ?? new KafkaConsumerServiceOptions();
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

    /// <summary>
    /// Called when routing a message to a retry topic fails.
    /// Override to implement custom failure handling (e.g., metrics, alerts).
    /// Default implementation logs the error.
    /// </summary>
    protected virtual ValueTask OnRetryTopicRoutingFailedAsync(
        Exception exception, ConsumeResult<TKey, TValue> result, CancellationToken cancellationToken)
    {
        LogRetryTopicRoutingFailed(exception, result.Topic, result.Partition, result.Offset);
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

        var subscriptionTopics = BuildSubscriptionTopics();
        _consumer.Subscribe(subscriptionTopics);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var topics = string.Join(", ", subscriptionTopics);
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

        // First, cancel ExecuteAsync to stop the normal consume loop
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        // Then drain any remaining buffered messages
        if (_serviceOptions.DrainOnShutdown)
        {
            await DrainBufferedMessagesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Final offset commit before disposal
        try
        {
            await _consumer.CommitAsync(cancellationToken).ConfigureAwait(false);
            LogFinalOffsetCommitSucceeded();
        }
        catch (Exception ex)
        {
            LogCommitOffsetsFailed(ex);
        }
    }

    private async Task DrainBufferedMessagesAsync(CancellationToken cancellationToken)
    {
        LogDrainingBufferedMessages();

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCts.CancelAfter(_serviceOptions.ShutdownTimeout);
        var drainedCount = 0;

        try
        {
            // Use ConsumeOneAsync to drain remaining buffered messages
            // with a short timeout to avoid blocking if the buffer is empty
            while (!drainCts.Token.IsCancellationRequested)
            {
                var result = await _consumer.ConsumeOneAsync(
                    TimeSpan.FromMilliseconds(100),
                    drainCts.Token).ConfigureAwait(false);

                if (result is null)
                {
                    // Buffer is empty, draining complete
                    break;
                }

                drainedCount++;
                await ProcessWithRetriesAsync(result.Value, drainCts.Token).ConfigureAwait(false);

                LogDrainProgress(drainedCount);
            }

            LogDrainCompleted(drainedCount);
        }
        catch (OperationCanceledException) when (drainCts.Token.IsCancellationRequested)
        {
            LogDrainTimedOut(_serviceOptions.ShutdownTimeout, drainedCount);
        }
        catch (Exception ex)
        {
            LogDrainFailed(ex);
        }
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) == 0)
        {
            try
            {
                DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                base.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        // Flush and dispose DLQ producer
        if (_dlqProducer is not null)
        {
            try
            {
                await _dlqProducer.FlushAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDlqProducerFlushError(ex);
            }

            try
            {
                await _dlqProducer.DisposeAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDlqProducerDisposalError(ex);
            }
        }

        try
        {
            await _consumer.DisposeAsync().AsTask()
                .WaitAsync(_serviceOptions.ShutdownTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            LogConsumerDisposalTimedOut(_serviceOptions.ShutdownTimeout);
        }
        catch (Exception ex)
        {
            LogConsumerDisposalError(ex);
        }
    }

    private async ValueTask ProcessWithRetriesAsync(
        ConsumeResult<TKey, TValue> result, CancellationToken stoppingToken)
    {
        if (PostponeRetryTopicMessageIfNeeded(result, stoppingToken))
            return;

        byte[]? rawKey = null;
        byte[]? rawValue = null;
        var previousFailureCount = RetryTopicHeaders.GetFailureCount(result.Headers);

        var maxAttemptsWithoutPolicy = _retryTopicOptions?.IsEnabled == true ? 1 : _deadLetterOptions?.MaxFailures ?? 1;
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

                var delay = _retryPolicy?.GetNextDelay(attempt, ex);
                if (delay is not null)
                {
                    await Task.Delay(delay.Value, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var deadLetterFailureCount = previousFailureCount + attempt;
                var retryTopicFailureCount = GetNextRetryTopicFailureCount(previousFailureCount);
                var retryTopicResult = await TryRouteToRetryTopicAsync(
                        result,
                        rawKey,
                        rawValue,
                        retryTopicFailureCount,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (retryTopicResult == RetryTopicRouteResult.Routed)
                {
                    return;
                }

                if (ShouldRouteToDeadLetter(result, ex, deadLetterFailureCount, retryTopicResult))
                {
                    await RouteToDeadLetterAsync(result, ex, rawKey, rawValue, deadLetterFailureCount, stoppingToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (_retryPolicy is null && attempt < maxAttemptsWithoutPolicy)
                    continue;

                return;
            }
        }
    }

    private static int GetNextRetryTopicFailureCount(int previousFailureCount) => previousFailureCount + 1;

    private bool ShouldRouteToDeadLetter(
        ConsumeResult<TKey, TValue> result,
        Exception exception,
        int failureCount,
        RetryTopicRouteResult retryTopicResult)
    {
        if (_deadLetterPolicy is null)
            return false;

        return retryTopicResult == RetryTopicRouteResult.Exhausted ||
            _deadLetterPolicy.ShouldDeadLetter(result, exception, failureCount);
    }

    private string[] BuildSubscriptionTopics()
    {
        var sourceTopics = Topics.ToArray();
        if (_retryTopicOptions?.IsEnabled != true)
            return sourceTopics;

        var topics = new HashSet<string>(sourceTopics, StringComparer.Ordinal);
        foreach (var sourceTopic in sourceTopics)
        {
            foreach (var retryTopic in _retryTopicOptions.GetRetryTopics(sourceTopic))
            {
                topics.Add(retryTopic);
            }
        }

        return topics.ToArray();
    }

    private bool PostponeRetryTopicMessageIfNeeded(
        ConsumeResult<TKey, TValue> result,
        CancellationToken cancellationToken)
    {
        if (_retryTopicOptions?.IsEnabled != true ||
            !RetryTopicHeaders.TryGetDueAt(result.Headers, out var dueAt))
        {
            return false;
        }

        var delay = dueAt - DateTimeOffset.UtcNow;
        if (delay <= TimeSpan.Zero)
            return false;

        var partition = new TopicPartition(result.Topic, result.Partition);
        var postponement = new RetryTopicPostponement(result.Offset, dueAt, delay);
        if (!TryBeginRetryTopicPostponement(partition, postponement))
            return true;

        LogRetryTopicDelay(result.Topic, result.Partition, result.Offset, delay);

        _consumer.Partitions.Pause(partition);
        _consumer.Positions.Seek(new TopicPartitionOffset(
            result.Topic,
            result.Partition,
            result.Offset,
            result.LeaderEpoch ?? -1));

        _ = ResumeRetryTopicPartitionAfterDelayAsync(partition, postponement, cancellationToken);
        return true;
    }

    private bool TryBeginRetryTopicPostponement(
        TopicPartition partition,
        RetryTopicPostponement postponement)
    {
        lock (_retryTopicPostponementsLock)
        {
            if (_retryTopicPostponements.TryGetValue(partition, out var pending) &&
                !IsEarlierRetryTopicPostponement(postponement, pending))
            {
                return false;
            }

            _retryTopicPostponements[partition] = postponement;
            return true;
        }
    }

    private bool TryCompleteRetryTopicPostponement(
        TopicPartition partition,
        RetryTopicPostponement postponement)
    {
        lock (_retryTopicPostponementsLock)
        {
            if (!_retryTopicPostponements.TryGetValue(partition, out var pending) ||
                pending != postponement)
            {
                return false;
            }

            _retryTopicPostponements.Remove(partition);
            return true;
        }
    }

    private static bool IsEarlierRetryTopicPostponement(
        RetryTopicPostponement candidate,
        RetryTopicPostponement pending)
    {
        // Partition order wins over due time; seeking to a later offset can skip an earlier postponed record.
        if (candidate.Offset != pending.Offset)
            return candidate.Offset < pending.Offset;

        return candidate.DueAt < pending.DueAt;
    }

    private async Task ResumeRetryTopicPartitionAfterDelayAsync(
        TopicPartition partition,
        RetryTopicPostponement postponement,
        CancellationToken cancellationToken)
    {
        try
        {
            await DelayUntilRetryTopicDueAsync(postponement.DueAt, cancellationToken).ConfigureAwait(false);
            if (!TryCompleteRetryTopicPostponement(partition, postponement))
                return;

            _consumer.Partitions.Resume(partition);
            LogRetryTopicPartitionResumed(partition.Topic, partition.Partition, postponement.Delay);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogRetryTopicPartitionResumeFailed(ex, partition.Topic, partition.Partition);
        }
    }

    private static async Task DelayUntilRetryTopicDueAsync(
        DateTimeOffset dueAt,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = dueAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return;

            var delay = remaining <= MaxRetryTopicDelayChunk
                ? remaining
                : MaxRetryTopicDelayChunk;

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private readonly record struct RetryTopicPostponement(
        long Offset,
        DateTimeOffset DueAt,
        TimeSpan Delay);

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
            var sourceTopic = RetryTopicHeaders.GetSourceTopic(result.Headers) ?? result.Topic;
            var dlqTopic = _deadLetterPolicy.GetDeadLetterTopic(sourceTopic);

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
                await _dlqProducer.FireAsync(message).ConfigureAwait(false);
            }

            LogMessageRoutedToDeadLetter(result.Topic, result.Partition, result.Offset, dlqTopic);
        }
        catch (Exception dlqEx)
        {
            await OnDeadLetterRoutingFailedAsync(dlqEx, result, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<RetryTopicRouteResult> TryRouteToRetryTopicAsync(
        ConsumeResult<TKey, TValue> result,
        byte[]? rawKey, byte[]? rawValue, int failureCount,
        CancellationToken cancellationToken)
    {
        if (_dlqProducer is null ||
            _retryTopicOptions?.IsEnabled != true)
        {
            return RetryTopicRouteResult.Disabled;
        }

        var sourceTopic = RetryTopicHeaders.GetSourceTopic(result.Headers) ?? result.Topic;
        if (!_retryTopicOptions.TryGetRetryTopic(sourceTopic, failureCount, out var retryTopic, out var delay))
            return RetryTopicRouteResult.Exhausted;

        try
        {
            var dueAt = DateTimeOffset.UtcNow + delay;
            var headers = RetryTopicHeaders.Build(result, failureCount, delay, dueAt);
            var message = new ProducerMessage<byte[]?, byte[]?>
            {
                Topic = retryTopic,
                Key = rawKey,
                Value = rawValue ?? [],
                Headers = headers
            };

            if (_deadLetterOptions?.AwaitDelivery == true)
            {
                await _dlqProducer.ProduceAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _dlqProducer.FireAsync(message).ConfigureAwait(false);
            }

            LogMessageRoutedToRetryTopic(result.Topic, result.Partition, result.Offset, retryTopic, delay, failureCount);
            return RetryTopicRouteResult.Routed;
        }
        catch (Exception retryEx)
        {
            await OnRetryTopicRoutingFailedAsync(retryEx, result, cancellationToken).ConfigureAwait(false);
            return RetryTopicRouteResult.Failed;
        }
    }

    private enum RetryTopicRouteResult
    {
        Disabled,
        Routed,
        Exhausted,
        Failed
    }

    private IKafkaProducer<byte[]?, byte[]?> BuildDlqProducer()
    {
        if (_deadLetterOptions?.BootstrapServers is null && _deadLetterOptions?.ConfigureProducer is null)
        {
            throw new InvalidOperationException(
                "Dead letter or retry topic producer bootstrap servers must be configured. " +
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Final offset commit succeeded during shutdown")]
    private partial void LogFinalOffsetCommitSucceeded();

    [LoggerMessage(Level = LogLevel.Information, Message = "Draining buffered messages before shutdown")]
    private partial void LogDrainingBufferedMessages();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Drain progress: {DrainedCount} message(s) processed so far")]
    private partial void LogDrainProgress(int drainedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Drain completed, {DrainedCount} buffered message(s) processed")]
    private partial void LogDrainCompleted(int drainedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drain timed out after {Timeout}, {DrainedCount} message(s) were processed before timeout")]
    private partial void LogDrainTimedOut(TimeSpan timeout, int drainedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during drain of buffered messages")]
    private partial void LogDrainFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumer disposal timed out after {Timeout}")]
    private partial void LogConsumerDisposalTimedOut(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error during consumer disposal")]
    private partial void LogConsumerDisposalError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message from {Topic}[{Partition}]@{Offset} routed to dead letter topic {DlqTopic}")]
    private partial void LogMessageRoutedToDeadLetter(string topic, int partition, long offset, string dlqTopic);

    [LoggerMessage(Level = LogLevel.Information, Message = "Message from {Topic}[{Partition}]@{Offset} routed to retry topic {RetryTopic} for {Delay} after failure {FailureCount}")]
    private partial void LogMessageRoutedToRetryTopic(string topic, int partition, long offset, string retryTopic, TimeSpan delay, int failureCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Delaying retry topic message {Topic}[{Partition}]@{Offset} for {Delay}")]
    private partial void LogRetryTopicDelay(string topic, int partition, long offset, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resumed retry topic partition {Topic}[{Partition}] after {Delay}")]
    private partial void LogRetryTopicPartitionResumed(string topic, int partition, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to resume retry topic partition {Topic}[{Partition}]")]
    private partial void LogRetryTopicPartitionResumeFailed(Exception ex, string topic, int partition);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to route message from {Topic}[{Partition}]@{Offset} to dead letter queue")]
    private partial void LogDeadLetterRoutingFailed(Exception ex, string topic, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to route message from {Topic}[{Partition}]@{Offset} to retry topic")]
    private partial void LogRetryTopicRoutingFailed(Exception ex, string topic, int partition, long offset);

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
    public static IHostBuilder UseKafkaConsumer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey,
        TValue>(this IHostBuilder builder)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        return builder.ConfigureServices((context, services) =>
        {
            services.AddHostedService<TService>();
        });
    }
}
