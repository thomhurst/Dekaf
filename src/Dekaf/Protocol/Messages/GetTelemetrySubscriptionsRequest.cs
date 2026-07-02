namespace Dekaf.Protocol.Messages;

/// <summary>
/// GetTelemetrySubscriptions request (API key 71).
/// Negotiates client telemetry subscription parameters with the broker.
/// </summary>
public sealed class GetTelemetrySubscriptionsRequest : IKafkaRequest<GetTelemetrySubscriptionsResponse>
{
    public static ApiKey ApiKey => ApiKey.GetTelemetrySubscriptions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The assigned client instance ID. Use Guid.Empty on the first request.
    /// </summary>
    public Guid ClientInstanceId { get; init; } = Guid.Empty;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteUuid(ClientInstanceId);
        writer.WriteEmptyTaggedFields();
    }
}
