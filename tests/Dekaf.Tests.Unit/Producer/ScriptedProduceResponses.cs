using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Scripts a fixed set of produce responses for a <see cref="TestKafkaConnection"/> and converts
/// any unscripted (extra) send into an immediate, diagnosable failure. Previously an exhausted
/// script threw <c>InvalidOperationException("Queue empty.")</c> from <c>Queue.Dequeue</c>, which
/// <c>BrokerSender.SendCoalescedAsync</c> treats as a retriable connection failure — with the
/// zero retry backoff these tests configure, the sender retried against the exhausted script
/// forever and the test died as an opaque 30s timeout (#2187). An unscripted send now records
/// a diagnostic (rethrow it from an <c>[After(Test)]</c> hook), cancels the token issued by
/// <see cref="Guard"/> so in-test waits abort instantly, and parks the sender on a response
/// that never completes so no retry loop can start.
/// </summary>
internal sealed class ScriptedProduceResponses : IDisposable
{
    private readonly Queue<TaskCompletionSource<ProduceResponse>> _responses;
    private readonly Action? _onSend;
    private readonly TaskCompletionSource<ProduceResponse> _unscriptedSendResponse = new();
    // object, not System.Threading.Lock: this project also targets net8.0.
    private readonly object _lock = new();
    private CancellationTokenSource? _guardCts;
    private int _dequeuedCount;
    private volatile InvalidOperationException? _unscriptedSendFailure;

    public ScriptedProduceResponses(
        Queue<TaskCompletionSource<ProduceResponse>> responses,
        Action? onSend)
    {
        _responses = responses;
        _onSend = onSend;
    }

    public InvalidOperationException? UnscriptedSendFailure => _unscriptedSendFailure;

    /// <summary>
    /// Links the test's cancellation token to this script so an unscripted send aborts every
    /// in-test wait immediately instead of hanging until the test timeout.
    /// </summary>
    public CancellationToken Guard(CancellationToken testToken)
    {
        CancellationTokenSource guardCts;
        bool alreadyFailed;
        lock (_lock)
        {
            guardCts = CancellationTokenSource.CreateLinkedTokenSource(testToken);
            _guardCts = guardCts;
            alreadyFailed = _unscriptedSendFailure is not null;
        }

        if (alreadyFailed)
            guardCts.Cancel();
        return guardCts.Token;
    }

    public Task<ProduceResponse> Dequeue()
    {
        TaskCompletionSource<ProduceResponse>? scripted = null;
        CancellationTokenSource? guardCts = null;
        lock (_lock)
        {
            if (_responses.Count > 0)
            {
                scripted = _responses.Dequeue();
                _dequeuedCount++;
            }
            else
            {
                // Capture the sender-side stack: it names the code path that produced the
                // extra send, which is the input the underlying-race investigation needs.
                _unscriptedSendFailure ??= new InvalidOperationException(
                    $"Unscripted send: all {_dequeuedCount} scripted produce responses were " +
                    "already consumed when the sender issued another send. Sender stack: " +
                    Environment.StackTrace);
                guardCts = _guardCts;
            }
        }

        if (scripted is not null)
        {
            _onSend?.Invoke();
            return scripted.Task;
        }

        // Cancel asynchronously: a synchronous Cancel would resume guarded test waits inline
        // on this sender-loop thread, and a resumed continuation may join this same thread
        // (sender.DisposeAsync in the test's finally).
        try
        {
            _ = guardCts?.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // The After(Test) hook already tore the guard down; the recorded
            // failure still fails the test.
        }

        return _unscriptedSendResponse.Task;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _guardCts?.Dispose();
            _guardCts = null;
        }
    }
}
