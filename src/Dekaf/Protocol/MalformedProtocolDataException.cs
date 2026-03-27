namespace Dekaf.Protocol;

/// <summary>
/// Thrown when a protocol read operation encounters malformed data, such as
/// an invalid variable-length integer encoding. This is distinct from
/// <see cref="InsufficientDataException"/> which indicates truncated but
/// structurally valid data.
/// </summary>
internal sealed class MalformedProtocolDataException(string message) : Exception(message);
