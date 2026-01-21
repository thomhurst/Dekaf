namespace Dekaf.Protocol;

/// <summary>
/// Kafka response header (v0-v1).
/// </summary>
public sealed class ResponseHeader
{
    public required int CorrelationId { get; init; }

    /// <summary>
    /// Header version:
    /// v0: correlation ID only
    /// v1: + tagged fields (flexible versions)
    /// </summary>
    public short HeaderVersion { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(CorrelationId);

        if (HeaderVersion >= 1)
        {
            writer.WriteEmptyTaggedFields();
        }
    }

    public static ResponseHeader Read(ref KafkaProtocolReader reader, short headerVersion)
    {
        var correlationId = reader.ReadInt32();

        if (headerVersion >= 1)
        {
            reader.SkipTaggedFields();
        }

        return new ResponseHeader
        {
            CorrelationId = correlationId,
            HeaderVersion = headerVersion
        };
    }
}
