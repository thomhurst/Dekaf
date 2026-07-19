using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Networking;

public sealed class ClientDnsLookupTests
{
    [Test]
    public async Task ResolveAsync_UseAllDnsIps_ReturnsAllAddresses()
    {
        var resolver = new ClientDnsEndpointResolver(new StubDnsLookup(
            addresses: [IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.11")]));

        var endpoints = await resolver.ResolveAsync(
            "broker.example",
            9092,
            ClientDnsLookup.UseAllDnsIps,
            CancellationToken.None);

        await Assert.That(endpoints.Select(endpoint => endpoint.Address.ToString()).ToArray())
            .IsEquivalentTo(["192.0.2.10", "192.0.2.11"]);
        await Assert.That(endpoints.All(endpoint => endpoint.TargetHost == "broker.example")).IsTrue();
    }

    [Test]
    public async Task ResolveAsync_SuccessfulAddress_IsTriedFirstNextTime()
    {
        var secondAddress = IPAddress.Parse("192.0.2.11");
        var resolver = new ClientDnsEndpointResolver(new StubDnsLookup(
            addresses: [IPAddress.Parse("192.0.2.10"), secondAddress]));

        resolver.MarkSuccessful("broker.example", 9092, ClientDnsLookup.UseAllDnsIps, secondAddress);

        var endpoints = await resolver.ResolveAsync(
            "broker.example",
            9092,
            ClientDnsLookup.UseAllDnsIps,
            CancellationToken.None);

        await Assert.That(endpoints[0].Address).IsEqualTo(secondAddress);
    }

    [Test]
    public async Task ResolveAsync_CanonicalMode_UsesCanonicalTargetHost()
    {
        var resolver = new ClientDnsEndpointResolver(new StubDnsLookup(
            hostEntry: new IPHostEntry
            {
                HostName = "canonical.example",
                AddressList = [IPAddress.Parse("192.0.2.10")]
            }));

        var endpoints = await resolver.ResolveAsync(
            "alias.example",
            9092,
            ClientDnsLookup.ResolveCanonicalBootstrapServersOnly,
            CancellationToken.None);

        await Assert.That(endpoints.Single().TargetHost).IsEqualTo("canonical.example");
    }

    [Test]
    public async Task KafkaConnection_ConnectAsync_TriesAllResolvedAddresses()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = AcceptAndCompleteHandshakeAsync(listener);
        // Listener is IPv4-only; IPv6 loopback fails quickly without depending on
        // platform-specific routing for aliases like 127.0.0.2.
        var resolver = new ClientDnsEndpointResolver(new StubDnsLookup(
            addresses: [IPAddress.IPv6Loopback, IPAddress.Loopback]));

        await using var connection = new KafkaConnection(
            "multi.example",
            port,
            options: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(5),
                DnsResolver = resolver
            });

        await connection.ConnectAsync();
        using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
        var endpoints = await resolver.ResolveAsync(
            "multi.example",
            port,
            ClientDnsLookup.UseAllDnsIps,
            CancellationToken.None);

        await Assert.That(connection.IsConnected).IsTrue();
        await Assert.That(endpoints[0].Address).IsEqualTo(IPAddress.Loopback);
    }

    private static async Task<Socket> AcceptAndCompleteHandshakeAsync(TcpListener listener)
    {
        var socket = await listener.AcceptSocketAsync();
        try
        {
            var stream = new NetworkStream(socket, ownsSocket: false);
            var length = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(length);
            var request = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
            await stream.ReadExactlyAsync(request);

            var body = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(body);
            writer.WriteInt16(0);
            writer.WriteUnsignedVarInt(2);
            writer.WriteInt16((short)ApiKey.ApiVersions);
            writer.WriteInt16(0);
            writer.WriteInt16(3);
            writer.WriteEmptyTaggedFields();
            writer.WriteInt32(0);
            writer.WriteEmptyTaggedFields();

            var response = new byte[sizeof(int) * 2 + body.WrittenCount];
            BinaryPrimitives.WriteInt32BigEndian(response, response.Length - sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(
                response.AsSpan(sizeof(int)),
                BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4)));
            body.WrittenSpan.CopyTo(response.AsSpan(sizeof(int) * 2));
            await stream.WriteAsync(response);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private sealed class StubDnsLookup : IDnsLookup
    {
        private readonly IPAddress[] _addresses;
        private readonly IPHostEntry _hostEntry;

        public StubDnsLookup(IPAddress[]? addresses = null, IPHostEntry? hostEntry = null)
        {
            _addresses = addresses ?? hostEntry?.AddressList ?? [];
            _hostEntry = hostEntry ?? new IPHostEntry
            {
                HostName = "broker.example",
                AddressList = _addresses
            };
        }

        public ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_addresses);
        }

        public ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_hostEntry);
        }
    }
}
