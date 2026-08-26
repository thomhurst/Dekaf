using System.Buffers.Binary;
using Dekaf.Errors;

namespace Dekaf.Serialization.Routing;

/// <summary>
/// Routes deserialization by the record topic.
/// </summary>
/// <typeparam name="TBase">The common reference type returned by every route.</typeparam>
public sealed class TopicRoutingDeserializer<TBase> :
    IDeserializer<TBase>,
    IAsyncDeserializerPreparer<TBase>,
    IAsyncDeserializerPreparationRequirement,
    IRecordHeaderDeserializer<TBase>,
    IRecordHeaderAsyncDeserializerPreparer<TBase>,
    ICallerOwnedHeaderDeserializer<TBase>,
    IRecordHeaderRoutingProvider
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
        var requiresPreparation = DeserializerPreparation.RequiresPreparation(deserializer);
        IDeserializer<TBase> route = requiresPreparation ||
                                     deserializer is IAsyncDeserializerPreparationRequirement
            ? new AsyncDeserializerRoute<TBase, TDerived>(deserializer)
            : deserializer;
        _routes.Register(topic, route);
        return this;
    }

    /// <summary>Sets the route used when no topic registration matches.</summary>
    public TopicRoutingDeserializer<TBase> SetFallback(IDeserializer<TBase> deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        var route = DeserializerPreparation.RequiresPreparation(deserializer)
            ? new AsyncDeserializerRoute<TBase, TBase>(deserializer)
            : deserializer;
        _routes.SetFallback(route);
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

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        _routes.RequiresDeserializerPreparation;

    bool IAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out TBase value)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
            return DeserializerPreparation.TryDeserialize(route, data, context, out value);

        throw MissingRoute("topic", context.Topic, context);
    }

    ValueTask IAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
            return DeserializerPreparation.PrepareAsync(route, data, context, cancellationToken);

        throw MissingRoute("topic", context.Topic, context);
    }

    TBase IRecordHeaderDeserializer<TBase>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
            return RecordHeaderDeserializer.DeserializeChild(route, data, context, in headers);

        throw MissingRoute("topic", context.Topic, context);
    }

    bool IRecordHeaderAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out TBase value)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
        {
            return DeserializerPreparation.TryDeserialize(
                route,
                data,
                context,
                in headers,
                out value);
        }

        throw MissingRoute("topic", context.Topic, context);
    }

    ValueTask IRecordHeaderAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
        {
            return DeserializerPreparation.PrepareAsync(
                route,
                data,
                context,
                headers,
                cancellationToken);
        }

        throw MissingRoute("topic", context.Topic, context);
    }

    TBase ICallerOwnedHeaderDeserializer<TBase>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context)
    {
        if (_routes.TryGetRoute(context.Topic, out var route))
            return RecordHeaderDeserializer.DeserializeCallerOwned(route, data, context);

        throw MissingRoute("topic", context.Topic, context);
    }

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names) =>
        _routes.CollectHeaderNames(names);

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
public sealed class SchemaIdRoutingDeserializer<TBase> :
    IDeserializer<TBase>,
    IAsyncDeserializerPreparer<TBase>,
    IAsyncDeserializerPreparationRequirement,
    IRecordHeaderDeserializer<TBase>,
    IRecordHeaderAsyncDeserializerPreparer<TBase>,
    ICallerOwnedHeaderDeserializer<TBase>,
    IRecordHeaderRoutingProvider
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
        var requiresPreparation = DeserializerPreparation.RequiresPreparation(deserializer);
        IDeserializer<TBase> route = requiresPreparation ||
                                     deserializer is IAsyncDeserializerPreparationRequirement
            ? new AsyncDeserializerRoute<TBase, TDerived>(deserializer)
            : deserializer;
        _routes.Register(schemaId, route);
        return this;
    }

    /// <summary>Sets the route used when no schema-ID registration matches.</summary>
    public SchemaIdRoutingDeserializer<TBase> SetFallback(IDeserializer<TBase> deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        var route = DeserializerPreparation.RequiresPreparation(deserializer)
            ? new AsyncDeserializerRoute<TBase, TBase>(deserializer)
            : deserializer;
        _routes.SetFallback(route);
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

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        _routes.RequiresDeserializerPreparation;

    bool IAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out TBase value)
    {
        var schemaId = ReadSchemaId(data, context);
        if (_routes.TryGetRoute(schemaId, out var route))
            return DeserializerPreparation.TryDeserialize(route, data, context, out value);

        throw MissingRoute(schemaId, context);
    }

    ValueTask IAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        var schemaId = ReadSchemaId(data, context);
        if (_routes.TryGetRoute(schemaId, out var route))
            return DeserializerPreparation.PrepareAsync(route, data, context, cancellationToken);

        throw MissingRoute(schemaId, context);
    }

    TBase IRecordHeaderDeserializer<TBase>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
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
            return RecordHeaderDeserializer.DeserializeChild(route, data, context, in headers);

        throw new SerializationException($"No schema-ID deserializer route is registered for '{schemaId}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
    }

    bool IRecordHeaderAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out TBase value)
    {
        var schemaId = ReadSchemaId(data, context);
        if (_routes.TryGetRoute(schemaId, out var route))
        {
            return DeserializerPreparation.TryDeserialize(
                route,
                data,
                context,
                in headers,
                out value);
        }

        throw MissingRoute(schemaId, context);
    }

    ValueTask IRecordHeaderAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken)
    {
        var schemaId = ReadSchemaId(data, context);
        if (_routes.TryGetRoute(schemaId, out var route))
        {
            return DeserializerPreparation.PrepareAsync(
                route,
                data,
                context,
                headers,
                cancellationToken);
        }

        throw MissingRoute(schemaId, context);
    }

    TBase ICallerOwnedHeaderDeserializer<TBase>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context)
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
            return RecordHeaderDeserializer.DeserializeCallerOwned(route, data, context);

        throw new SerializationException($"No schema-ID deserializer route is registered for '{schemaId}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
    }

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names) =>
        _routes.CollectHeaderNames(names);

    private static int ReadSchemaId(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (data.Length >= HeaderSize && data.Span[0] == 0)
            return BinaryPrimitives.ReadInt32BigEndian(data.Span.Slice(1, sizeof(int)));

        throw new SerializationException("Schema-ID routing requires Confluent framing.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
    }

    private static SerializationException MissingRoute(int schemaId, SerializationContext context) =>
        new($"No schema-ID deserializer route is registered for '{schemaId}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
}

internal sealed class AsyncDeserializerRoute<TBase, TDerived>(IDeserializer<TDerived> deserializer) :
    IDeserializer<TBase>,
    IAsyncDeserializerPreparer<TBase>,
    IAsyncDeserializerPreparationRequirement,
    IRecordHeaderDeserializer<TBase>,
    IRecordHeaderAsyncDeserializerPreparer<TBase>,
    ICallerOwnedHeaderDeserializer<TBase>,
    IRecordHeaderRoutingProvider
    where TBase : class
    where TDerived : class, TBase
{
    private readonly IDeserializer<TDerived> _deserializer = deserializer;

    public TBase Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        _deserializer.Deserialize(data, context);

    bool IAsyncDeserializerPreparationRequirement.RequiresPreparation =>
        DeserializerPreparation.RequiresPreparation(_deserializer);

    bool IAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        out TBase value)
    {
        if (!DeserializerPreparation.TryDeserialize(
                _deserializer,
                data,
                context,
                out var derivedValue))
        {
            value = default!;
            return false;
        }

        value = derivedValue;
        return true;
    }

    ValueTask IAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        DeserializerPreparation.PrepareAsync(
            _deserializer,
            data,
            context,
            cancellationToken);

    TBase IRecordHeaderDeserializer<TBase>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        RecordHeaderDeserializer.DeserializeChild(
            _deserializer,
            data,
            context,
            in headers);

    bool IRecordHeaderAsyncDeserializerPreparer<TBase>.TryDeserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers,
        out TBase value)
    {
        if (!DeserializerPreparation.TryDeserialize(
                _deserializer,
                data,
                context,
                in headers,
                out var derivedValue))
        {
            value = default!;
            return false;
        }

        value = derivedValue;
        return true;
    }

    ValueTask IRecordHeaderAsyncDeserializerPreparer<TBase>.PrepareAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        RecordHeaderRoutingLookup headers,
        CancellationToken cancellationToken) =>
        DeserializerPreparation.PrepareAsync(
            _deserializer,
            data,
            context,
            headers,
            cancellationToken);

    TBase ICallerOwnedHeaderDeserializer<TBase>.DeserializeCallerOwned(
        ReadOnlyMemory<byte> data,
        SerializationContext context) =>
        RecordHeaderDeserializer.DeserializeCallerOwned(_deserializer, data, context);

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        if (_deserializer is IRecordHeaderRoutingProvider provider)
            provider.CollectHeaderNames(names);
    }
}
