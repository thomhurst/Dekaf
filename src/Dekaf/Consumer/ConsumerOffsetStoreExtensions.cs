namespace Dekaf.Consumer;

/// <summary>
/// Allocation-free batch offset storage for Kafka consumers.
/// </summary>
/// <remarks>
/// Every overload validates the full input before staging its first offset. Duplicate
/// topic-partitions use the last entry. A non-negative leader epoch stores or replaces the staged
/// epoch; a negative epoch clears it. Publication is ordered but is not globally atomic relative
/// to concurrent commit snapshots. These methods perform no broker I/O.
/// </remarks>
public static class ConsumerOffsetStoreExtensions
{
    /// <summary>Stores next offsets from an array for the automatic commit loop.</summary>
    public static void StoreOffsets<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartitionOffset[] offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        StoreOffsets(consumer, offsets.AsSpan());
    }

    /// <summary>Stores next offsets from a span for the automatic commit loop.</summary>
    public static void StoreOffsets<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        ReadOnlySpan<TopicPartitionOffset> offsets)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        if (consumer is KafkaConsumer<TKey, TValue> kafkaConsumer)
        {
            kafkaConsumer.StoreOffsets(offsets);
            return;
        }

        if (consumer is IConsumerBatchOffsetStore batchStore)
        {
            batchStore.StoreOffsets(offsets);
            return;
        }

        Validate(offsets);
        for (var index = 0; index < offsets.Length; index++)
            consumer.StoreOffset(offsets[index]);
    }

    /// <summary>Stores next offsets from an indexed collection without boxing value-type collections.</summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <typeparam name="TOffsets">The indexed collection type.</typeparam>
    public static void StoreOffsets<TKey, TValue, TOffsets>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TOffsets offsets)
        where TOffsets : IReadOnlyList<TopicPartitionOffset>
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (offsets is null)
            throw new ArgumentNullException(nameof(offsets));

        if (consumer is KafkaConsumer<TKey, TValue> kafkaConsumer)
        {
            kafkaConsumer.StoreOffsets(offsets);
            return;
        }

        if (consumer is IConsumerBatchOffsetStore batchStore)
        {
            batchStore.StoreOffsets(offsets);
            return;
        }

        Validate(offsets);
        var count = offsets.Count;
        for (var index = 0; index < count; index++)
            consumer.StoreOffset(offsets[index]);
    }

    private static void Validate(ReadOnlySpan<TopicPartitionOffset> offsets)
    {
        for (var index = 0; index < offsets.Length; index++)
            TopicPartitionOffsetValidator.Validate(offsets[index], nameof(offsets));
    }

    private static void Validate<TOffsets>(TOffsets offsets)
        where TOffsets : IReadOnlyList<TopicPartitionOffset>
    {
        var count = offsets.Count;
        for (var index = 0; index < count; index++)
            TopicPartitionOffsetValidator.Validate(offsets[index], nameof(offsets));
    }
}
