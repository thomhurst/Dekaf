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
                    return;
            }
            catch (ResourceNotFoundException)
            {
                // Creation is not visible to this read yet.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }
}
