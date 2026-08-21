using System.Collections.Frozen;

namespace Dekaf.Serialization.Routing;

internal sealed class FrozenRouteTable<TKey, TRoute>(IEqualityComparer<TKey>? comparer = null)
    where TKey : notnull
    where TRoute : class
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, TRoute> _registrations = new(comparer);
    private FrozenDictionary<TKey, TRoute>? _routes;
    private TRoute? _fallback;
    private bool _producesRecordHeaders;

    internal bool IsFrozen => Volatile.Read(ref _routes) is not null;
    internal bool ProducesRecordHeaders => Volatile.Read(ref _producesRecordHeaders);

    internal void Register(TKey key, TRoute route)
    {
        lock (_gate)
        {
            if (_routes is not null)
                throw new InvalidOperationException("Routes cannot be changed after Freeze().");
            if (!_registrations.TryAdd(key, route))
                throw new InvalidOperationException("A route is already registered for the supplied key.");
            if (route is IRecordHeaderSerializer { ProducesRecordHeaders: true })
                Volatile.Write(ref _producesRecordHeaders, true);
        }
    }

    internal void SetFallback(TRoute route)
    {
        lock (_gate)
        {
            if (_routes is not null)
                throw new InvalidOperationException("Routes cannot be changed after Freeze().");
            _fallback = route;
            if (route is IRecordHeaderSerializer { ProducesRecordHeaders: true })
                Volatile.Write(ref _producesRecordHeaders, true);
        }
    }

    internal void Freeze()
    {
        lock (_gate)
        {
            if (_routes is null)
            {
                var producesRecordHeaders =
                    _fallback is IRecordHeaderSerializer { ProducesRecordHeaders: true };
                if (!producesRecordHeaders)
                {
                    foreach (var route in _registrations.Values)
                    {
                        if (route is IRecordHeaderSerializer { ProducesRecordHeaders: true })
                        {
                            producesRecordHeaders = true;
                            break;
                        }
                    }
                }

                Volatile.Write(ref _producesRecordHeaders, producesRecordHeaders);
                Volatile.Write(
                    ref _routes,
                    _registrations.ToFrozenDictionary(comparer: _registrations.Comparer));
            }
        }
    }

    internal bool TryGetRoute(TKey key, out TRoute route)
    {
        var routes = Volatile.Read(ref _routes)
            ?? throw new InvalidOperationException("Freeze() must be called before routing data.");
        if (routes.TryGetValue(key, out route!))
            return true;

        route = _fallback!;
        return route is not null;
    }

    internal void CollectHeaderNames(List<string> names)
    {
        lock (_gate)
        {
            if (_fallback is IRecordHeaderRoutingProvider fallbackProvider)
                fallbackProvider.CollectHeaderNames(names);

            foreach (var route in _registrations.Values)
            {
                if (route is IRecordHeaderRoutingProvider provider)
                    provider.CollectHeaderNames(names);
            }
        }
    }
}
