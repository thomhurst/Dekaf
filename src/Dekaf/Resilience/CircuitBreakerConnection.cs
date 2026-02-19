using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Resilience;

/// <summary>
/// A connection decorator that records successes and failures to a circuit breaker.
/// Wraps an existing <see cref="IKafkaConnection"/> to track broker health.
/// </summary>
internal sealed class CircuitBreakerConnection : IKafkaConnection
{
    private readonly IKafkaConnection _inner;
    private readonly ICircuitBreaker _circuitBreaker;

    public CircuitBreakerConnection(IKafkaConnection inner, ICircuitBreaker circuitBreaker)
    {
        _inner = inner;
        _circuitBreaker = circuitBreaker;
    }

    public int BrokerId => _inner.BrokerId;
    public string Host => _inner.Host;
    public int Port => _inner.Port;
    public bool IsConnected => _inner.IsConnected;

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
        }
        catch
        {
            _circuitBreaker.RecordFailure();
            throw;
        }
    }

    public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        try
        {
            var response = await _inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken)
                .ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return response;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a broker failure
            throw;
        }
        catch
        {
            _circuitBreaker.RecordFailure();
            throw;
        }
    }

    public async ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        try
        {
            await _inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken)
                .ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _circuitBreaker.RecordFailure();
            throw;
        }
    }

    public async Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
        TRequest request,
        short apiVersion,
        CancellationToken cancellationToken = default)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        try
        {
            var response = await _inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken)
                .ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _circuitBreaker.RecordFailure();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }
}
