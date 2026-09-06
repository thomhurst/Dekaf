using System.Net;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public sealed class DnsPreferenceLifetimeTests
{
    private static readonly IPAddress First = IPAddress.Parse("192.0.2.10");
    private static readonly IPAddress Second = IPAddress.Parse("192.0.2.11");

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostnameChurn_RetainsAtMost1024Preferences(bool concurrent)
    {
        var resolver = new ClientDnsEndpointResolver(new Lookup());
        var hosts = Enumerable.Range(0, 4096).Select(index => $"broker-{index}.example").ToArray();
        if (concurrent)
            Parallel.For(0, hosts.Length, index => resolver.MarkSuccessful(hosts[index], 9092, ClientDnsLookup.UseAllDnsIps, Second));
        else
            foreach (var host in hosts)
                resolver.MarkSuccessful(host, 9092, ClientDnsLookup.UseAllDnsIps, Second);

        // Count externally observable preferences, independent of cache representation/hash collisions.
        var retained = 0;
        foreach (var host in hosts)
        {
            var endpoints = await resolver.ResolveAsync(host, 9092, ClientDnsLookup.UseAllDnsIps, default);
            if (endpoints[0].Address.Equals(Second))
                retained++;
        }
        await Assert.That(retained).IsGreaterThan(0);
        await Assert.That(retained).IsLessThanOrEqualTo(1024);

        resolver.MarkSuccessful("most-recent.example", 9092, ClientDnsLookup.UseAllDnsIps, Second);
        var latest = await resolver.ResolveAsync("most-recent.example", 9092, ClientDnsLookup.UseAllDnsIps, default);
        await Assert.That(latest[0].Address).IsEqualTo(Second);
    }

    [Test]
    public async Task DuplicateAddresses_PreserveDnsOrderAndUniqueEndpoints()
    {
        var lookup = new Lookup { Addresses = [First, IPAddress.Parse("192.0.2.10"), Second, Second] };
        var resolver = new ClientDnsEndpointResolver(lookup);
        var endpoints = await resolver.ResolveAsync("duplicates.example", 9092, ClientDnsLookup.UseAllDnsIps, default);
        await Assert.That(endpoints.Count).IsEqualTo(2);
        await Assert.That(endpoints[0].Address).IsEqualTo(First);
        await Assert.That(endpoints[1].Address).IsEqualTo(Second);

        resolver.MarkSuccessful("duplicates.example", 9092, ClientDnsLookup.UseAllDnsIps, Second);
        endpoints = await resolver.ResolveAsync("duplicates.example", 9092, ClientDnsLookup.UseAllDnsIps, default);
        await Assert.That(endpoints.Count).IsEqualTo(2);
        await Assert.That(endpoints[0].Address).IsEqualTo(Second);
        await Assert.That(endpoints[1].Address).IsEqualTo(First);
    }

    [Test]
    public async Task RepeatedSuccess_UpdatesExistingPreference()
    {
        var resolver = new ClientDnsEndpointResolver(new Lookup());
        for (var index = 0; index < 10000; index++)
            resolver.MarkSuccessful("stable.example", 9092, ClientDnsLookup.UseAllDnsIps, index % 2 == 0 ? First : Second);
        var endpoints = await resolver.ResolveAsync("stable.example", 9092, ClientDnsLookup.UseAllDnsIps, default);
        await Assert.That(endpoints[0].Address).IsEqualTo(Second);
    }

    [Test]
    public async Task Preference_OnlyAppliesToCurrentDnsResultAndMatchingEndpoint()
    {
        var lookup = new Lookup();
        var resolver = new ClientDnsEndpointResolver(lookup);
        resolver.MarkSuccessful("alias.example", 9092, ClientDnsLookup.UseAllDnsIps, Second);
        var otherPort = await resolver.ResolveAsync("alias.example", 9093, ClientDnsLookup.UseAllDnsIps, default);
        var canonical = await resolver.ResolveAsync("alias.example", 9092, ClientDnsLookup.ResolveCanonicalBootstrapServersOnly, default);
        await Assert.That(otherPort[0].Address).IsEqualTo(First);
        await Assert.That(canonical[0].Address).IsEqualTo(First);
        await Assert.That(canonical[0].TargetHost).IsEqualTo("canonical.example");

        lookup.Addresses = [First];
        var changed = await resolver.ResolveAsync("alias.example", 9092, ClientDnsLookup.UseAllDnsIps, default);
        await Assert.That(changed.Count).IsEqualTo(1);
        await Assert.That(changed[0].Address).IsEqualTo(First);
    }

    private sealed class Lookup : IDnsLookup
    {
        internal IPAddress[] Addresses { get; set; } = [First, Second];
        public ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken) => ValueTask.FromResult(Addresses);
        public ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new IPHostEntry { HostName = "canonical.example", AddressList = Addresses });
    }
}
