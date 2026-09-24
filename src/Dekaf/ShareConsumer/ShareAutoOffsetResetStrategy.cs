using System.Xml;
using Dekaf.Consumer;

namespace Dekaf.ShareConsumer;

internal static class ShareAutoOffsetResetStrategy
{
    internal static string? GetConfigValue(ShareConsumerOptions options)
    {
        if (options.AutoOffsetReset != AutoOffsetReset.ByDuration && options.AutoOffsetResetDuration is not null)
            throw new ArgumentException("AutoOffsetResetDuration requires AutoOffsetReset.ByDuration.", nameof(options));

        switch (options.AutoOffsetReset)
        {
            case null:
                return null;
            case AutoOffsetReset.Earliest:
                return "earliest";
            case AutoOffsetReset.Latest:
                return "latest";
            case AutoOffsetReset.ByDuration:
                if (options.AutoOffsetResetDuration is not { } duration)
                    throw new ArgumentException("AutoOffsetResetDuration must be set for AutoOffsetReset.ByDuration.", nameof(options));
                AutoOffsetResetStrategy.ValidateDuration(duration);
                return "by_duration:" + XmlConvert.ToString(duration);
            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.AutoOffsetReset,
                    "Share groups support Earliest, Latest, or ByDuration.");
        }
    }
}
