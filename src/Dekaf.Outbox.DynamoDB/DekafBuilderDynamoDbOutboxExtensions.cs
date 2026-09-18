using Amazon.DynamoDBv2;
using Dekaf.Extensions.DependencyInjection;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// DynamoDB outbox store registration for <see cref="DekafBuilder"/>.
/// </summary>
public static class DekafBuilderDynamoDbOutboxExtensions
{
    /// <summary>
    /// Registers <see cref="DynamoDbOutboxStore"/> as the <see cref="IOutboxStore"/> and
    /// <see cref="DynamoDbOutboxWriter"/> as the <see cref="IDynamoDbOutboxWriter"/>; exactly
    /// what <see cref="DynamoDbOutboxServiceCollectionExtensions.AddDekafDynamoDbOutboxStore"/>
    /// registers. Add <c>AddOutboxRelay</c> to the same builder to publish; a process that
    /// only enqueues needs the writer alone.
    /// </summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Table and layout options, identical for every writer and relay.</param>
    /// <param name="clientFactory">Supplies the DynamoDB client; called once per container.
    /// Defaults to the container's <see cref="IAmazonDynamoDB"/> registration. The container
    /// does not dispose a client returned by this factory.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddDynamoDbOutboxStore(
        this DekafBuilder builder,
        DynamoDbOutboxOptions options,
        Func<IServiceProvider, IAmazonDynamoDB>? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafDynamoDbOutboxStore(options, clientFactory);
        return builder;
    }
}
