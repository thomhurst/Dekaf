using Dekaf.Consumer;
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

    protected KafkaConsumerService(
        IKafkaConsumer<TKey, TValue> consumer,
        ILogger logger)
    {
        _consumer = consumer;
        _logger = logger;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _consumer.InitializeAsync(stoppingToken).ConfigureAwait(false);

        _consumer.Subscribe(Topics.ToArray());

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var topics = string.Join(", ", Topics);
            LogStartedConsuming(topics);
        }

        try
        {
            await foreach (var result in _consumer.ConsumeAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(result, stoppingToken);
                }
                catch (Exception ex)
                {
                    await OnErrorAsync(ex, result, stoppingToken);
                }
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
            await _consumer.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogCommitOffsetsFailed(ex);
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
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
