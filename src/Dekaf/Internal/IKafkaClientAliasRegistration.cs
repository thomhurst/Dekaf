namespace Dekaf.Internal;

/// <summary>
/// Identifies a DI descriptor that forwards to another registered client.
/// In-memory replacement preserves the alias and replaces its target instead.
/// </summary>
internal interface IKafkaClientAliasRegistration
{
    object TargetServiceKey { get; }
}
