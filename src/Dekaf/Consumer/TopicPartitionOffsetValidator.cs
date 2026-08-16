using System.Runtime.CompilerServices;

namespace Dekaf.Consumer;

internal static class TopicPartitionOffsetValidator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Validate(in TopicPartitionOffset offset, string paramName)
    {
        if (string.IsNullOrEmpty(offset.Topic))
            throw new ArgumentException("Topic must be specified.", paramName);

        if (offset.Partition < 0)
            throw new ArgumentOutOfRangeException(paramName, offset.Partition, "Partition must be non-negative.");

        if (offset.Offset < 0)
            throw new ArgumentOutOfRangeException(paramName, offset.Offset, "Offset must be non-negative.");
    }
}
