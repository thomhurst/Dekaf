using System.Threading.Tasks.Sources;
using Dekaf.Protocol;

namespace Dekaf.Networking;

/// <summary>
/// Represents a connection to a Kafka broker.
/// </summary>
public interface IKafkaConnection : IAsyncDisposable
{
    /// <summary>
    /// The broker ID this connection is connected to.
    /// </summary>
    int BrokerId { get; }

    /// <summary>
    /// The host this connection is connected to.
    /// </summary>
    string Host { get; }

    /// <summary>
    /// The port this connection is connected to.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Whether the connection is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Sends a request and waits for the response.
    /// </summary>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    /// <summary>
    /// Sends a request without waiting for a response (fire-and-forget).
    /// Used for Produce requests with acks=0.
    /// </summary>
    ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    /// <summary>
    /// Sends a request and returns a task that completes when the response arrives.
    /// Unlike SendAsync, this method returns immediately after writing the request,
    /// enabling pipelining of multiple requests over a single connection.
    /// </summary>
    Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    /// <summary>
    /// Sends a request without waiting for a response (fire-and-forget), where the caller's
    /// cancellation token already carries a timeout. Skips the per-write CancellationTokenSource
    /// allocation — a hot-path optimization for BrokerSender.
    /// </summary>
    /// <remarks>
    /// The caller's token MUST be exclusively a timeout token (e.g., from a CancellationTokenSource
    /// configured with only CancelAfter). Do NOT pass a linked user-cancellation token;
    /// use the standard Send methods instead, which correctly distinguish timeout from explicit cancellation.
    /// If the token does not carry a timeout, flush operations may block indefinitely.
    /// </remarks>
    ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    /// <summary>
    /// Sends a pipelined request where the caller's cancellation token already carries a timeout
    /// for the write and response phases. Skips per-request timeout CTS/linking on the hot path —
    /// a hot-path optimization for BrokerSender.
    /// </summary>
    /// <remarks>
    /// The caller's token MUST be exclusively a timeout token (e.g., from a CancellationTokenSource
    /// configured with only CancelAfter). Do NOT pass a linked user-cancellation token;
    /// use the standard Send methods instead, which correctly distinguish timeout from explicit cancellation.
    /// If the token does not carry a timeout, flush operations may block indefinitely.
    /// </remarks>
    Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    /// <summary>
    /// Connects to the broker.
    /// </summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
}

internal interface IKafkaPipelinedWriteCompletionConnection
{
    /// <summary>
    /// Sends a pipelined request and returns its response handle after the socket write completes.
    /// </summary>
    /// <remarks>
    /// Response completion runs inline on the receive-dispatch path. Code awaiting the returned
    /// response must remain bounded and non-blocking until its next asynchronous yield.
    /// </remarks>
    ValueTask<PipelinedResponse<TResponse>> SendPipelinedAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;

    ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse;
}

internal interface IPipelinedResponseSource<TResponse> : IValueTaskSource<TResponse>
{
    void Abandon(short token);
}

internal readonly struct PipelinedResponse<TResponse>
{
    private readonly Task<TResponse>? _task;
    private readonly IPipelinedResponseSource<TResponse>? _source;
    private readonly short _token;

    public PipelinedResponse(Task<TResponse> task) => _task = task;

    public PipelinedResponse(IPipelinedResponseSource<TResponse> source, short token)
    {
        _source = source;
        _token = token;
    }

    public bool IsCompleted => _task?.IsCompleted
        ?? _source!.GetStatus(_token) != ValueTaskSourceStatus.Pending;

    public bool IsFaulted => _task?.IsFaulted
        ?? _source!.GetStatus(_token) == ValueTaskSourceStatus.Faulted;

    public bool IsCanceled => _task?.IsCanceled
        ?? _source!.GetStatus(_token) == ValueTaskSourceStatus.Canceled;

    public int Id => _task?.Id ?? 0;

    public TResponse GetResult() => _task is not null
        ? _task.GetAwaiter().GetResult()
        : _source!.GetResult(_token);

    public ValueTask<TResponse> AsValueTask() => _task is not null
        ? new ValueTask<TResponse>(_task)
        : new ValueTask<TResponse>(_source!, _token);

    public Task<TResponse> AsTask() => _task ?? AsValueTask().AsTask();

    public void UnsafeOnCompleted(Action continuation)
    {
        if (_task is not null)
        {
            _task.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(continuation);
            return;
        }

        _source!.OnCompleted(
            static state => ((Action)state!).Invoke(),
            continuation,
            _token,
            ValueTaskSourceOnCompletedFlags.None);
    }

    public void Abandon() => _source?.Abandon(_token);
}
