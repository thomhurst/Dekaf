namespace Dekaf.Protocol;

/// <summary>
/// Thrown when a protocol read operation encounters insufficient data in the buffer.
/// This commonly occurs when parsing partial record batches at the end of a fetch response.
/// </summary>
internal sealed class InsufficientDataException : InvalidOperationException
{
    public InsufficientDataException()
        : base("Insufficient data in buffer")
    {
    }
}
