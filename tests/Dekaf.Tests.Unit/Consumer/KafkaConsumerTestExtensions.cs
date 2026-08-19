using System.Reflection;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

internal static class KafkaConsumerTestExtensions
{
    public static void StageExplicitCommitOffsetsForTesting<TKey, TValue>(
        this KafkaConsumer<TKey, TValue> consumer)
    {
        var method = typeof(KafkaConsumer<TKey, TValue>).GetMethod(
            "StageExplicitCommitOffsets",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("StageExplicitCommitOffsets method not found.");

        method.Invoke(consumer, null);
    }
}
