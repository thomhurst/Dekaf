using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Internal;
using Dekaf.Retry;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Processes acquired share records serially. Successful processing or durable routing accepts
/// a record; terminal Retry releases it and stops the service; Discard rejects it and continues.
/// Overrides must not call the consumer or start unobserved work. Long synchronous handlers must
/// yield to allow renewal. Processing must honor cancellation for prompt asynchronous disposal.
/// </summary>
public abstract partial class KafkaShareConsumerService<TKey, TValue> : BackgroundService, IAsyncDisposable
{
    private readonly IKafkaShareConsumer<TKey, TValue> _consumer;
    private readonly ILogger _logger;
    private readonly DeadLetterOptions? _deadLetterOptions;
    private readonly IRetryPolicy? _retryPolicy;
    private readonly IDeadLetterPolicy<TKey, TValue>? _deadLetterPolicy;
    private readonly KafkaShareConsumerServiceOptions _options;
    private readonly CancellationTokenSource _pollCancellation = new();
    private readonly CancellationTokenSource _processingCancellation = new();
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IKafkaProducer<byte[]?, byte[]?>? _producer;
    private readonly AsyncAutoResetSignal _operationCompleted = new(inlineContinuations: true);
    private readonly Action _processingCompletedCallback;
    private Dictionary<string, string>? _retrySourceTopics;
    private AcknowledgeType _recordDisposition;
    private long _lastRenewal;
    private Exception? _acknowledgementFailure;
    private int _shutdownStarted;
    private int _disposeStarted;

    /// <summary>Creates a hosted share consumer. The service owns consumer and routing producer cleanup.</summary>
    protected KafkaShareConsumerService(
        IKafkaShareConsumer<TKey, TValue> consumer, ILogger logger,
        DeadLetterOptions? deadLetterOptions = null, IRetryPolicy? retryPolicy = null,
        KafkaShareConsumerServiceOptions? serviceOptions = null,
        IDeadLetterPolicy<TKey, TValue>? deadLetterPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(logger);
        if (deadLetterPolicy is not null && deadLetterOptions is null)
            throw new ArgumentException("A dead letter policy requires DeadLetterOptions.", nameof(deadLetterPolicy));
        _processingCompletedCallback = _operationCompleted.Signal;
        _consumer = consumer;
        _logger = logger;
        _deadLetterOptions = deadLetterOptions;
        _retryPolicy = retryPolicy;
        _options = serviceOptions ?? new KafkaShareConsumerServiceOptions();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.ShutdownTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_options.ShutdownTimeout.TotalMilliseconds, uint.MaxValue - 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.RenewalInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_options.RenewalInterval.TotalMilliseconds, int.MaxValue - 1);
        _deadLetterPolicy = deadLetterPolicy ?? (deadLetterOptions is null
            ? null : new DefaultDeadLetterPolicy<TKey, TValue>(deadLetterOptions));
    }

    internal DeadLetterOptions? ConfiguredDeadLetterOptions => _deadLetterOptions;

    /// <summary>Gets source topics. Configured retry topics are added automatically.</summary>
    protected abstract IEnumerable<string> Topics { get; }

    /// <summary>Processes one acquired record. Acceptance follows only successful completion.</summary>
    protected abstract ValueTask ProcessAsync(ShareConsumeResult<TKey, TValue> result, CancellationToken cancellationToken);

    /// <summary>Observes processing, polling, initialization, and acknowledgement errors.</summary>
    protected virtual ValueTask OnErrorAsync(Exception exception, ShareConsumeResult<TKey, TValue>? result, CancellationToken cancellationToken)
    {
        LogFailure(exception, result?.Topic);
        return ValueTask.CompletedTask;
    }

    /// <summary>Returns Retry (Release and stop) or Discard (Reject and continue). The default is Retry.</summary>
    protected virtual ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(
        ShareMessageFailureContext<TKey, TValue> context, CancellationToken cancellationToken)
        => new(MessageFailureDisposition.Retry);

    /// <summary>Observes a failed durable retry-topic write.</summary>
    protected virtual ValueTask OnRetryTopicRoutingFailedAsync(Exception exception, ShareConsumeResult<TKey, TValue> result, CancellationToken cancellationToken)
    {
        LogFailure(exception, result.Topic);
        return ValueTask.CompletedTask;
    }

    /// <summary>Observes a failed durable dead-letter write.</summary>
    protected virtual ValueTask OnDeadLetterRoutingFailedAsync(Exception exception, ShareConsumeResult<TKey, TValue> result, CancellationToken cancellationToken)
    {
        LogFailure(exception, result.Topic);
        return ValueTask.CompletedTask;
    }

    /// <summary>Builds the owned routing producer. Overrides must provide broker-acknowledged durable delivery.</summary>
    protected virtual IKafkaProducer<byte[]?, byte[]?> CreateDeadLetterProducer()
    {
        var builder = Kafka.CreateProducer<byte[]?, byte[]?>();
        if (_deadLetterOptions?.BootstrapServers is { } servers)
            builder.WithBootstrapServers(servers);
        _deadLetterOptions?.ConfigureProducer?.Invoke(builder);
        // Routing must be durable even if the callback selected weaker acknowledgements.
        return builder.WithAcks(Acks.All).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stoppingRegistration = stoppingToken.UnsafeRegister(static state =>
            ((KafkaShareConsumerService<TKey, TValue>)state!).BeginShutdown(), this);
        using var shutdownRegistration = _shutdownCancellation.Token.UnsafeRegister(static state =>
            ((KafkaShareConsumerService<TKey, TValue>)state!).CancelProcessing(), this);
        using var processingRegistration = _processingCancellation.Token.UnsafeRegister(static state =>
            ((AsyncAutoResetSignal)state!).Signal(), _operationCompleted);
        ShareConsumeResult<TKey, TValue>? current = null;
        var initialized = false;
        try
        {
            if (_consumer is not IShareConsumerConfiguration { AcknowledgementMode: ShareAcknowledgementMode.Explicit })
                throw new InvalidOperationException("Hosted share consumers require verified Explicit acknowledgement mode. Wrappers must implement IShareConsumerConfiguration.");
            if (_consumer is IHostedShareConsumer hostedConsumer)
                hostedConsumer.ObserveAcknowledgements(ObserveAcknowledgements);
            if (_deadLetterOptions is not null)
            {
                if (!_deadLetterOptions.AwaitDelivery)
                    throw new InvalidOperationException("Hosted share consumers require AwaitDelivery for durable routing.");
                if (_consumer is not IRawShareRecordAccessor raw)
                    throw new InvalidOperationException("This share consumer does not support raw record capture required for durable routing.");
                raw.EnableRawRecordTracking();
                _producer = CreateDeadLetterProducer();
                await _producer.InitializeAsync(_pollCancellation.Token).ConfigureAwait(false);
            }
            await _consumer.InitializeAsync(_pollCancellation.Token).ConfigureAwait(false);
            initialized = true;
            _consumer.Subscribe(BuildTopics());
            await foreach (var record in _consumer.PollAsync(_pollCancellation.Token).ConfigureAwait(false))
            {
                current = record;
                if (_acknowledgementFailure is { } pollFailure)
                    ExceptionDispatchInfo.Capture(pollFailure).Throw();
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    break;
                _lastRenewal = _consumer is IHostedShareConsumer acquisition
                    ? acquisition.AcquisitionStartedTimestamp : Stopwatch.GetTimestamp();
                CheckAcquisitionDeadline();
                var processingToken = _processingCancellation.Token;
                _recordDisposition = AcknowledgeType.Accept;
                Exception? processingFailure = null;
                while (true)
                {
                    try
                    {
                        var operation = processingFailure is null
                            ? BeginProcessingAsync(record, processingToken)
                            : HandleFailureAsync(processingFailure, record, processingToken);
                        Exception? renewalFailure = null;
                        if (!operation.IsCompleted)
                        {
                            // Register one cached callback, without converting an application
                            // ValueTask to Task or allocating a per-record async wrapper.
                            operation.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(_processingCompletedCallback);
                            while (!operation.IsCompleted)
                            {
                                try
                                {
                                    if (processingToken.IsCancellationRequested)
                                    {
                                        // A ValueTask backed by IValueTaskSource permits only one
                                        // completion registration. Keep waiting on our reusable signal
                                        // until the original operation completes; never await it twice.
                                        await _operationCompleted.WaitAsync(Timeout.Infinite).ConfigureAwait(false);
                                        continue;
                                    }
                                    if (await _operationCompleted.WaitAsync(GetRenewalDelayMs()).ConfigureAwait(false))
                                        continue;
                                    if (operation.IsCompleted)
                                        break;
                                    CheckAcquisitionDeadline();
                                    _consumer.Acknowledge(record, AcknowledgeType.Renew);
                                    _lastRenewal = Stopwatch.GetTimestamp();
                                    await _consumer.CommitAsync(processingToken).ConfigureAwait(false);
                                }
                                catch (Exception exception)
                                {
                                    renewalFailure ??= exception;
                                    CancelProcessing();
                                }
                            }
                        }
                        try
                        {
                            // Already complete: this consumes the result once without registering
                            // another continuation on a single-use application ValueTask.
                            await operation.ConfigureAwait(false);
                        }
                        catch (Exception exception) when (renewalFailure is not null)
                        {
                            LogFailure(exception, record.Topic);
                        }
                        if (renewalFailure is not null)
                            ExceptionDispatchInfo.Capture(renewalFailure).Throw();
                        break;
                    }
                    catch (Exception exception) when (processingFailure is null && !processingToken.IsCancellationRequested)
                    {
                        processingFailure = exception;
                    }
                }
                processingToken.ThrowIfCancellationRequested();
                CheckAcquisitionDeadline();
                _consumer.Acknowledge(record, _recordDisposition);
                current = null;
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    break;
            }
            if (_acknowledgementFailure is { } acknowledgementFailure)
                ExceptionDispatchInfo.Capture(acknowledgementFailure).Throw();
        }
        catch (OperationCanceledException) when (_pollCancellation.IsCancellationRequested || _processingCancellation.IsCancellationRequested)
        {
            if (_acknowledgementFailure is { } failure)
            {
                await OnErrorAsync(failure, current, CancellationToken.None).ConfigureAwait(false);
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
        catch (Exception exception)
        {
            await OnErrorAsync(exception, current, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            BeginShutdown();
            // All consumer operations and cleanup stay on this execution chain. StopAsync never
            // disposes a consumer that is still used by an uncooperative application handler.
            if (current is not null && initialized)
            {
                try
                {
                    _consumer.Acknowledge(current, AcknowledgeType.Release);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, current.Topic);
                }
            }
            if (initialized)
            {
                try
                {
                    await _consumer.CommitAsync(_shutdownCancellation.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, null);
                }
                try
                {
                    await _consumer.CloseAsync(_shutdownCancellation.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, null);
                }
            }
            if (_producer is not null)
            {
                try
                {
                    await _producer.FlushAsync(_shutdownCancellation.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, null);
                }
            }
        }
    }

    private void ObserveAcknowledgements(ReadOnlySpan<ShareAcknowledgementCommitResult> results)
    {
        foreach (ref readonly var result in results)
        {
            if (result.Exception is { } exception)
            {
                if (exception is OperationCanceledException && _pollCancellation.IsCancellationRequested)
                    continue;
                _acknowledgementFailure ??= exception;
                _pollCancellation.Cancel();
            }
        }
    }

    private void CheckAcquisitionDeadline()
    {
        if (_consumer.AcquisitionLockTimeoutMs is > 0 and var timeout &&
            Stopwatch.GetElapsedTime(_lastRenewal).TotalMilliseconds >= timeout)
            throw new KafkaException("The acquisition lock may have expired. The record will not be accepted.");
    }

    private ValueTask BeginProcessingAsync(ShareConsumeResult<TKey, TValue> record, CancellationToken cancellationToken)
    {
        if (_retrySourceTopics?.ContainsKey(record.Topic) == true &&
            RetryTopicHeaders.TryGetDueAt(record.Headers, out var dueAt) && dueAt > DateTimeOffset.UtcNow)
            return ProcessWhenDueAsync(record, dueAt, cancellationToken);
        return ProcessAsync(record, cancellationToken);
    }

    private async ValueTask ProcessWhenDueAsync(ShareConsumeResult<TKey, TValue> record,
        DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        while (dueAt > DateTimeOffset.UtcNow)
        {
            var remaining = dueAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(remaining.TotalMilliseconds, int.MaxValue - 1)), cancellationToken).ConfigureAwait(false);
        }
        await ProcessAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private int GetRenewalDelayMs()
    {
        CheckAcquisitionDeadline();
        var intervalMs = _options.RenewalInterval.TotalMilliseconds;
        if (_consumer.AcquisitionLockTimeoutMs is > 0 and var timeout)
        {
            var remaining = timeout - Stopwatch.GetElapsedTime(_lastRenewal).TotalMilliseconds;
            intervalMs = Math.Min(intervalMs, remaining / 3);
        }
        return (int)Math.Max(1, intervalMs);
    }

    private void CancelProcessing()
    {
        try
        {
            _processingCancellation.Cancel();
        }
        catch (Exception exception)
        {
            LogFailure(exception, null);
        }
    }

    private async ValueTask HandleFailureAsync(Exception exception,
        ShareConsumeResult<TKey, TValue> record, CancellationToken cancellationToken)
    {
        var sourceTopic = record.Topic;
        var isRetryTopic = _retrySourceTopics is not null && _retrySourceTopics.TryGetValue(record.Topic, out sourceTopic);
        sourceTopic ??= record.Topic;
        var previousFailures = isRetryTopic ? RetryTopicHeaders.GetFailureCount(record.Headers) : 0;
        var attempt = 1;
        var retryTopics = _deadLetterOptions?.RetryTopics;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await OnErrorAsync(exception, record, cancellationToken).ConfigureAwait(false);
            var delay = _retryPolicy?.GetNextDelay(attempt, exception);
            var retryInPlace = delay is not null || (_retryPolicy is null && retryTopics?.IsEnabled != true &&
                attempt < (_deadLetterOptions?.MaxFailures ?? 1));
            if (retryInPlace)
            {
                if (delay is not null)
                    await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
                attempt++;
                try
                {
                    await ProcessAsync(record, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception next) when (next is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    exception = next;
                    continue;
                }
            }

            var failureCount = checked(previousFailures + attempt);
            var routingHeaders = BuildRoutingHeaders(record, sourceTopic, isRetryTopic);
            var converted = new ConsumeResult<TKey, TValue>(record.Topic, record.Partition, record.Offset,
                record.Key, record.Value, routingHeaders, record.TimestampMs, TimestampType.CreateTime, null);
            Exception? routingException = null;
            var stage = MessageFailureStage.Processing;
            var exhausted = false;
            if (retryTopics?.IsEnabled == true)
            {
                if (retryTopics.TryGetRetryTopic(sourceTopic, checked(previousFailures + 1), out var topic, out var retryDelay))
                {
                    stage = MessageFailureStage.RetryTopicRouting;
                    try
                    {
                        await RouteAsync(record, topic, RetryTopicHeaders.Build(converted,
                            previousFailures + 1, retryDelay, DateTimeOffset.UtcNow + retryDelay), cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                    {
                        routingException = failure;
                        await OnRetryTopicRoutingFailedAsync(failure, record, cancellationToken).ConfigureAwait(false);
                    }
                }
                else exhausted = true;
            }
            if (_deadLetterPolicy is not null && (exhausted || _deadLetterPolicy.ShouldDeadLetter(converted, exception, failureCount)))
            {
                stage = MessageFailureStage.DeadLetterRouting;
                try
                {
                    await RouteAsync(record, _deadLetterPolicy.GetDeadLetterTopic(sourceTopic),
                        DeadLetterHeaders.Build(converted, exception, failureCount, _deadLetterOptions!.IncludeExceptionInHeaders),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    routingException = failure;
                    await OnDeadLetterRoutingFailedAsync(failure, record, cancellationToken).ConfigureAwait(false);
                }
            }
            var disposition = await GetFailureDispositionAsync(new ShareMessageFailureContext<TKey, TValue>(record,
                exception, attempt, failureCount, stage, routingException), cancellationToken).ConfigureAwait(false);
            if (disposition == MessageFailureDisposition.Discard)
            {
                _recordDisposition = AcknowledgeType.Reject;
                return;
            }
            if (disposition != MessageFailureDisposition.Retry)
                throw new InvalidOperationException($"Unsupported message failure disposition: {disposition}.");
            ExceptionDispatchInfo.Capture(routingException ?? exception).Throw();
        }
    }

    private async ValueTask RouteAsync(ShareConsumeResult<TKey, TValue> record, string topic,
        Dekaf.Serialization.Headers headers, CancellationToken cancellationToken)
    {
        if (_producer is null || _consumer is not IRawShareRecordAccessor raw ||
            !raw.TryGetRawRecord(new TopicPartitionOffset(record.Topic, record.Partition, record.Offset), out var key, out var value))
            throw new InvalidOperationException("Raw record data is unavailable; the source record cannot be accepted.");
        await _producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
        {
            Topic = topic, Key = key, Value = value, Headers = headers
        }, cancellationToken).ConfigureAwait(false);
    }

    private string[] BuildTopics()
    {
        var source = Topics.ToArray();
        if (_deadLetterOptions?.RetryTopics is not { IsEnabled: true } retryTopics) return source;
        var sourceTopics = new HashSet<string>(source, StringComparer.Ordinal);
        var topics = new HashSet<string>(source, StringComparer.Ordinal);
        _retrySourceTopics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var topic in source)
        {
            foreach (var retryTopic in retryTopics.GetRetryTopics(topic))
            {
                topics.Add(retryTopic);
                // Explicit source topics never inherit retry-control semantics from their names.
                if (sourceTopics.Contains(retryTopic)) continue;
                if (_retrySourceTopics.TryGetValue(retryTopic, out var existingSource) && existingSource != topic)
                    throw new InvalidOperationException($"Retry topic '{retryTopic}' belongs to multiple source topics.");
                _retrySourceTopics[retryTopic] = topic;
            }
        }
        return topics.ToArray();
    }

    private static Dekaf.Serialization.Headers BuildRoutingHeaders(
        ShareConsumeResult<TKey, TValue> record, string sourceTopic, bool isRetryTopic)
    {
        // Error path only. Keep the original record intact for application hooks, and strip
        // source-supplied retry controls before either shared header builder can interpret them.
        var headers = new Dekaf.Serialization.Headers(record.Headers.Count + 1);
        for (var index = 0; index < record.Headers.Count; index++)
        {
            var header = record.Headers[index];
            if (header.Key == RetryTopicHeaders.SourceTopicKey || (!isRetryTopic && header.Key is
                RetryTopicHeaders.SourcePartitionKey or RetryTopicHeaders.SourceOffsetKey or
                RetryTopicHeaders.FailureCountKey or RetryTopicHeaders.DelayMsKey or RetryTopicHeaders.DueTimestampMsKey))
                continue;
            headers.Add(header);
        }
        // Topic membership, not a message header, determines the original source topic.
        if (isRetryTopic) headers.Add(RetryTopicHeaders.SourceTopicKey, sourceTopic);
        return headers;
    }

    private void BeginShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;
        _shutdownCancellation.CancelAfter(_options.ShutdownTimeout);
        _pollCancellation.Cancel();
        if (!_options.DrainOnShutdown) CancelProcessing();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        BeginShutdown();
        using var registration = cancellationToken.UnsafeRegister(static state =>
            ((CancellationTokenSource)state!).Cancel(), _shutdownCancellation);
        try
        {
            await base.StopAsync(_shutdownCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested) { }
    }

    public override void Dispose()
    {
        // BackgroundService has only a synchronous disposal API. Cancel immediately; the
        // asynchronous cleanup chain observes errors and waits for all application work.
        base.Dispose();
        _ = DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            await _disposed.Task.ConfigureAwait(false);
            return;
        }
        try
        {
            BeginShutdown();
            CancelProcessing();
            base.Dispose();
            if (ExecuteTask is not null)
            {
                try
                {
                    await ExecuteTask.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, null);
                }
            }
            if (_producer is not null)
            {
                try
                {
                    await _producer.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogFailure(exception, null);
                }
            }
            try
            {
                await _consumer.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogFailure(exception, null);
            }
        }
        finally
        {
            _operationCompleted.Dispose();
            _pollCancellation.Dispose();
            _processingCancellation.Dispose();
            _shutdownCancellation.Dispose();
            _disposed.TrySetResult();
            GC.SuppressFinalize(this);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Share consumer service operation failed for {Topic}")]
    private partial void LogFailure(Exception exception, string? topic);
}
