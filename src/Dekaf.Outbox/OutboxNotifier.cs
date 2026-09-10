using System.Threading.Channels;

namespace Dekaf.Outbox;

internal sealed class OutboxNotifier : IOutboxNotifier, IDisposable
{
    private readonly Channel<byte> _notifications = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });
    private readonly ITimer _timer;

    public OutboxNotifier(TimeProvider timeProvider)
    {
        _timer = timeProvider.CreateTimer(static state => ((OutboxNotifier)state!).NotifyCommitted(),
            this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void NotifyCommitted() => _notifications.Writer.TryWrite(0);

    public ValueTask WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_notifications.Reader.TryRead(out _))
            return ValueTask.CompletedTask;
        return WaitForNotificationAsync(timeout, cancellationToken);
    }

    private async ValueTask WaitForNotificationAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        _timer.Change(timeout, Timeout.InfiniteTimeSpan);
        try
        {
            await _notifications.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose() => _timer.Dispose();
}
