using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Producer;

internal sealed class TestKafkaConnection : IKafkaConnection, IKafkaPipelinedWriteCompletionConnection
{
    public int BrokerId { get; init; } = 1;
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 9092;
    public bool IsConnected { get; set; } = true;

    public int SendPipelinedCalls;
    public int SendPipelinedWithCallerTimeoutCalls;
    public int SendPipelinedAfterWriteCalls;
    public int SendPipelinedWithCallerTimeoutAfterWriteCalls;
    public int SendFireAndForgetWithCallerTimeoutCalls;

    public Func<ValueTask<Task<ProduceResponse>>>? SendProducePipelinedAfterWrite { get; set; }
    public Func<ValueTask>? SendProduceFireAndForgetWithCallerTimeout { get; set; }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => throw new NotSupportedException();

    public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

    public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedCalls);
        throw new NotSupportedException();
    }

    public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendFireAndForgetWithCallerTimeoutCalls);
        return SendProduceFireAndForgetWithCallerTimeout?.Invoke() ?? ValueTask.CompletedTask;
    }

    public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedWithCallerTimeoutCalls);
        throw new NotSupportedException();
    }

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async ValueTask<Task<TResponse>> SendPipelinedAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedAfterWriteCalls);

        if (SendProducePipelinedAfterWrite is null)
            throw new NotSupportedException();

        var responseTask = await SendProducePipelinedAfterWrite().ConfigureAwait(false);
        return CastResponseTask<TResponse>(responseTask);
    }

    public async ValueTask<Task<TResponse>> SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        Interlocked.Increment(ref SendPipelinedWithCallerTimeoutAfterWriteCalls);
        throw new NotSupportedException();
    }

    private static async Task<TResponse> CastResponseTask<TResponse>(Task<ProduceResponse> responseTask)
        where TResponse : IKafkaResponse
        => (TResponse)(IKafkaResponse)await responseTask.ConfigureAwait(false);
}
