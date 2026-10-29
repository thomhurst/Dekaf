namespace Dekaf.Errors;

/// <summary>
/// Cached metadata has no controller route yet. No request reached a broker, so retry
/// metadata discovery until the operation deadline rather than spending broker-error retries.
/// </summary>
internal sealed class ControllerUnavailableException()
    : KafkaException(Protocol.ErrorCode.BrokerNotAvailable, "No controller available.", isRetriable: true);
