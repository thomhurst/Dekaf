namespace Dekaf.Protocol.Messages;

/// <summary>
/// ApiVersions request (API key 18).
/// Used to discover the API versions supported by a broker.
/// </summary>
public sealed class ApiVersionsRequest : IKafkaRequest<ApiVersionsResponse>
{
    public static ApiKey ApiKey => ApiKey.ApiVersions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// Client software name (v3+).
    /// </summary>
    public string? ClientSoftwareName { get; init; }

    /// <summary>
    /// Client software version (v3+).
    /// </summary>
    public string? ClientSoftwareVersion { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 3;
    public static short GetRequestHeaderVersion(short version) => version >= 3 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 3 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 3)
        {
            writer.WriteCompactString(ClientSoftwareName ?? string.Empty);
            writer.WriteCompactString(ClientSoftwareVersion ?? string.Empty);
            writer.WriteEmptyTaggedFields();
        }
    }
}
