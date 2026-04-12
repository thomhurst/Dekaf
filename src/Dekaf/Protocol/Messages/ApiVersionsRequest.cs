namespace Dekaf.Protocol.Messages;

/// <summary>
/// ApiVersions request (API key 18).
/// Used to discover the API versions supported by a broker.
/// </summary>
public sealed class ApiVersionsRequest : IKafkaRequest<ApiVersionsResponse>
{
    public static ApiKey ApiKey => ApiKey.ApiVersions;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// Client software name (v3+).
    /// </summary>
    public string? ClientSoftwareName { get; init; }

    /// <summary>
    /// Client software version (v3+).
    /// </summary>
    public string? ClientSoftwareVersion { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    // ApiVersions is the bootstrap API — the broker always sends the response
    // with header v0 (no tagged fields) regardless of the API version.
    public static short GetResponseHeaderVersion(short version) => 0;

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
