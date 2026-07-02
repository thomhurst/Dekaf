namespace Dekaf.Protocol.Messages;

/// <summary>
/// PushTelemetry request (API key 72).
/// Uploads an OpenTelemetry MetricsData payload for a broker subscription.
/// </summary>
public sealed class PushTelemetryRequest : IKafkaRequest<PushTelemetryResponse>
{
    public static ApiKey ApiKey => ApiKey.PushTelemetry;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The client instance ID assigned by GetTelemetrySubscriptions.
    /// </summary>
    public Guid ClientInstanceId { get; init; }

    /// <summary>
    /// The subscription ID assigned by GetTelemetrySubscriptions.
    /// </summary>
    public int SubscriptionId { get; init; }

    /// <summary>
    /// True when this is the final telemetry push for the client lifecycle.
    /// </summary>
    public bool Terminating { get; init; }

    /// <summary>
    /// Compression type ID used for the metrics payload.
    /// </summary>
    public sbyte CompressionType { get; init; }

    /// <summary>
    /// OpenTelemetry MetricsData payload bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Metrics { get; init; } = ReadOnlyMemory<byte>.Empty;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteUuid(ClientInstanceId);
        writer.WriteInt32(SubscriptionId);
        writer.WriteBoolean(Terminating);
        writer.WriteInt8(CompressionType);
        writer.WriteCompactBytes(Metrics.Span);
        writer.WriteEmptyTaggedFields();
    }
}
