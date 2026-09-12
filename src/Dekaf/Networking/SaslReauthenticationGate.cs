namespace Dekaf.Networking;

/// <summary>
/// Stops request admission during a SASL exchange. The uncontended path uses only
/// atomics; completion sources are allocated once per re-authentication attempt.
/// The connection serializes calls to PauseAsync/Resume with its re-authentication lock.
/// </summary>
internal sealed class SaslReauthenticationGate
{
    private TaskCompletionSource<bool>? _resumed;
    private TaskCompletionSource<bool>? _drained;
    private int _active;
    private int _closed;

    // Pair each successful entry with Exit in a finally block. A non-generic ValueTask
    // reuses the send method's existing awaiter instead of enlarging its async state.
    public ValueTask EnterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        return TryEnter() ? ValueTask.CompletedTask : EnterSlowAsync(cancellationToken);
    }

    private bool TryEnter()
    {
        var active = Volatile.Read(ref _active);
        while (active >= 0)
        {
            var observed = Interlocked.CompareExchange(ref _active, active + 1, active);
            if (observed == active)
                return true;
            active = observed;
        }
        return false;
    }

    private async ValueTask EnterSlowAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            cancellationToken.ThrowIfCancellationRequested();
            var resumed = Volatile.Read(ref _resumed);
            if (TryEnter())
                return;
            if (resumed is not null)
                await resumed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        var drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _drained, drained);
        Volatile.Write(ref _resumed, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        var active = Volatile.Read(ref _active);
        while (true)
        {
            var observed = Interlocked.CompareExchange(ref _active, active | int.MinValue, active);
            if (observed == active)
                break;
            active = observed;
        }
        if (active == 0 || Volatile.Read(ref _closed) != 0)
            drained.TrySetResult(true);
        return drained.Task.WaitAsync(cancellationToken);
    }

    public void Resume()
    {
        // Pause may be cancelled while old requests are still draining. Preserve their count.
        var active = Volatile.Read(ref _active);
        while (true)
        {
            var observed = Interlocked.CompareExchange(ref _active, active & int.MaxValue, active);
            if (observed == active)
                break;
            active = observed;
        }
        Volatile.Write(ref _drained, null);
        Interlocked.Exchange(ref _resumed, null)?.TrySetResult(true);
    }

    public void Close()
    {
        Volatile.Write(ref _closed, 1);
        Volatile.Read(ref _drained)?.TrySetResult(true);
        Volatile.Read(ref _resumed)?.TrySetResult(true);
    }

    public void Exit()
    {
        if (Interlocked.Decrement(ref _active) == int.MinValue)
            Volatile.Read(ref _drained)?.TrySetResult(true);
    }
}
