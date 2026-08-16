using System.Buffers.Binary;
using Dekaf.Errors;

namespace Dekaf.Serialization.Routing;

/// <summary>
/// Routes deserialization by the record topic.
/// </summary>
/// <typeparam name="TBase">The common reference type returned by every route.</typeparam>
public sealed class TopicRoutingDeserializer<TBase> : IDeserializer<TBase>
    where TBase : class
{
    private readonly FrozenRouteTable<string, IDeserializer<TBase>> _routes = new(StringComparer.Ordinal);

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    /// <summary>Registers one topic route.</summary>
    public TopicRoutingDeserializer<TBase> Register<TDerived>(
        string topic,
        IDeserializer<TDerived> deserializer)
        where TDerived : class, TBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(deserializer);
        _routes.Register(topic, deserializer);
        return this;
    }

    /// <summary>Sets the route used when no topic registration matches.</summary>
    public TopicRoutingDeserializer<TBase> SetFallback(IDeserializer<TBase> deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        _routes.SetFallback(deserializer);
        return this;
    }

    /// <summary>Freezes registration and enables concurrent routing.</summary>
    public TopicRoutingDeserializer<TBase> Freeze()
    {
        _routes.Freeze();
        return this;
    }

    /// <inheritdoc />
    public TBase Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
            return route.Deserialize(data, context);

        throw MissingRoute("topic", context.Topic, context);
    }

    private static SerializationException MissingRoute(
        string routeKind,
        string routeValue,
        SerializationContext context) =>
        new($"No {routeKind} deserializer route is registered for '{routeValue}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
}

/// <summary>
/// Routes Confluent-framed Schema Registry payloads by schema ID.
/// </summary>
/// <remarks>
/// The selected deserializer receives the complete framed input. Registration is mutable only
/// before <see cref="Freeze"/>; after freezing, routing is thread-safe and allocation-free.
/// </remarks>
/// <typeparam name="TBase">The common reference type returned by every route.</typeparam>
public sealed class SchemaIdRoutingDeserializer<TBase> : IDeserializer<TBase>
    where TBase : class
{
    private const int HeaderSize = 5;
    private readonly FrozenRouteTable<int, IDeserializer<TBase>> _routes = new();

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    /// <summary>Registers one schema-ID route.</summary>
    public SchemaIdRoutingDeserializer<TBase> Register<TDerived>(
        int schemaId,
        IDeserializer<TDerived> deserializer)
        where TDerived : class, TBase
    {
        ArgumentOutOfRangeException.ThrowIfNegative(schemaId);
        ArgumentNullException.ThrowIfNull(deserializer);
        _routes.Register(schemaId, deserializer);
        return this;
    }

    /// <summary>Sets the route used when no schema-ID registration matches.</summary>
    public SchemaIdRoutingDeserializer<TBase> SetFallback(IDeserializer<TBase> deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        _routes.SetFallback(deserializer);
        return this;
    }

    /// <summary>Freezes registration and enables concurrent routing.</summary>
    public SchemaIdRoutingDeserializer<TBase> Freeze()
    {
        _routes.Freeze();
        return this;
    }

    /// <inheritdoc />
    public TBase Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (data.Length < HeaderSize || data.Span[0] != 0)
        {
            throw new SerializationException("Schema-ID routing requires Confluent framing.")
            {
                Topic = context.Topic,
                Component = context.Component
            };
        }

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(data.Span.Slice(1, sizeof(int)));
        if (_routes.TryGetRoute(schemaId, out var route))
            return route.Deserialize(data, context);

        throw new SerializationException($"No schema-ID deserializer route is registered for '{schemaId}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
    }
}
