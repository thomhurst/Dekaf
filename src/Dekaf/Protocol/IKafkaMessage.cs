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
    /// </summary>
    static abstract bool IsFlexibleVersion(short version);

    /// <summary>
    /// Gets the request header version for the given API version.
    /// </summary>
    static abstract short GetRequestHeaderVersion(short version);

    /// <summary>
    /// Gets the response header version for the given API version.
    /// </summary>
    static abstract short GetResponseHeaderVersion(short version);
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
