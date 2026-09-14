using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

internal sealed partial class OutboxNotificationService : BackgroundService
{
    private readonly IOutboxNotificationTransport _transport;
    private readonly OutboxRemoteNotifications _outgoing;
    private readonly Action<int> _received;
    private readonly OutboxRelayOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxNotificationService> _logger;

    public OutboxNotificationService(IOutboxNotificationTransport transport, IOutboxNotifier notifier,
        OutboxRelayOptions options, TimeProvider timeProvider, ILogger<OutboxNotificationService> logger)
    {
        options.Validate();
        if (notifier is not OutboxNotifier local)
            throw new OutboxMisconfigurationException("The outbox notification transport requires the built-in notifier.");
        _transport = transport;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _outgoing = new OutboxRemoteNotifications(options.BucketCount);
        local.SetRemoteNotifications(_outgoing);
        _received = local.NotifyReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Two lifetime workers isolate slow transport implementations from commits,
        // publication and each other. No task or buffer is allocated per message.
        var sender = Task.Run(() => SendAsync(stoppingToken), CancellationToken.None);
        var receiver = Task.Run(() => ReceiveAsync(stoppingToken), CancellationToken.None);
        await Task.WhenAll(sender, receiver).ConfigureAwait(false);
    }

    private async Task SendAsync(CancellationToken stoppingToken)
    {
        var buckets = new int[_options.BucketCount];
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var count = await _outgoing.ReadAsync(buckets, stoppingToken).ConfigureAwait(false);
                if (count == 0)
                    continue;
                try
                {
                    await _transport.PublishAsync(buckets.AsMemory(0, count), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogSendFailed(ex);
                    await Task.Delay(_options.ErrorBackoff, _timeProvider, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ReceiveAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _transport.ListenAsync(_received, stoppingToken).ConfigureAwait(false);
                    if (!stoppingToken.IsCancellationRequested)
                        LogSubscriptionEnded();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogReceiveFailed(ex);
                }
                await Task.Delay(_options.ErrorBackoff, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox notification send failed; periodic polling will discover committed rows")]
    private partial void LogSendFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox notification subscription failed; retrying after ErrorBackoff while polling continues")]
    private partial void LogReceiveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox notification subscription ended; retrying after ErrorBackoff")]
    private partial void LogSubscriptionEnded();
}
