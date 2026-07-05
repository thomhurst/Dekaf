using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Dekaf.Networking;

internal sealed class ClientDnsEndpointResolver
{
    public static ClientDnsEndpointResolver Default { get; } = new(new SystemDnsLookup());

    private readonly IDnsLookup _dnsLookup;
    private readonly ConcurrentDictionary<EndpointCacheKey, IPAddress> _lastSuccessfulAddresses = new();

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

        var endpoints = addresses
            .Where(IsSupportedAddress)
            .Distinct()
            .Select(ipAddress => new ClientDnsEndpoint(ipAddress, port, targetHost))
            .ToArray();

        if (endpoints.Length == 0)
            return [];

        var key = new EndpointCacheKey(host, port, lookup);
        if (_lastSuccessfulAddresses.TryGetValue(key, out var lastSuccessful))
            MoveAddressToFront(endpoints, lastSuccessful);

        return endpoints;
    }

    public void MarkSuccessful(string host, int port, ClientDnsLookup lookup, IPAddress address)
    {
        _lastSuccessfulAddresses[new EndpointCacheKey(host, port, lookup)] = address;
    }

    private static bool IsSupportedAddress(IPAddress address)
    {
        return address.AddressFamily is AddressFamily.InterNetwork
            or AddressFamily.InterNetworkV6;
    }

    private static void MoveAddressToFront(ClientDnsEndpoint[] endpoints, IPAddress address)
    {
        var index = Array.FindIndex(endpoints, endpoint => endpoint.Address.Equals(address));
        if (index <= 0)
            return;

        var selected = endpoints[index];
        Array.Copy(endpoints, 0, endpoints, 1, index);
        endpoints[0] = selected;
    }

    private readonly record struct EndpointCacheKey(string Host, int Port, ClientDnsLookup Lookup);
}

internal readonly record struct ClientDnsEndpoint(IPAddress Address, int Port, string TargetHost);

internal interface IDnsLookup
{
    ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken);

    ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken);
}

internal sealed class SystemDnsLookup : IDnsLookup
{
    public async ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
        return await CompatibilityBcl.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
    {
        return await CompatibilityBcl.GetHostEntryAsync(host, cancellationToken).ConfigureAwait(false);
    }
}
