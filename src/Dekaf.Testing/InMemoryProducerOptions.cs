namespace Dekaf.Testing;

/// <summary>
/// Options for <see cref="InMemoryProducer{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryProducerOptions
{
    /// <summary>
    /// Identity shared by replacement producers to recover prepared transactions.
    /// When null, the producer has a private identity and cannot recover another producer's transaction.
    /// </summary>
    public string? TransactionalId { get; init; }
}
