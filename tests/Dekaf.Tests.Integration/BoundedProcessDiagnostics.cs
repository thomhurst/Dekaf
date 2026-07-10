using System.Text;

namespace Dekaf.Tests.Integration;

internal sealed class BoundedProcessDiagnostics(int maximumCharacters)
{
    private readonly StringBuilder _buffer = new();

    public void Append(string stream, string? message)
    {
        if (message is null)
            return;

        lock (_buffer)
        {
            _buffer.Append('[').Append(stream).Append("] ").AppendLine(message);
            if (_buffer.Length > maximumCharacters)
                _buffer.Remove(0, _buffer.Length - maximumCharacters);
        }
    }

    public override string ToString()
    {
        lock (_buffer)
            return _buffer.ToString();
    }
}
