using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Dekaf.Consumer.DeadLetter;

internal static class DeadLetterHeaderFormatting
{
    public static string FormatInt(int value)
    {
        Span<byte> buffer = stackalloc byte[11];
        if (Utf8Formatter.TryFormat(value, buffer, out var bytesWritten))
            return Encoding.UTF8.GetString(buffer[..bytesWritten]);

        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string FormatLong(long value)
    {
        Span<byte> buffer = stackalloc byte[20];
        if (Utf8Formatter.TryFormat(value, buffer, out var bytesWritten))
            return Encoding.UTF8.GetString(buffer[..bytesWritten]);

        return value.ToString(CultureInfo.InvariantCulture);
    }
}
