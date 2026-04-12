namespace Dekaf.Protocol;

/// <summary>
/// Base interface for all Kafka protocol messages.
/// </summary>
public interface IKafkaMessage
{
    /// <summary>
    /// The API key for this message type.
    /// </summary>
    static abstract ApiKey ApiKey { get; }

    /// <summary>
    /// The lowest supported API version.
    /// </summary>
    static abstract short LowestSupportedVersion { get; }

    /// <summary>
    /// The highest supported API version.
    /// </summary>
    static abstract short HighestSupportedVersion { get; }
}

/// <summary>
/// Interface for Kafka request messages.
/// </summary>
public interface IKafkaRequest<TResponse> : IKafkaMessage
    where TResponse : IKafkaResponse
{
    /// <summary>
    /// Writes the request body to the protocol writer.
    /// </summary>
    void Write(ref KafkaProtocolWriter writer, short version);

    /// <summary>
    /// Returns true if this API version uses flexible encoding.
    /// With Kafka 4.0+ all supported versions are flexible.
    /// </summary>
    static virtual bool IsFlexibleVersion(short version) => true;

    /// <summary>
    /// Gets the request header version for the given API version.
    /// With Kafka 4.0+ all requests use header v2.
    /// </summary>
    static virtual short GetRequestHeaderVersion(short version) => 2;

    /// <summary>
    /// Gets the response header version for the given API version.
    /// With Kafka 4.0+ all responses use header v1.
    /// </summary>
    static virtual short GetResponseHeaderVersion(short version) => 1;
}

/// <summary>
/// Interface for Kafka response messages.
/// </summary>
public interface IKafkaResponse : IKafkaMessage
{
    /// <summary>
    /// Reads the response body from the protocol reader.
    /// </summary>
    static abstract IKafkaResponse Read(ref KafkaProtocolReader reader, short version);
}
