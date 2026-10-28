using System.Buffers;
using Dekaf.Errors;

namespace Dekaf.Serialization.Routing;

/// <summary>
/// Routes serialization by the destination topic.
/// </summary>
/// <typeparam name="TBase">The common reference type accepted by every route.</typeparam>
public sealed class TopicRoutingSerializer<TBase> :
    ISerializer<TBase>,
    IRecordHeaderSerializer,
    IAsyncSerializerPreparationAdmission<TBase>,
    ISerializerPreparationRequirement
    where TBase : class
{
    private readonly FrozenRouteTable<string, SerializerRoute<TBase>> _routes = new(StringComparer.Ordinal);

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    bool IRecordHeaderSerializer.ProducesRecordHeaders => _routes.ProducesRecordHeaders;

    bool ISerializerPreparationRequirement.RequiresPreparation => _routes.RequiresSerializerPreparation;

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

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken = default) =>
        _routes.RequiresSerializerPreparation && value is not null &&
        _routes.TryGetRoute(context.Topic, out var route)
            ? route.PrepareAsync(value, context, cancellationToken)
            : default;

    ValueTask<SerializerPreparationAdmission> IAsyncSerializerPreparationAdmission<TBase>.PrepareForSerializationAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        _routes.RequiresSerializerPreparation && value is not null &&
        _routes.TryGetRoute(context.Topic, out var route)
            ? route.PrepareForSerializationAsync(value, context, cancellationToken)
            : default;

    void IAsyncSerializerPreparationAdmission<TBase>.SerializePrepared<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
    {
        if (value is not null && _routes.TryGetRoute(context.Topic, out var route))
        {
            route.SerializePrepared(value, ref destination, context, in admission);
            return;
        }

        Serialize(value!, ref destination, context);
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
public sealed class TypeRoutingSerializer<TBase> :
    ISerializer<TBase>,
    IRecordHeaderSerializer,
    IAsyncSerializerPreparationAdmission<TBase>,
    ISerializerPreparationRequirement
    where TBase : class
{
    private readonly FrozenRouteTable<Type, SerializerRoute<TBase>> _routes = new();

    /// <summary>Whether registration has been frozen for routing.</summary>
    public bool IsFrozen => _routes.IsFrozen;

    bool IRecordHeaderSerializer.ProducesRecordHeaders => _routes.ProducesRecordHeaders;

    bool ISerializerPreparationRequirement.RequiresPreparation => _routes.RequiresSerializerPreparation;

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

    /// <inheritdoc />
    public ValueTask PrepareAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken = default) =>
        _routes.RequiresSerializerPreparation && value is not null &&
        _routes.TryGetRoute(value.GetType(), out var route)
            ? route.PrepareAsync(value, context, cancellationToken)
            : default;

    ValueTask<SerializerPreparationAdmission> IAsyncSerializerPreparationAdmission<TBase>.PrepareForSerializationAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        _routes.RequiresSerializerPreparation && value is not null &&
        _routes.TryGetRoute(value.GetType(), out var route)
            ? route.PrepareForSerializationAsync(value, context, cancellationToken)
            : default;

    void IAsyncSerializerPreparationAdmission<TBase>.SerializePrepared<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
    {
        if (value is not null && _routes.TryGetRoute(value.GetType(), out var route))
        {
            route.SerializePrepared(value, ref destination, context, in admission);
            return;
        }

        Serialize(value!, ref destination, context);
    }
}

internal abstract class SerializerRoute<TBase> : IRecordHeaderSerializer, ISerializerPreparationRequirement
    where TBase : class
{
    public abstract bool ProducesRecordHeaders { get; }

    public abstract bool RequiresPreparation { get; }

    internal abstract ValueTask PrepareAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken);

    internal abstract ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken);

    internal abstract void SerializePrepared<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
        where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
        , allows ref struct
#endif
        ;

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
    private readonly IAsyncSerializerPreparer<TDerived>? _preparer = serializer as IAsyncSerializerPreparer<TDerived>;
    private readonly IAsyncSerializerPreparationAdmission<TDerived>? _admission =
        serializer as IAsyncSerializerPreparationAdmission<TDerived>;

    public override bool ProducesRecordHeaders =>
        serializer is IRecordHeaderSerializer { ProducesRecordHeaders: true };

    // Nested routers report whether any of their own routes prepare; other preparers always do.
    public override bool RequiresPreparation =>
        _preparer is not null &&
        serializer is not ISerializerPreparationRequirement { RequiresPreparation: false };

    // A value of the wrong type is rejected by the following serialization call.
    internal override ValueTask PrepareAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken) =>
        _preparer is not null && value is TDerived typedValue
            ? _preparer.PrepareAsync(typedValue, context, cancellationToken)
            : default;

    internal override ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
        TBase value,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        if (_preparer is null || value is not TDerived typedValue)
            return default;

        return _admission is not null
            ? _admission.PrepareForSerializationAsync(typedValue, context, cancellationToken)
            : AwaitPreparationAsync(_preparer.PrepareAsync(typedValue, context, cancellationToken));

        static ValueTask<SerializerPreparationAdmission> AwaitPreparationAsync(ValueTask preparation)
        {
            if (preparation.IsCompletedSuccessfully)
            {
                // Consume the result so a source-backed ValueTask can recycle its source.
                preparation.GetAwaiter().GetResult();
                return default;
            }

            return AwaitAsync(preparation);

            static async ValueTask<SerializerPreparationAdmission> AwaitAsync(ValueTask pending)
            {
                await pending.ConfigureAwait(false);
                return default;
            }
        }
    }

    // Routes are frozen, so the admission always comes from this route's serializer.
    internal override void SerializePrepared<TWriter>(
        TBase value,
        ref TWriter destination,
        SerializationContext context,
        in SerializerPreparationAdmission admission)
    {
        if (_admission is not null && admission.IsPrepared && value is TDerived typedValue)
        {
            _admission.SerializePrepared(typedValue, ref destination, context, in admission);
            return;
        }

        Serialize(value, ref destination, context);
    }

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

/// <summary>Reports whether a serializer route needs the producer's asynchronous preparation step.</summary>
internal interface ISerializerPreparationRequirement
{
    bool RequiresPreparation { get; }
}
