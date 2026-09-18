using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// The single-table item layout: key values, attribute names, and the mapping between
/// <see cref="OutboxMessage"/> and a DynamoDB item. The store and the writer share it so the
/// two sides of the table cannot drift apart.
/// </summary>
/// <remarks>
/// <para>Four item kinds live under one key prefix:</para>
/// <list type="bullet">
/// <item>Messages: one partition per bucket, sorted by a zero-padded sequence number, so a
/// strongly consistent query returns a bucket in enqueue order.</item>
/// <item>Sequences: one counter item per bucket, each in its own partition so the counters do
/// not share one partition's write capacity.</item>
/// <item>Leases and relay heartbeats: one coordination partition, so a single strongly
/// consistent query reads every lease and every heartbeat as one view.</item>
/// </list>
/// </remarks>
internal sealed class DynamoDbOutboxSchema
{
    public const string MessageId = "MessageId";
    public const string Topic = "Topic";
    public const string Key = "Key";
    public const string Value = "Value";
    public const string Headers = "Headers";
    public const string Partition = "Partition";
    public const string CreatedAtUtc = "CreatedAtUtc";
    public const string BucketCount = "BucketCount";
    public const string Sequence = "Sequence";
    public const string Owner = "Owner";
    public const string ExpiresAtUtc = "ExpiresAtUtc";
    public const string LastSeenUtc = "LastSeenUtc";

    public const string SequenceSortKey = "SEQUENCE";
    public const string LeaseSortKeyPrefix = "LEASE#";
    public const string RelaySortKeyPrefix = "RELAY#";

    private static readonly string[] ItemAttributeNames =
    [
        MessageId, Topic, Key, Value, Headers, Partition, CreatedAtUtc, BucketCount, Sequence, Owner,
        ExpiresAtUtc, LastSeenUtc
    ];

    private readonly string _messagePartitionPrefix;
    private readonly string _sequencePartitionPrefix;

    public DynamoDbOutboxSchema(DynamoDbOutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ThrowIfItemAttribute(options.PartitionKeyAttributeName);
        ThrowIfItemAttribute(options.SortKeyAttributeName);

        TableName = options.TableName;
        PartitionKeyName = options.PartitionKeyAttributeName;
        SortKeyName = options.SortKeyAttributeName;
        CoordinationPartition = options.KeyPrefix + "#COORDINATION";
        _messagePartitionPrefix = options.KeyPrefix + "#MESSAGES#";
        _sequencePartitionPrefix = options.KeyPrefix + "#SEQUENCE#";
    }

    public string TableName { get; }

    public string PartitionKeyName { get; }

    public string SortKeyName { get; }

    public string CoordinationPartition { get; }

    public string MessagePartition(int bucket) =>
        _messagePartitionPrefix + bucket.ToString(CultureInfo.InvariantCulture);

    /// <summary>Fixed width, so the string order of sort keys is the numeric order.</summary>
    public static string MessageSortKey(long sequence) => sequence.ToString("D19", CultureInfo.InvariantCulture);

    public static string LeaseSortKey(int bucket) =>
        LeaseSortKeyPrefix + bucket.ToString("D10", CultureInfo.InvariantCulture);

    public static string RelaySortKey(string relayId) => RelaySortKeyPrefix + relayId;

    public Dictionary<string, AttributeValue> MessageKey(int bucket, long sequence) =>
        ItemKey(MessagePartition(bucket), MessageSortKey(sequence));

    public Dictionary<string, AttributeValue> SequenceKey(int bucket) =>
        ItemKey(_sequencePartitionPrefix + bucket.ToString(CultureInfo.InvariantCulture), SequenceSortKey);

    public Dictionary<string, AttributeValue> LeaseKey(int bucket) =>
        ItemKey(CoordinationPartition, LeaseSortKey(bucket));

    public Dictionary<string, AttributeValue> RelayKey(string relayId) =>
        ItemKey(CoordinationPartition, RelaySortKey(relayId));

    public Dictionary<string, AttributeValue> ItemKey(string partitionKey, string sortKey) => new(2)
    {
        [PartitionKeyName] = new AttributeValue { S = partitionKey },
        [SortKeyName] = new AttributeValue { S = sortKey }
    };

    public static AttributeValue Number(long value) =>
        new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static long ReadNumber(Dictionary<string, AttributeValue> item, string attribute) =>
        TryReadNumber(item, attribute, out var value)
            ? value
            : throw new InvalidOperationException($"The outbox item has no numeric '{attribute}' attribute.");

    public static bool TryReadNumber(Dictionary<string, AttributeValue> item, string attribute, out long value)
    {
        value = 0;
        return item.TryGetValue(attribute, out var attributeValue)
            && attributeValue.N is { } number
            && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Builds the item for a message. A null key, value or header blob is an absent attribute;
    /// an empty one is an empty binary, because the outbox orders an empty key as a real key.
    /// </summary>
    public Dictionary<string, AttributeValue> ToItem(OutboxMessage message, long sequence, int bucketCount)
    {
        var item = MessageKey(message.Bucket, sequence);
        item[MessageId] = new AttributeValue { S = message.MessageId.ToString("D") };
        item[Topic] = new AttributeValue { S = message.Topic };
        item[CreatedAtUtc] = Number(message.CreatedAtUtc.UtcTicks);
        item[BucketCount] = Number(bucketCount);
        if (message.Key is not null)
            item[Key] = Binary(message.Key);
        if (message.Value is not null)
            item[Value] = Binary(message.Value);
        if (message.Headers is not null)
            item[Headers] = Binary(message.Headers);
        if (message.Partition is { } partition)
            item[Partition] = Number(partition);
        return item;
    }

    public OutboxMessage FromItem(int bucket, Dictionary<string, AttributeValue> item)
    {
        var sortKey = item.TryGetValue(SortKeyName, out var sortKeyValue) ? sortKeyValue.S : null;
        if (!long.TryParse(sortKey, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
        {
            throw new InvalidOperationException(
                $"Outbox bucket {bucket} holds an item whose sort key '{sortKey}' is not a sequence number.");
        }

        if (!item.TryGetValue(MessageId, out var messageId) || !Guid.TryParse(messageId.S, out var parsedMessageId)
            || !item.TryGetValue(Topic, out var topic) || string.IsNullOrEmpty(topic.S))
        {
            throw new InvalidOperationException(
                $"Outbox message {sequence} in bucket {bucket} lacks a valid '{MessageId}' or '{Topic}' attribute.");
        }

        return new OutboxMessage
        {
            Id = sequence,
            MessageId = parsedMessageId,
            Bucket = bucket,
            Topic = topic.S,
            Key = ReadBinary(item, Key),
            Value = ReadBinary(item, Value),
            Headers = ReadBinary(item, Headers),
            Partition = TryReadNumber(item, Partition, out var partition) ? checked((int)partition) : null,
            CreatedAtUtc = new DateTimeOffset(ReadNumber(item, CreatedAtUtc), TimeSpan.Zero)
        };
    }

    // A key attribute that doubles as an item attribute would be overwritten by it.
    private static void ThrowIfItemAttribute(string keyAttributeName)
    {
        if (Array.IndexOf(ItemAttributeNames, keyAttributeName) >= 0)
        {
            throw new ArgumentException(
                $"The key attribute name '{keyAttributeName}' is also an outbox item attribute. Choose another name.");
        }
    }

    private static AttributeValue Binary(byte[] bytes) => new() { B = new MemoryStream(bytes, writable: false) };

    private static byte[]? ReadBinary(Dictionary<string, AttributeValue> item, string attribute) =>
        item.TryGetValue(attribute, out var value) && value.B is { } stream ? stream.ToArray() : null;
}
