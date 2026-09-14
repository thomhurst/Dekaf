using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Networking;

internal static class ClientTelemetryRequestExtensions
{
    internal static ValueTask<TResponse> SendWithClientTelemetryAsync<TRequest, TResponse>(
        this IKafkaConnection connection, TRequest request, short apiVersion,
        ClientTelemetryMetricCollector? collector, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => connection is KafkaConnection kafkaConnection && collector is not null
            ? kafkaConnection.SendWithTelemetryAsync<TRequest, TResponse>(request, apiVersion, collector, cancellationToken)
            : connection.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

    internal static ValueTask<TResponse> SendWithClientTelemetryAsync<TRequest, TResponse>(
        this IKafkaRequestWriteObserverConnection connection, TRequest request, short apiVersion,
        Action requestWriteStarted, ClientTelemetryMetricCollector? collector, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => connection is KafkaConnection kafkaConnection && collector is not null
            ? kafkaConnection.SendWithTelemetryAsync<TRequest, TResponse>(request, apiVersion, collector, requestWriteStarted, cancellationToken)
            : connection.SendWithWriteObservationAsync<TRequest, TResponse>(request, apiVersion, requestWriteStarted, cancellationToken);

    internal static ValueTask<PipelinedResponse<TResponse>> SendPipelinedWithClientTelemetryAsync<TRequest, TResponse>(
        this IKafkaRequestWriteObserverConnection connection, TRequest request, short apiVersion,
        Action requestWriteStarted, ClientTelemetryMetricCollector? collector, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
        => connection is KafkaConnection kafkaConnection && collector is not null
            ? kafkaConnection.SendPipelinedWithTelemetryAfterWriteAsync<TRequest, TResponse>(request, apiVersion, collector, requestWriteStarted, cancellationToken)
            : connection.SendPipelinedWithWriteObservationAfterWriteAsync<TRequest, TResponse>(request, apiVersion, requestWriteStarted, cancellationToken);
}
