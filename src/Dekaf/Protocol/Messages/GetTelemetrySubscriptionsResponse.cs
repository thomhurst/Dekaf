namespace Dekaf.Protocol.Messages;

/// <summary>
/// GetTelemetrySubscriptions response (API key 71).
/// Contains the broker-selected telemetry subscription and upload constraints.
/// </summary>
public sealed class GetTelemetrySubscriptionsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.GetTelemetrySubscriptions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The assigned client instance ID, or Guid.Empty when unchanged.
    /// </summary>
    public Guid ClientInstanceId { get; init; }

    /// <summary>
    /// The broker-assigned subscription ID for subsequent PushTelemetry requests.
    /// </summary>
    public int SubscriptionId { get; init; }

    /// <summary>
    /// Compression type IDs accepted for telemetry payloads.
    /// </summary>
    public IReadOnlyList<sbyte> AcceptedCompressionTypes { get; init; } = [];

    /// <summary>
    /// Minimum interval in milliseconds between PushTelemetry requests.
    /// </summary>
    public int PushIntervalMs { get; init; }

    /// <summary>
    /// Maximum telemetry payload size in bytes.
    /// </summary>
    public int TelemetryMaxBytes { get; init; }

    /// <summary>
    /// True when delta temporality is requested; false for cumulative temporality.
    /// </summary>
    public bool DeltaTemporality { get; init; }

    /// <summary>
    /// Metric name prefixes requested by the broker.
    /// </summary>
    public IReadOnlyList<string> RequestedMetrics { get; init; } = [];

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var clientInstanceId = reader.ReadUuid();
        var subscriptionId = reader.ReadInt32();
        var acceptedCompressionTypes = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt8());
        var pushIntervalMs = reader.ReadInt32();
        var telemetryMaxBytes = reader.ReadInt32();
        var deltaTemporality = reader.ReadBoolean();
        var requestedMetrics = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadCompactNonNullableString());

        reader.SkipTaggedFields();

        return new GetTelemetrySubscriptionsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ClientInstanceId = clientInstanceId,
            SubscriptionId = subscriptionId,
            AcceptedCompressionTypes = acceptedCompressionTypes,
            PushIntervalMs = pushIntervalMs,
            TelemetryMaxBytes = telemetryMaxBytes,
            DeltaTemporality = deltaTemporality,
            RequestedMetrics = requestedMetrics
        };
    }
}
