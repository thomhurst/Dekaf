namespace Dekaf.Protocol.Messages;

/// <summary>
/// ApiVersions request (API key 18).
/// Used to discover the API versions supported by a broker.
/// </summary>
public sealed class ApiVersionsRequest : IKafkaRequest<ApiVersionsResponse>
{
    public static ApiKey ApiKey => ApiKey.ApiVersions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Client software name (v3+).
    /// </summary>
    public string? ClientSoftwareName { get; init; }

    /// <summary>
    /// Client software version (v3+).
    /// </summary>
    public string? ClientSoftwareVersion { get; init; }

    // ApiVersions is the bootstrap API — the broker always sends the response
    // with header v0 (no tagged fields) regardless of the API version.
    public static bool IsFlexibleVersion(short version) => version >= 3;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => 0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (!IsFlexibleVersion(version))
            return;

        if (string.IsNullOrEmpty(ClientSoftwareName))
            throw new InvalidOperationException("Client software name must not be empty for ApiVersions v3+.");
        if (string.IsNullOrEmpty(ClientSoftwareVersion))
            throw new InvalidOperationException("Client software version must not be empty for ApiVersions v3+.");

        writer.WriteCompactString(ClientSoftwareName);
        writer.WriteCompactString(ClientSoftwareVersion);
        writer.WriteEmptyTaggedFields();
    }
}
