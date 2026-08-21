using System.Buffers;
using Dekaf.Errors;

namespace Dekaf.Serialization.Routing;

/// <summary>
/// Routes serialization by the destination topic.
/// </summary>
/// <typeparam name="TBase">The common reference type accepted by every route.</typeparam>
public sealed class TopicRoutingSerializer<TBase> : ISerializer<TBase>, IRecordHeaderSerializer
    where TBase : class
{
    private readonly FrozenRouteTable<string, SerializerRoute<TBase>> _routes = new(StringComparer.Ordinal);

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    bool IRecordHeaderSerializer.ProducesRecordHeaders => _routes.ProducesRecordHeaders;

    /// <summary>Registers one topic route.</summary>
    public TopicRoutingSerializer<TBase> Register<TDerived>(string topic, ISerializer<TDerived> serializer)
        where TDerived : class, TBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(serializer);
        _routes.Register(topic, new SerializerRoute<TBase, TDerived>(serializer));
        return this;
    }

    /// <summary>Sets the route used when no topic registration matches.</summary>
    public TopicRoutingSerializer<TBase> SetFallback(ISerializer<TBase> serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _routes.SetFallback(new SerializerRoute<TBase, TBase>(serializer));
        return this;
    }

    /// <summary>Freezes registration and enables concurrent routing.</summary>
    public TopicRoutingSerializer<TBase> Freeze()
    {
        _routes.Freeze();
        return this;
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(TBase value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (value is null)
            throw MissingRoute("runtime type", "null", context);

        if (_routes.TryGetRoute(context.Topic, out var route))
        {
            route.Serialize(value, ref destination, context);
            return;
        }

        throw MissingRoute("topic", context.Topic, context);
    }

    private static SerializationException MissingRoute(
        string routeKind,
        string routeValue,
        SerializationContext context) =>
        new($"No {routeKind} serializer route is registered for '{routeValue}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
}

/// <summary>
/// Routes serialization by the value's exact runtime type.
/// </summary>
/// <typeparam name="TBase">The common reference type accepted by every route.</typeparam>
public sealed class TypeRoutingSerializer<TBase> : ISerializer<TBase>, IRecordHeaderSerializer
    where TBase : class
{
    private readonly FrozenRouteTable<Type, SerializerRoute<TBase>> _routes = new();

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    bool IRecordHeaderSerializer.ProducesRecordHeaders => _routes.ProducesRecordHeaders;

    /// <summary>Registers one exact runtime-type route.</summary>
    public TypeRoutingSerializer<TBase> Register<TDerived>(ISerializer<TDerived> serializer)
        where TDerived : class, TBase
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _routes.Register(typeof(TDerived), new SerializerRoute<TBase, TDerived>(serializer));
        return this;
    }

    /// <summary>Sets the route used when no exact runtime-type registration matches.</summary>
    public TypeRoutingSerializer<TBase> SetFallback(ISerializer<TBase> serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _routes.SetFallback(new SerializerRoute<TBase, TBase>(serializer));
        return this;
    }

    /// <summary>Freezes registration and enables concurrent routing.</summary>
    public TypeRoutingSerializer<TBase> Freeze()
    {
        _routes.Freeze();
        return this;
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(TBase value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (value is null)
        {
            throw new SerializationException("No runtime-type serializer route is registered for 'null'.")
            {
                Topic = context.Topic,
                Component = context.Component
            };
        }

        if (_routes.TryGetRoute(value.GetType(), out var route))
        {
            route.Serialize(value, ref destination, context);
            return;
        }

        var routeValue = value.GetType().FullName;
        throw new SerializationException($"No runtime-type serializer route is registered for '{routeValue}'.")
        {
            Topic = context.Topic,
            Component = context.Component
        };
    }
}

internal abstract class SerializerRoute<TBase> : IRecordHeaderSerializer
    where TBase : class
{
    public abstract bool ProducesRecordHeaders { get; }

    internal abstract void Serialize<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
        ;
}

internal sealed class SerializerRoute<TBase, TDerived>(ISerializer<TDerived> serializer)
    : SerializerRoute<TBase>
    where TBase : class
    where TDerived : class, TBase
{
    public override bool ProducesRecordHeaders =>
        serializer is IRecordHeaderSerializer { ProducesRecordHeaders: true };

    internal override void Serialize<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context)
    {
        if (value is not TDerived typedValue)
        {
            throw new SerializationException(
                $"The route requires '{typeof(TDerived).FullName}', but received '{value?.GetType().FullName ?? "null"}'.")
            {
                Topic = context.Topic,
                Component = context.Component
            };
        }

        serializer.Serialize(typedValue, ref destination, context);
    }
}
