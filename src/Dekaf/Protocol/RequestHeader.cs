using System.Buffers;

namespace Dekaf.Protocol;

/// <summary>
/// Kafka request header (v0-v2).
/// </summary>
public sealed class RequestHeader
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
            if (HeaderVersion >= 2)
            {
                writer.WriteCompactString(ClientId);
                writer.WriteEmptyTaggedFields();
            }
            else
            {
                writer.WriteString(ClientId);
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
            clientId = headerVersion >= 2
                ? reader.ReadCompactString()
                : reader.ReadString();

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
