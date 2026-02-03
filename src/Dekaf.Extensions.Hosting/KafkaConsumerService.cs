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
public abstract class KafkaConsumerService<TKey, TValue> : BackgroundService
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
        _logger.LogError(exception, "Error processing message from {Topic}", result?.Topic);
        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topics.ToArray());

        _logger.LogInformation("Started consuming from topics: {Topics}", string.Join(", ", Topics));

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
            _logger.LogInformation("Consumer service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer service failed");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping consumer service");

        // Commit any pending offsets
        try
        {
            await _consumer.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to commit offsets during shutdown");
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
                .GetAwaiter()
                .GetResult();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Consumer disposal timed out after 30 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during consumer disposal");
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
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
