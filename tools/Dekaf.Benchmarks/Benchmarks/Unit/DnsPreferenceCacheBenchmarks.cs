using System.Net;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Connection-time DNS preference maintenance for a stable broker set and hostname churn.
/// DNS lookup is stubbed so network latency cannot hide cache costs.
/// </summary>
[MemoryDiagnoser]
public class DnsPreferenceCacheBenchmarks
{
    private ClientDnsEndpointResolver _resolver = null!;
    private string[] _hosts = null!;
    private readonly IPAddress _successful = IPAddress.Parse("192.0.2.11");
    private int _index;

    [Params(32, 8192)]
    public int HostCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _resolver = new ClientDnsEndpointResolver(new Lookup(_successful));
        _hosts = new string[HostCount];
        for (var index = 0; index < _hosts.Length; index++)
        {
            _hosts[index] = $"broker-{index}.example";
            _resolver.MarkSuccessful(_hosts[index], 9092, ClientDnsLookup.UseAllDnsIps, _successful);
        }
    }

    [Benchmark]
    public void MarkSuccessful() => _resolver.MarkSuccessful(
        _hosts[_index++ & (HostCount - 1)], 9092, ClientDnsLookup.UseAllDnsIps, _successful);

    [Benchmark]
    public IPAddress Resolve() => _resolver.ResolveAsync(
        _hosts[_index++ & (HostCount - 1)], 9092, ClientDnsLookup.UseAllDnsIps, default).GetAwaiter().GetResult()[0].Address;

    private sealed class Lookup(IPAddress successful) : IDnsLookup
    {
        private readonly IPAddress[] _addresses = [IPAddress.Parse("192.0.2.10"), successful];
        public ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken) => ValueTask.FromResult(_addresses);
        public ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
