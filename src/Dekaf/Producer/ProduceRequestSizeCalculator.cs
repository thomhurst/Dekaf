using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

internal static class ProduceRequestSizeCalculator
{
    internal const int DefaultMaxRequestSize = 1_048_576;

    internal static int GetSingleBatchRequestBodySize(
        string? transactionalId,
        string topic,
        int encodedBatchSize)
        => GetSingleBatchRequestBodySize(
            GetSingleBatchFixedSize(transactionalId, topic),
            encodedBatchSize);

    internal static int GetSingleBatchRequestBodySize(
        int singleBatchFixedSize,
        int encodedBatchSize)
        => checked(
            singleBatchFixedSize +
            CompactBytesLengthSize(encodedBatchSize) +
            encodedBatchSize);

    internal static int GetMaxEncodedBatchSize(
        int maxRequestSize,
        string? transactionalId,
        string topic)
    {
        var available = maxRequestSize - GetSingleBatchFixedSize(transactionalId, topic);
        if (available <= 1)
            return 0;

        var encodedBatchSize = available - 1;
        while (true)
        {
            var adjustedSize = available - CompactBytesLengthSize(encodedBatchSize);
            if (adjustedSize == encodedBatchSize)
                return encodedBatchSize;

            encodedBatchSize = adjustedSize;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CompactArrayLengthSize(int count)
        => Record.VarUIntSize((uint)checked(count + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CompactBytesLengthSize(int byteCount)
        => Record.VarUIntSize((uint)checked(byteCount + 1));

    internal static int CompactStringSize(string? value)
    {
        if (value is null)
            return 1;

        var byteCount = Encoding.UTF8.GetByteCount(value);
        return checked(CompactBytesLengthSize(byteCount) + byteCount);
    }

    internal static int GetSingleBatchFixedSize(string? transactionalId, string topic)
        => checked(
            CompactStringSize(transactionalId) +
            2 + // Acks
            4 + // TimeoutMs
            CompactArrayLengthSize(1) + // Topics array
            CompactStringSize(topic) +
            CompactArrayLengthSize(1) + // Partitions array
            4 + // Partition index
            1 + // Partition tagged fields
            1 + // Topic tagged fields
            1); // Request tagged fields
}
