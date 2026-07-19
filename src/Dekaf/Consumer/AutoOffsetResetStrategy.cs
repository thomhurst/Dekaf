using Dekaf.Protocol;

namespace Dekaf.Consumer;

internal static class AutoOffsetResetStrategy
{
    public static long GetListOffsetsTimestamp(
        ConsumerOptions options,
        DateTimeOffset now,
        TopicPartition? partition = null,
        bool isNewPartition = false)
    {
        var policy = isNewPartition && options.AutoOffsetResetNewPartitions is { } newPartitionPolicy
            ? newPartitionPolicy
            : options.AutoOffsetReset;
        var duration = isNewPartition && options.AutoOffsetResetNewPartitions is not null
            ? options.AutoOffsetResetNewPartitionsDuration
            : options.AutoOffsetResetDuration;

        return policy switch
        {
            AutoOffsetReset.Earliest => -2,
            AutoOffsetReset.Latest => -1,
            AutoOffsetReset.ByDuration => GetByDurationTimestamp(duration, now),
            AutoOffsetReset.None => throw new KafkaException(
                ErrorCode.OffsetOutOfRange,
                GetOffsetResetNoneMessage(partition)),
            _ => throw new InvalidOperationException($"Unknown AutoOffsetReset value: {policy}")
        };
    }

    private static string GetOffsetResetNoneMessage(TopicPartition? partition) =>
        partition is { } tp
            ? $"No committed offset for {tp.Topic}-{tp.Partition} and auto.offset.reset is 'none'"
            : "No committed offset and auto.offset.reset is 'none'";

    public static long GetByDurationTimestamp(TimeSpan? duration, DateTimeOffset now)
    {
        if (duration is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ConsumerOptions.AutoOffsetResetDuration)} must be set when {nameof(ConsumerOptions.AutoOffsetReset)} is {nameof(AutoOffsetReset.ByDuration)}.");
        }

        ValidateDuration(duration.Value);
        return now.Subtract(duration.Value).ToUnixTimeMilliseconds();
    }

    public static void ValidateOptions(ConsumerOptions options)
    {
        if (options.AutoOffsetReset == AutoOffsetReset.ByDuration)
        {
            if (options.AutoOffsetResetDuration is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ConsumerOptions.AutoOffsetResetDuration)} must be set when {nameof(ConsumerOptions.AutoOffsetReset)} is {nameof(AutoOffsetReset.ByDuration)}.");
            }

            ValidateDuration(options.AutoOffsetResetDuration.Value);
        }

        if (options.AutoOffsetResetNewPartitions == AutoOffsetReset.None)
        {
            throw new InvalidOperationException(
                $"{nameof(ConsumerOptions.AutoOffsetResetNewPartitions)} does not support {nameof(AutoOffsetReset.None)}. " +
                "Use Earliest, Latest, or ByDuration.");
        }

        if (options.AutoOffsetResetNewPartitions == AutoOffsetReset.ByDuration)
        {
            if (options.AutoOffsetResetNewPartitionsDuration is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ConsumerOptions.AutoOffsetResetNewPartitionsDuration)} must be set when " +
                    $"{nameof(ConsumerOptions.AutoOffsetResetNewPartitions)} is {nameof(AutoOffsetReset.ByDuration)}.");
            }

            ValidateDuration(options.AutoOffsetResetNewPartitionsDuration.Value);
        }

        if (options.AutoOffsetResetNewPartitions is not null && string.IsNullOrWhiteSpace(options.GroupId))
        {
            throw new InvalidOperationException(
                $"{nameof(ConsumerOptions.AutoOffsetResetNewPartitions)} requires a consumer group subscription.");
        }
    }

    public static void ValidateDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration-based offset reset does not allow negative durations.");
        }
    }
}
