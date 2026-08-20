using System.Collections.Concurrent;
using Dekaf.Internal;

namespace Dekaf.Protocol.Messages;

internal static class TopicNameInternCache
{
    private const int MaxCachedTopicNames = 256;
    private const int MaxCachedTopicNameBytes = 512;
    private static readonly ConcurrentDictionary<string, string> s_cache = new();
    private static readonly Utf8StringInternCache s_utf8Cache = new(
        MaxCachedTopicNames,
        MaxCachedTopicNameBytes,
        Intern,
        cacheLastEntry: true);
    private static int s_count;

    public static string Intern(string topic)
    {
        if (s_cache.TryGetValue(topic, out var cached))
            return cached;

        if (Volatile.Read(ref s_count) < MaxCachedTopicNames)
        {
            if (s_cache.TryAdd(topic, topic))
            {
                Interlocked.Increment(ref s_count);
            }
            else if (s_cache.TryGetValue(topic, out cached))
            {
                return cached;
            }
        }

        return topic;
    }

    public static string? Read(ref KafkaProtocolReader reader) =>
        reader.ReadInternedString(s_utf8Cache);

    public static string? ReadCompact(ref KafkaProtocolReader reader) =>
        reader.ReadCompactInternedString(s_utf8Cache);
}
