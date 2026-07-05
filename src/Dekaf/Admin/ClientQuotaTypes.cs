using Dekaf.Errors;

namespace Dekaf.Admin;

/// <summary>
/// Entity type for a Kafka client quota.
/// </summary>
public enum ClientQuotaEntityType
{
    /// <summary>
    /// Unknown entity type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// User quota entity.
    /// </summary>
    User = 1,

    /// <summary>
    /// Client ID quota entity.
    /// </summary>
    ClientId = 2,

    /// <summary>
    /// IP quota entity.
    /// </summary>
    Ip = 3
}

/// <summary>
/// Match type for a client quota filter component.
/// </summary>
public enum ClientQuotaMatchType : sbyte
{
    /// <summary>
    /// Match a specific entity name.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Match the default entity for the entity type.
    /// </summary>
    Default = 1,

    /// <summary>
    /// Match any specified entity name for the entity type.
    /// </summary>
    AnySpecified = 2
}

/// <summary>
/// Filter for DescribeClientQuotas.
/// </summary>
public sealed class ClientQuotaFilter
{
    /// <summary>
    /// Entity components to match. An empty list matches all quota entities.
    /// </summary>
    public required IReadOnlyList<ClientQuotaFilterComponent> Components { get; init; }

    /// <summary>
    /// True if only entities with exactly these components should match.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Creates a filter that matches all client quota entities.
    /// </summary>
    public static ClientQuotaFilter All() => new() { Components = [] };
}

/// <summary>
/// A single entity component in a client quota filter.
/// </summary>
public sealed class ClientQuotaFilterComponent
{
    /// <summary>
    /// The entity type to match.
    /// </summary>
    public required ClientQuotaEntityType EntityType { get; init; }

    /// <summary>
    /// The match type.
    /// </summary>
    public required ClientQuotaMatchType MatchType { get; init; }

    /// <summary>
    /// The entity name for exact matches, otherwise null.
    /// </summary>
    public string? Match { get; init; }

    /// <summary>
    /// Creates an exact entity-name match.
    /// </summary>
    public static ClientQuotaFilterComponent Exact(ClientQuotaEntityType entityType, string name) =>
        new() { EntityType = entityType, MatchType = ClientQuotaMatchType.Exact, Match = name };

    /// <summary>
    /// Creates a default-entity match.
    /// </summary>
    public static ClientQuotaFilterComponent Default(ClientQuotaEntityType entityType) =>
        new() { EntityType = entityType, MatchType = ClientQuotaMatchType.Default };

    /// <summary>
    /// Creates a match for any specified entity name.
    /// </summary>
    public static ClientQuotaFilterComponent AnySpecified(ClientQuotaEntityType entityType) =>
        new() { EntityType = entityType, MatchType = ClientQuotaMatchType.AnySpecified };

    /// <summary>
    /// Validates this filter component before sending it to Kafka.
    /// </summary>
    public void Validate()
    {
        _ = ClientQuotaEntityTypeNames.ToProtocolName(EntityType);

        switch (MatchType)
        {
            case ClientQuotaMatchType.Exact when Match is null:
                throw new ArgumentException("Exact client quota filter components must specify a match.");

            case ClientQuotaMatchType.Exact:
                return;

            case ClientQuotaMatchType.Default or ClientQuotaMatchType.AnySpecified when Match is not null:
                throw new ArgumentException("Only exact client quota filter components can specify a match.");

            case ClientQuotaMatchType.Default or ClientQuotaMatchType.AnySpecified:
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(MatchType), MatchType, "Unsupported client quota match type.");
        }
    }
}

/// <summary>
/// A single entity component in a client quota entity.
/// </summary>
public sealed class ClientQuotaEntityComponent
{
    /// <summary>
    /// The entity type.
    /// </summary>
    public required ClientQuotaEntityType EntityType { get; init; }

    /// <summary>
    /// The entity name, or null for the default entity of this type.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Creates a user entity component.
    /// </summary>
    public static ClientQuotaEntityComponent User(string? name) =>
        new() { EntityType = ClientQuotaEntityType.User, Name = name };

    /// <summary>
    /// Creates a client-id entity component.
    /// </summary>
    public static ClientQuotaEntityComponent ClientId(string? name) =>
        new() { EntityType = ClientQuotaEntityType.ClientId, Name = name };

    /// <summary>
    /// Creates an IP entity component.
    /// </summary>
    public static ClientQuotaEntityComponent Ip(string? name) =>
        new() { EntityType = ClientQuotaEntityType.Ip, Name = name };
}

/// <summary>
/// A Kafka client quota entity.
/// </summary>
public sealed class ClientQuotaEntity : IEquatable<ClientQuotaEntity>
{
    /// <summary>
    /// Entity components that identify this quota entity.
    /// </summary>
    public required IReadOnlyList<ClientQuotaEntityComponent> Components { get; init; }

    /// <summary>
    /// Creates a user quota entity.
    /// </summary>
    public static ClientQuotaEntity ForUser(string? user) =>
        For(ClientQuotaEntityComponent.User(user));

    /// <summary>
    /// Creates a client-id quota entity.
    /// </summary>
    public static ClientQuotaEntity ForClientId(string? clientId) =>
        For(ClientQuotaEntityComponent.ClientId(clientId));

    /// <summary>
    /// Creates an IP quota entity.
    /// </summary>
    public static ClientQuotaEntity ForIp(string? ip) =>
        For(ClientQuotaEntityComponent.Ip(ip));

    /// <summary>
    /// Creates a quota entity from one or more components.
    /// </summary>
    public static ClientQuotaEntity For(params ClientQuotaEntityComponent[] components) =>
        new() { Components = components.ToList() };

    public bool Equals(ClientQuotaEntity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Components.Count != other.Components.Count)
        {
            return false;
        }

        var matched = new bool[other.Components.Count];
        foreach (var component in Components)
        {
            var found = false;
            for (var i = 0; i < other.Components.Count; i++)
            {
                if (matched[i])
                {
                    continue;
                }

                var otherComponent = other.Components[i];
                if (component.EntityType != otherComponent.EntityType || component.Name != otherComponent.Name)
                {
                    continue;
                }

                matched[i] = true;
                found = true;
                break;
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ClientQuotaEntity);

    public override int GetHashCode()
    {
        var hash = Components.Count;
        foreach (var component in Components)
        {
            hash ^= HashCode.Combine(component.EntityType, component.Name);
        }

        return hash;
    }

    public override string ToString() =>
        string.Join(",", Components.Select(c => $"{c.EntityType}:{c.Name ?? "<default>"}"));
}

/// <summary>
/// A client quota set or remove operation.
/// </summary>
public sealed class ClientQuotaOperation
{
    /// <summary>
    /// The quota key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The quota value for set operations.
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// True to remove this quota key.
    /// </summary>
    public bool Remove { get; init; }

    /// <summary>
    /// Creates a set operation.
    /// </summary>
    public static ClientQuotaOperation Set(string key, double value) =>
        new() { Key = key, Value = value };

    /// <summary>
    /// Creates a remove operation.
    /// </summary>
    public static ClientQuotaOperation RemoveValue(string key) =>
        new() { Key = key, Remove = true };
}

/// <summary>
/// Client quota alterations for one entity.
/// </summary>
public sealed class ClientQuotaAlteration
{
    /// <summary>
    /// The entity to alter.
    /// </summary>
    public required ClientQuotaEntity Entity { get; init; }

    /// <summary>
    /// Set or remove operations for the entity.
    /// </summary>
    public required IReadOnlyList<ClientQuotaOperation> Operations { get; init; }

    /// <summary>
    /// Creates an alteration that sets one quota key.
    /// </summary>
    public static ClientQuotaAlteration Set(ClientQuotaEntity entity, string key, double value) =>
        new() { Entity = entity, Operations = [ClientQuotaOperation.Set(key, value)] };

    /// <summary>
    /// Creates an alteration that removes one quota key.
    /// </summary>
    public static ClientQuotaAlteration Remove(ClientQuotaEntity entity, string key) =>
        new() { Entity = entity, Operations = [ClientQuotaOperation.RemoveValue(key)] };

    /// <summary>
    /// Validates this alteration before sending it to Kafka.
    /// </summary>
    public void Validate()
    {
        CompatibilityThrowHelpers.ThrowIfNull(Entity);

        if (Entity.Components is null || Entity.Components.Count == 0)
        {
            throw new ArgumentException("Client quota alteration entity must contain at least one component.");
        }

        foreach (var component in Entity.Components)
        {
            CompatibilityThrowHelpers.ThrowIfNull(component);
            _ = ClientQuotaEntityTypeNames.ToProtocolName(component.EntityType);
        }

        if (Operations is null || Operations.Count == 0)
        {
            throw new ArgumentException("Client quota alteration must contain at least one operation.");
        }

        foreach (var operation in Operations)
        {
            CompatibilityThrowHelpers.ThrowIfNull(operation);
            CompatibilityThrowHelpers.ThrowIfNullOrEmpty(operation.Key);
        }
    }
}

/// <summary>
/// Options for DescribeClientQuotas.
/// </summary>
public sealed class DescribeClientQuotasOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for AlterClientQuotas.
/// </summary>
public sealed class AlterClientQuotasOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// If true, the broker validates the request but does not change quotas.
    /// </summary>
    public bool ValidateOnly { get; init; }
}

internal static class ClientQuotaEntityTypeNames
{
    public static string ToProtocolName(ClientQuotaEntityType entityType) => entityType switch
    {
        ClientQuotaEntityType.User => "user",
        ClientQuotaEntityType.ClientId => "client-id",
        ClientQuotaEntityType.Ip => "ip",
        ClientQuotaEntityType.Unknown => throw new KafkaException(
            "Unknown client quota entity types cannot be sent to Kafka because the original protocol name is not available."),
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unsupported client quota entity type.")
    };

    public static ClientQuotaEntityType FromProtocolName(string entityType) => entityType switch
    {
        "user" => ClientQuotaEntityType.User,
        "client-id" => ClientQuotaEntityType.ClientId,
        "ip" => ClientQuotaEntityType.Ip,
        _ => ClientQuotaEntityType.Unknown
    };
}
