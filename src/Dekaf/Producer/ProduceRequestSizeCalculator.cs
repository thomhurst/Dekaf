using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

internal static class ProduceRequestSizeCalculator
{
    internal const int DefaultMaxRequestSize = 1_048_576;
    internal const int TopicIdSize = 16;

    internal static int GetSingleBatchRequestBodySize(
        string? transactionalId,
        string topic,
        int encodedBatchSize)
        => GetSingleBatchRequestBodySize(
            GetSingleBatchFixedSize(transactionalId, topic),
            encodedBatchSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetConservativeSingleBatchRequestBodySize(
        string? transactionalId,
        string topic,
        int encodedBatchSize)
        => GetSingleBatchRequestBodySize(
            GetConservativeSingleBatchFixedSize(transactionalId, topic),
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
        var available = maxRequestSize - GetConservativeSingleBatchFixedSize(transactionalId, topic);
        if (available <= 1)
            return 0;

        // Compact-length jumps leave some budgets without an exact fit.
        // Find the largest batch whose encoded size stays within the available bytes.
        var lowerBound = 0;
        var upperBound = available - 1;
        while (lowerBound < upperBound)
        {
            var candidate = lowerBound + (upperBound - lowerBound + 1) / 2;
            var encodedSizeWithLength = candidate + CompactBytesLengthSize(candidate);
            if (encodedSizeWithLength <= available)
                lowerBound = candidate;
            else
                upperBound = candidate - 1;
        }

        return lowerBound;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CompactArrayLengthSize(int count)
        => Record.VarUIntSize((uint)checked(count + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CompactBytesLengthSize(int byteCount)
        => Record.VarUIntSize((uint)checked(byteCount + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ArrayLengthSize(int count, bool flexible)
        => flexible ? CompactArrayLengthSize(count) : sizeof(int);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BytesLengthSize(int byteCount, bool flexible)
        => flexible ? CompactBytesLengthSize(byteCount) : sizeof(int);

    internal static int CompactStringSize(string? value)
    {
        if (value is null)
            return 1;

        var byteCount = Encoding.UTF8.GetByteCount(value);
        return checked(CompactBytesLengthSize(byteCount) + byteCount);
    }

    internal static int StringSize(string? value, bool flexible)
    {
        if (flexible)
            return CompactStringSize(value);

        return checked(sizeof(short) + (value is null ? 0 : Encoding.UTF8.GetByteCount(value)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetSingleBatchFixedSize(string? transactionalId, string topic)
        => GetSingleBatchFixedSize(transactionalId, CompactStringSize(topic));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetConservativeSingleBatchFixedSize(string? transactionalId, string topic)
        => Math.Max(
            GetSingleBatchFixedSize(
                transactionalId,
                Math.Max(TopicIdSize, CompactStringSize(topic))),
            GetLegacySingleBatchFixedSize(transactionalId, topic));

    private static int GetLegacySingleBatchFixedSize(string? transactionalId, string topic)
        => checked(
            StringSize(transactionalId, flexible: false) +
            2 + // Acks
            4 + // TimeoutMs
            4 + // Topics array
            StringSize(topic, flexible: false) +
            4 + // Partitions array
            4 + // Partition index
            4 - CompactBytesLengthSize(0)); // Worst-case legacy record length prefix delta

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSingleBatchFixedSize(string? transactionalId, int topicFieldSize)
        => checked(
            CompactStringSize(transactionalId) +
            2 + // Acks
            4 + // TimeoutMs
            CompactArrayLengthSize(1) + // Topics array
            topicFieldSize + // Topic name through v12; 16-byte topic ID in v13
            CompactArrayLengthSize(1) + // Partitions array
            4 + // Partition index
            1 + // Partition tagged fields
            1 + // Topic tagged fields
            1); // Request tagged fields
}
