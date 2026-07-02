using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Consumer;

internal static class AutoOffsetResetStrategy
{
    public static long GetListOffsetsTimestamp(ConsumerOptions options, DateTimeOffset now, TopicPartition? partition = null)
    {
        return options.AutoOffsetReset switch
        {
            AutoOffsetReset.Earliest => -2,
            AutoOffsetReset.Latest => -1,
            AutoOffsetReset.ByDuration => GetByDurationTimestamp(options.AutoOffsetResetDuration, now),
            AutoOffsetReset.None => throw new KafkaException(
                ErrorCode.OffsetOutOfRange,
                GetOffsetResetNoneMessage(partition)),
            _ => throw new InvalidOperationException($"Unknown AutoOffsetReset value: {options.AutoOffsetReset}")
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
    }

    public static void ValidateDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration-based offset reset does not allow negative durations.");
        }
    }
}
