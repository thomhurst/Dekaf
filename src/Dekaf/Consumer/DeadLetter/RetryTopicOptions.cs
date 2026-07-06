using System.Globalization;

namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Configuration for tiered retry topics used before dead-lettering.
/// </summary>
public sealed class RetryTopicOptions
{
    /// <summary>
    /// Ordered retry delays. Attempt 1 uses the first delay, attempt 2 uses the second delay, and so on.
    /// </summary>
    public IReadOnlyList<TimeSpan> Delays { get; init; } = [];

    /// <summary>
    /// Suffix format appended to the source topic. The <c>{0}</c> placeholder receives a compact delay label.
    /// Default: <c>-retry-{0}</c>, e.g. <c>orders-retry-5s</c>.
    /// </summary>
    public string TopicSuffixFormat { get; init; } = "-retry-{0}";

    /// <summary>
    /// Whether at least one retry tier is configured.
    /// </summary>
    public bool IsEnabled => Delays.Count > 0;

    /// <summary>
    /// Gets the retry topic for a specific delay tier.
    /// </summary>
    public string GetRetryTopic(string sourceTopic, TimeSpan delay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTopic);
        ValidateDelay(delay);
        ValidateTopicSuffixFormat(TopicSuffixFormat);
        return sourceTopic + string.Format(
            CultureInfo.InvariantCulture,
            TopicSuffixFormat,
            FormatDelayLabel(delay));
    }

    /// <summary>
    /// Gets all retry topic names for the supplied source topic.
    /// </summary>
    public IEnumerable<string> GetRetryTopics(string sourceTopic)
    {
        foreach (var delay in Delays)
        {
            yield return GetRetryTopic(sourceTopic, delay);
        }
    }

    /// <summary>
    /// Gets the retry topic and delay for a 1-based failure count.
    /// </summary>
    internal bool TryGetRetryTopic(string sourceTopic, int failureCount, out string retryTopic, out TimeSpan delay)
    {
        if (failureCount <= 0 || failureCount > Delays.Count)
        {
            retryTopic = string.Empty;
            delay = default;
            return false;
        }

        delay = Delays[failureCount - 1];
        retryTopic = GetRetryTopic(sourceTopic, delay);
        return true;
    }

    internal static void ValidateDelay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Retry topic delays must be greater than zero.");
        if (delay.Ticks < TimeSpan.TicksPerMillisecond)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Retry topic delays must be at least one millisecond.");
    }

    internal static void ValidateTopicSuffixFormat(string suffixFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffixFormat);
        if (!suffixFormat.Contains("{0}", StringComparison.Ordinal))
            throw new ArgumentException("Retry topic suffix format must contain the {0} delay placeholder.", nameof(suffixFormat));
    }

    private static string FormatDelayLabel(TimeSpan delay)
    {
        if (delay.Ticks % TimeSpan.TicksPerDay == 0)
            return $"{delay.Ticks / TimeSpan.TicksPerDay}d";
        if (delay.Ticks % TimeSpan.TicksPerHour == 0)
            return $"{delay.Ticks / TimeSpan.TicksPerHour}h";
        if (delay.Ticks % TimeSpan.TicksPerMinute == 0)
            return $"{delay.Ticks / TimeSpan.TicksPerMinute}m";
        if (delay.Ticks % TimeSpan.TicksPerSecond == 0)
            return $"{delay.Ticks / TimeSpan.TicksPerSecond}s";

        return $"{delay.Ticks / TimeSpan.TicksPerMillisecond}ms";
    }
}
