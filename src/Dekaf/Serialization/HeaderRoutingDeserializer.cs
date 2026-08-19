using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

/// <summary>Maps one Kafka header value to a child deserializer.</summary>
public readonly struct HeaderDeserializerRoute<T>
{
    public HeaderDeserializerRoute(ReadOnlyMemory<byte> headerValue, IDeserializer<T> deserializer)
    {
        HeaderValue = headerValue;
        Deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
    }

    public ReadOnlyMemory<byte> HeaderValue { get; }
    public IDeserializer<T> Deserializer { get; }
}

/// <summary>Routes deserialization to a cached child deserializer using a Kafka header value.</summary>
/// <remarks>
/// The last header with the configured name wins. Missing, null, and unrecognized values use the
/// fallback. Routes are copied and indexed during construction; warmed calls do not allocate.
/// </remarks>
public sealed class HeaderRoutingDeserializer<T> :
    IDeserializer<T>,
    IRecordHeaderDeserializer<T>,
    IRecordHeaderRoutingProvider
{
    private readonly string _headerName;
    private readonly IDeserializer<T> _fallbackDeserializer;
    private readonly RouteEntry[] _routes;

    public HeaderRoutingDeserializer(
        string headerName,
        IDeserializer<T> fallbackDeserializer,
        params HeaderDeserializerRoute<T>[] routes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentNullException.ThrowIfNull(fallbackDeserializer);
        ArgumentNullException.ThrowIfNull(routes);

        _headerName = headerName;
        _fallbackDeserializer = fallbackDeserializer;
        _routes = new RouteEntry[routes.Length];
        for (var i = 0; i < routes.Length; i++)
        {
            var route = routes[i];
            if (route.Deserializer is null)
                throw new ArgumentException("Header routes must specify a deserializer.", nameof(routes));

            var value = route.HeaderValue.ToArray();
            _routes[i] = new RouteEntry(Hash(value), value, route.Deserializer);
        }

        SortRoutes(_routes);
        for (var i = 0; i < _routes.Length; i++)
        {
            ref readonly var candidate = ref _routes[i];
            for (var j = i + 1; j < _routes.Length && _routes[j].Hash == candidate.Hash; j++)
            {
                if (candidate.Value.AsSpan().SequenceEqual(_routes[j].Value))
                    throw new ArgumentException("Header route values must be unique.", nameof(routes));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var headers = context.Headers;
        if (headers is not null)
        {
            for (var i = headers.Count - 1; i >= 0; i--)
            {
                var header = headers[i];
                if (!string.Equals(header.Key, _headerName, StringComparison.Ordinal))
                    continue;

                if (!header.IsValueNull && TryGetDeserializer(header.Value.Span, out var deserializer))
                    return deserializer.Deserialize(data, context);

                break;
            }
        }

        return _fallbackDeserializer.Deserialize(data, context);
    }

    T IRecordHeaderDeserializer<T>.Deserialize(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers) =>
        DeserializeWithHeaders(data, context, in headers);

    void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names)
    {
        AddHeaderName(names, _headerName);
        CollectChildHeaderNames(names, _fallbackDeserializer);
        for (var index = 0; index < _routes.Length; index++)
            CollectChildHeaderNames(names, _routes[index].Deserializer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T DeserializeWithHeaders(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        in RecordHeaderRoutingLookup headers)
    {
        if (headers.TryGetLast(_headerName, out var header)
            && !header.IsValueNull
            && TryGetDeserializer(header.Value.Span, out var deserializer))
        {
            return RecordHeaderDeserializer.Deserialize(
                deserializer,
                data,
                context,
                in headers);
        }

        return RecordHeaderDeserializer.Deserialize(
            _fallbackDeserializer,
            data,
            context,
            in headers);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T DeserializeWithHeaders(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        ReadOnlySpan<Header> headers)
    {
        for (var i = headers.Length - 1; i >= 0; i--)
        {
            ref readonly var header = ref headers[i];
            if (!string.Equals(header.Key, _headerName, StringComparison.Ordinal))
                continue;

            if (!header.IsValueNull && TryGetDeserializer(header.Value.Span, out var deserializer))
                return deserializer is HeaderRoutingDeserializer<T> nested
                    ? nested.DeserializeWithHeaders(data, context, headers)
                    : deserializer.Deserialize(data, context);

            break;
        }

        return _fallbackDeserializer is HeaderRoutingDeserializer<T> nestedFallback
            ? nestedFallback.DeserializeWithHeaders(data, context, headers)
            : _fallbackDeserializer.Deserialize(data, context);
    }

    private static void CollectChildHeaderNames(
        List<string> names,
        IDeserializer<T> deserializer)
    {
        if (deserializer is IRecordHeaderRoutingProvider provider)
            provider.CollectHeaderNames(names);
    }

    private static void AddHeaderName(List<string> names, string headerName)
    {
        for (var index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], headerName, StringComparison.Ordinal))
                return;
        }

        names.Add(headerName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetDeserializer(ReadOnlySpan<byte> value, out IDeserializer<T> deserializer)
    {
        var hash = Hash(value);
        var low = 0;
        var high = _routes.Length - 1;
        while (low <= high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            var candidateHash = _routes[middle].Hash;
            if (candidateHash < hash)
            {
                low = middle + 1;
            }
            else if (candidateHash > hash)
            {
                high = middle - 1;
            }
            else
            {
                for (var i = middle; i >= 0 && _routes[i].Hash == hash; i--)
                {
                    if (value.SequenceEqual(_routes[i].Value))
                    {
                        deserializer = _routes[i].Deserializer;
                        return true;
                    }
                }

                for (var i = middle + 1; i < _routes.Length && _routes[i].Hash == hash; i++)
                {
                    if (value.SequenceEqual(_routes[i].Value))
                    {
                        deserializer = _routes[i].Deserializer;
                        return true;
                    }
                }

                break;
            }
        }

        deserializer = null!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Hash(ReadOnlySpan<byte> value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return hash;
    }

    private static void SortRoutes(RouteEntry[] routes)
    {
        for (var i = 1; i < routes.Length; i++)
        {
            var route = routes[i];
            var j = i - 1;
            while (j >= 0 && routes[j].Hash > route.Hash)
            {
                routes[j + 1] = routes[j];
                j--;
            }

            routes[j + 1] = route;
        }
    }

    private readonly record struct RouteEntry(ulong Hash, byte[] Value, IDeserializer<T> Deserializer);
}
