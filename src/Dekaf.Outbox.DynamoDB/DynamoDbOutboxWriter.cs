using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Default <see cref="IDynamoDbOutboxWriter"/>.
/// </summary>
public sealed class DynamoDbOutboxWriter : IDynamoDbOutboxWriter
{
    /// <summary>
    /// The DynamoDB limit on the actions of one <c>TransactWriteItems</c> request.
    /// </summary>
    public const int MaxTransactionItems = 100;

    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbOutboxOptions _options;
    private readonly DynamoDbOutboxSchema _schema;
    private readonly IOutboxNotifier? _notifier;

    /// <summary>
    /// Creates the writer. The caller owns <paramref name="client"/>.
    /// </summary>
    /// <param name="notifier">The local relay's notifier, or null in a process without a relay.</param>
    public DynamoDbOutboxWriter(
        IAmazonDynamoDB client,
        DynamoDbOutboxOptions options,
        IOutboxNotifier? notifier = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
        _schema = new DynamoDbOutboxSchema(options);
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async ValueTask<TransactWriteItem> CreateTransactWriteItemAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ValidateMessage(message);
        var last = await ReserveSequencesAsync(message.Bucket, 1, cancellationToken).ConfigureAwait(false);
        return CreatePut(message, last);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TransactWriteItem>> CreateTransactWriteItemsAsync(
        IReadOnlyList<OutboxMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            return [];

        var perBucket = new Dictionary<int, int>();
        for (var index = 0; index < messages.Count; index++)
        {
            ValidateMessage(messages[index]);
            perBucket[messages[index].Bucket] = perBucket.GetValueOrDefault(messages[index].Bucket) + 1;
        }

        // One atomic reservation per bucket covers all of that bucket's messages.
        var buckets = new int[perBucket.Count];
        perBucket.Keys.CopyTo(buckets, 0);
        var next = new long[buckets.Length];
        await BoundedConcurrency.ForAsync(buckets.Length, _options.MaxConcurrency, async (index, token) =>
        {
            var count = perBucket[buckets[index]];
            next[index] = await ReserveSequencesAsync(buckets[index], count, token).ConfigureAwait(false) - count + 1;
        }, cancellationToken).ConfigureAwait(false);

        var items = new TransactWriteItem[messages.Count];
        for (var index = 0; index < messages.Count; index++)
        {
            var slot = Array.IndexOf(buckets, messages[index].Bucket);
            items[index] = CreatePut(messages[index], next[slot]++);
        }

        return items;
    }

    /// <inheritdoc />
    public void NotifyCommitted(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_notifier is IOutboxBucketNotifier bucketNotifier)
            bucketNotifier.NotifyCommitted(message.Bucket);
        else
            _notifier?.NotifyCommitted();
    }

    /// <inheritdoc />
    public void NotifyCommitted(IReadOnlyList<OutboxMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0 || _notifier is null)
            return;

        if (_notifier is not IOutboxBucketNotifier bucketNotifier)
        {
            _notifier.NotifyCommitted();
            return;
        }

        if (messages.Count == 1)
        {
            bucketNotifier.NotifyCommitted(messages[0].Bucket);
            return;
        }

        // A HashSet keeps the exact bucket ids on the cross-process notification transport.
        var buckets = new HashSet<int>();
        for (var index = 0; index < messages.Count; index++)
            buckets.Add(messages[index].Bucket);
        bucketNotifier.NotifyCommitted(buckets);
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var item = await CreateTransactWriteItemAsync(message, cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = item.Put.TableName,
                Item = item.Put.Item,
                ConditionExpression = item.Put.ConditionExpression,
                ExpressionAttributeNames = item.Put.ExpressionAttributeNames,
                ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.ALL_OLD
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException refused) when (IsSameMessage(refused.Item, message))
        {
            // The AWS SDK retried a put whose first attempt was applied but whose response
            // was lost. The message is stored, so this is a success, not an overwrite.
        }

        NotifyCommitted(message);
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(IReadOnlyList<OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
            return;
        if (messages.Count > MaxTransactionItems)
        {
            throw new ArgumentException(
                $"DynamoDB commits at most {MaxTransactionItems} items atomically; got {messages.Count} messages.",
                nameof(messages));
        }

        var items = await CreateTransactWriteItemsAsync(messages, cancellationToken).ConfigureAwait(false);
        await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = [.. items] },
            cancellationToken).ConfigureAwait(false);
        NotifyCommitted(messages);
    }

    private static bool IsSameMessage(Dictionary<string, AttributeValue>? stored, OutboxMessage message) =>
        stored is not null
        && stored.TryGetValue(DynamoDbOutboxSchema.MessageId, out var messageId)
        && Guid.TryParse(messageId.S, out var storedId)
        && storedId == message.MessageId;

    private void ValidateMessage(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if ((uint)message.Bucket >= (uint)_options.BucketCount)
        {
            throw new ArgumentException(
                $"The message is in bucket {message.Bucket}, outside [0, {_options.BucketCount}). Create messages " +
                "with the bucket count of DynamoDbOutboxOptions.BucketCount.", nameof(message));
        }
    }

    /// <returns>The last reserved sequence number.</returns>
    private async Task<long> ReserveSequencesAsync(int bucket, int count, CancellationToken cancellationToken)
    {
        var response = await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _schema.TableName,
            Key = _schema.SequenceKey(bucket),
            // ADD creates the counter at zero first, so the first sequence number is one.
            UpdateExpression = "ADD #sequence :count",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#sequence"] = DynamoDbOutboxSchema.Sequence },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":count"] = DynamoDbOutboxSchema.Number(count)
            },
            ReturnValues = ReturnValue.UPDATED_NEW
        }, cancellationToken).ConfigureAwait(false);
        return DynamoDbOutboxSchema.ReadNumber(response.Attributes, DynamoDbOutboxSchema.Sequence);
    }

    private TransactWriteItem CreatePut(OutboxMessage message, long sequence) => new()
    {
        Put = new Put
        {
            TableName = _schema.TableName,
            Item = _schema.ToItem(message, sequence, _options.BucketCount),
            // A counter that was reset or deleted hands out numbers again. Refusing the write
            // fails the business transaction instead of overwriting a pending message.
            ConditionExpression = "attribute_not_exists(#pk)",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _schema.PartitionKeyName }
        }
    };
}
