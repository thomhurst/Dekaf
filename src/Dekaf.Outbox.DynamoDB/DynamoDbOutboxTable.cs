using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Creates the outbox table for development, tests and samples. Production tables usually
/// come from infrastructure as code; <see cref="CreateTableRequest"/> is the schema to copy.
/// </summary>
public static class DynamoDbOutboxTable
{
    /// <summary>
    /// The table definition: a string partition key, a string sort key, on-demand billing.
    /// </summary>
    public static CreateTableRequest CreateTableRequest(DynamoDbOutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return new CreateTableRequest
        {
            TableName = options.TableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition(options.PartitionKeyAttributeName, ScalarAttributeType.S),
                new AttributeDefinition(options.SortKeyAttributeName, ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement(options.PartitionKeyAttributeName, KeyType.HASH),
                new KeySchemaElement(options.SortKeyAttributeName, KeyType.RANGE)
            ]
        };
    }

    /// <summary>
    /// Creates the table unless it exists, and waits until it is active. Safe to call from
    /// several instances at once.
    /// </summary>
    /// <exception cref="OutboxMisconfigurationException">The table exists with a key schema
    /// other than the one <paramref name="options"/> name.</exception>
    public static async Task CreateIfNotExistsAsync(
        IAmazonDynamoDB client,
        DynamoDbOutboxOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        var request = CreateTableRequest(options);
        try
        {
            await client.CreateTableAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceInUseException)
        {
            // The table exists, or another instance is creating it.
        }

        while (true)
        {
            try
            {
                var description = await client.DescribeTableAsync(options.TableName, cancellationToken)
                    .ConfigureAwait(false);
                if (description.Table.TableStatus == TableStatus.ACTIVE)
                {
                    ThrowIfKeySchemaDiffers(description.Table, options);
                    return;
                }
            }
            catch (ResourceNotFoundException)
            {
                // Creation is not visible to this read yet.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A table that was there already may have been made for something else. Every outbox
    /// request would fail against it later, with an error that names no cause.
    /// </summary>
    private static void ThrowIfKeySchemaDiffers(TableDescription table, DynamoDbOutboxOptions options)
    {
        if (IsStringKey(table, KeyType.HASH, options.PartitionKeyAttributeName)
            && IsStringKey(table, KeyType.RANGE, options.SortKeyAttributeName))
        {
            return;
        }

        var actual = string.Join(", ", (table.KeySchema ?? []).Select(key =>
            $"{key.AttributeName} ({key.KeyType}, {AttributeType(table, key.AttributeName) ?? "?"})"));
        throw new OutboxMisconfigurationException(
            $"Table '{options.TableName}' exists with the key schema [{actual}]. The outbox needs the string partition " +
            $"key '{options.PartitionKeyAttributeName}' and the string sort key '{options.SortKeyAttributeName}'. Use " +
            "another table, or set PartitionKeyAttributeName and SortKeyAttributeName to the table's key attributes.");
    }

    private static bool IsStringKey(TableDescription table, KeyType keyType, string attributeName) =>
        (table.KeySchema ?? []).Any(key => key.KeyType == keyType && key.AttributeName == attributeName)
        && AttributeType(table, attributeName) == ScalarAttributeType.S;

    private static ScalarAttributeType? AttributeType(TableDescription table, string attributeName) =>
        (table.AttributeDefinitions ?? []).FirstOrDefault(attribute => attribute.AttributeName == attributeName)
            ?.AttributeType;
}
