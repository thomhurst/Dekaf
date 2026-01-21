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
    /// Connects to the broker.
    /// </summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
}
