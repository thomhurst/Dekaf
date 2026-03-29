namespace Dekaf.Protocol;

/// <summary>
/// Kafka request header (v0-v2).
/// Readonly struct to eliminate per-request heap allocation.
/// RequestHeader is constructed, written to the wire, and immediately discarded —
/// it is never stored in collections or passed by reference, making it ideal for
/// stack allocation via struct.
/// </summary>
public readonly struct RequestHeader
{
    public required ApiKey ApiKey { get; init; }
    public required short ApiVersion { get; init; }
    public required int CorrelationId { get; init; }
    public string? ClientId { get; init; }

    /// <summary>
    /// Header version based on API version flexibility.
    /// v0: API key, version, correlation ID
    /// v1: + client ID
    /// v2: + tagged fields (flexible versions)
    /// </summary>
    public short HeaderVersion { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ApiKey);
        writer.WriteInt16(ApiVersion);
        writer.WriteInt32(CorrelationId);

        if (HeaderVersion >= 1)
        {
            // ClientId is ALWAYS NULLABLE_STRING (flexibleVersions: "none" in schema)
            // even in header v2, it uses INT16 length prefix for backward compatibility
            writer.WriteString(ClientId);

            if (HeaderVersion >= 2)
            {
                writer.WriteEmptyTaggedFields();
            }
        }
    }

    public static RequestHeader Read(ref KafkaProtocolReader reader, short headerVersion)
    {
        var apiKey = (ApiKey)reader.ReadInt16();
        var apiVersion = reader.ReadInt16();
        var correlationId = reader.ReadInt32();
        string? clientId = null;

        if (headerVersion >= 1)
        {
            // ClientId is ALWAYS NULLABLE_STRING (flexibleVersions: "none" in schema)
            clientId = reader.ReadString();

            if (headerVersion >= 2)
            {
                reader.SkipTaggedFields();
            }
        }

        return new RequestHeader
        {
            ApiKey = apiKey,
            ApiVersion = apiVersion,
            CorrelationId = correlationId,
            ClientId = clientId,
            HeaderVersion = headerVersion
        };
    }
}
