using System.Net;
using System.Net.Sockets;

namespace Dekaf.Networking;

internal sealed class ClientDnsEndpointResolver
{
    public static ClientDnsEndpointResolver Default { get; } = new(new SystemDnsLookup());

    private readonly IDnsLookup _dnsLookup;
    // Preferences are connection-time hints, not cached DNS results. Four entries per bucket
    // bound retained hosts and lookup work; reused slots avoid allocations during steady churn.
    private const int PreferenceBucketCount = 256;
    private readonly PreferenceBucket?[] _successfulPreferences = new PreferenceBucket?[PreferenceBucketCount];

    public ClientDnsEndpointResolver(IDnsLookup dnsLookup)
    {
        _dnsLookup = dnsLookup;
    }

    public async ValueTask<IReadOnlyList<ClientDnsEndpoint>> ResolveAsync(
        string host,
        int port,
        ClientDnsLookup lookup,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var address))
            return [new ClientDnsEndpoint(address, port, host)];

        var targetHost = host;
        IPAddress[] addresses;

        if (lookup == ClientDnsLookup.ResolveCanonicalBootstrapServersOnly)
        {
            var entry = await _dnsLookup.GetHostEntryAsync(host, cancellationToken).ConfigureAwait(false);
            targetHost = string.IsNullOrWhiteSpace(entry.HostName) ? host : entry.HostName;
            addresses = entry.AddressList;
        }
        else
        {
            addresses = await _dnsLookup.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }

        if (addresses.Length == 0)
            return [];

        if (addresses.Length == 1)
            return IsSupportedAddress(addresses[0]) ? [new ClientDnsEndpoint(addresses[0], port, targetHost)] : [];

        var seen = new HashSet<IPAddress>();
        for (var index = 0; index < addresses.Length; index++)
        {
            var candidate = addresses[index];
            if (IsSupportedAddress(candidate))
                seen.Add(candidate);
        }
        if (seen.Count == 0)
            return [];

        // Size by unique supported addresses, then retain their original DNS order.
        var endpoints = new ClientDnsEndpoint[seen.Count];
        var count = 0;
        for (var index = 0; index < addresses.Length; index++)
        {
            var candidate = addresses[index];
            if (IsSupportedAddress(candidate) && seen.Remove(candidate))
            {
                endpoints[count++] = new ClientDnsEndpoint(candidate, port, targetHost);
                if (count == endpoints.Length)
                    break;
            }
        }

        var key = new EndpointCacheKey(host, port, lookup);
        var hash = key.GetHashCode();
        var bucket = Volatile.Read(ref _successfulPreferences[hash & (PreferenceBucketCount - 1)]);
        if (bucket?.GetAddress(key, hash) is { } lastSuccessful)
            MoveAddressToFront(endpoints, lastSuccessful);

        return endpoints;
    }

    public void MarkSuccessful(string host, int port, ClientDnsLookup lookup, IPAddress address)
    {
        var key = new EndpointCacheKey(host, port, lookup);
        var hash = key.GetHashCode();
        var index = hash & (PreferenceBucketCount - 1);
        var bucket = Volatile.Read(ref _successfulPreferences[index]);
        if (bucket is null)
        {
            var created = new PreferenceBucket();
            bucket = Interlocked.CompareExchange(ref _successfulPreferences[index], created, null) ?? created;
        }
        bucket.SetAddress(key, hash, address);
    }

    private static bool IsSupportedAddress(IPAddress address)
    {
        return address.AddressFamily is AddressFamily.InterNetwork
            or AddressFamily.InterNetworkV6;
    }

    private static void MoveAddressToFront(ClientDnsEndpoint[] endpoints, IPAddress address)
    {
        for (var index = 0; index < endpoints.Length; index++)
        {
            if (!endpoints[index].Address.Equals(address))
                continue;

            if (index > 0)
            {
                var selected = endpoints[index];
                Array.Copy(endpoints, 0, endpoints, 1, index);
                endpoints[0] = selected;
            }
            return;
        }
    }

    private readonly record struct EndpointCacheKey(string Host, int Port, ClientDnsLookup Lookup);

    private sealed class PreferenceBucket
    {
        private readonly PreferenceEntry[] _entries = new PreferenceEntry[4];
        private int _count;
        private int _next;

        public IPAddress? GetAddress(EndpointCacheKey key, int hash)
        {
            // DNS preferences are consulted only while establishing connections. A bucket lock
            // keeps a reused slot's endpoint key and address coherent for concurrent clients.
            lock (this)
            {
                for (var index = 0; index < _count; index++)
                {
                    if (_entries[index].Hash == hash && _entries[index].Key == key)
                        return _entries[index].Address;
                }
                return null;
            }
        }

        public void SetAddress(EndpointCacheKey key, int hash, IPAddress address)
        {
            lock (this)
            {
                for (var index = 0; index < _count; index++)
                {
                    if (_entries[index].Hash == hash && _entries[index].Key == key)
                    {
                        _entries[index] = new PreferenceEntry(hash, key, address);
                        return;
                    }
                }

                // FIFO within the bucket: updating a preference does not consume another slot.
                _entries[_next] = new PreferenceEntry(hash, key, address);
                _next = (_next + 1) & 3;
                if (_count < _entries.Length)
                    _count++;
            }
        }
    }

    private readonly record struct PreferenceEntry(int Hash, EndpointCacheKey Key, IPAddress Address);
}

internal readonly record struct ClientDnsEndpoint(IPAddress Address, int Port, string TargetHost);

internal sealed class DnsResolutionException(string host, int port, Exception? innerException = null)
    : Exception($"DNS resolution failed for host '{host}:{port}'.", innerException)
{
    public string Host { get; } = host;

    public int Port { get; } = port;
}

internal interface IDnsLookup
{
    ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken);

    ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken);
}

internal sealed class SystemDnsLookup : IDnsLookup
{
    public async ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
        return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
    {
        return await Dns.GetHostEntryAsync(host, cancellationToken).ConfigureAwait(false);
    }
}
