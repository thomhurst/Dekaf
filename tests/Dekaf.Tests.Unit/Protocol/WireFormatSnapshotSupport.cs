using System.Reflection;
using System.Text;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

internal static class WireFormatSnapshotSupport
{
    internal static Type? GetRequestInterface(Type type) => type.GetInterfaces()
        .SingleOrDefault(static implemented =>
            implemented.IsGenericType &&
            implemented.GetGenericTypeDefinition() == typeof(IKafkaRequest<>));

    internal static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)(type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException($"{type.FullName} has no static {propertyName} property."));

    internal static class HexDump
    {
        private const int BytesPerLine = 16;

        internal static string Format(ReadOnlySpan<byte> bytes)
        {
            var builder = new StringBuilder();
            for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
            {
                var line = bytes.Slice(offset, Math.Min(BytesPerLine, bytes.Length - offset));
                builder.Append(offset.ToString("X4")).Append(": ");

                for (var index = 0; index < BytesPerLine; index++)
                {
                    builder.Append(index < line.Length ? line[index].ToString("X2") : "  ");
                    builder.Append(index == 7 ? "  " : " ");
                }

                builder.Append('|');
                foreach (var value in line)
                {
                    builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
                }

                builder.AppendLine("|");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
