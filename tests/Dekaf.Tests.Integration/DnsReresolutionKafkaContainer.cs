using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

public sealed class DnsReresolutionKafkaContainer : IAsyncInitializer, IAsyncDisposable
{
    private const ushort KafkaClientPort = 19_092;
    private const ushort KafkaBrokerPort = 19_093;
    private const ushort KafkaControllerPort = 19_094;
    private const string KafkaNetworkAlias = "dns-reresolution-kafka";

    private IContainer? _kafka;
    private TcpRelay? _primaryRelay;
    private TcpRelay? _secondaryRelay;
    private int _relayPort;
    private string? _upstreamHost;
    private int _upstreamPort;

    public const string StableHost = "dns-reresolution.test";
    public static IPAddress PrimaryAddress { get; } = IPAddress.Parse("127.0.0.2");
    public static IPAddress SecondaryAddress { get; } = IPAddress.Parse("127.0.0.3");
    public static IPAddress DeadAddress { get; } = IPAddress.Parse("127.0.0.4");

    public string BootstrapServers => $"{StableHost}:{_relayPort}";
    public int PrimaryAcceptedConnections => _primaryRelay?.AcceptedConnections ?? 0;
    public int SecondaryAcceptedConnections => _secondaryRelay?.AcceptedConnections ?? 0;

    public async Task InitializeAsync()
    {
        await StartRelaysAsync().ConfigureAwait(false);

        _kafka = new ContainerBuilder($"apache/kafka:{KafkaContainerDefault.ImageTag}")
            .WithNetworkAliases(KafkaNetworkAlias)
            .WithPortBinding(KafkaClientPort, true)
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment(
                "KAFKA_LISTENERS",
                $"CLIENT://0.0.0.0:{KafkaClientPort}," +
                $"BROKER://0.0.0.0:{KafkaBrokerPort}," +
                $"CONTROLLER://0.0.0.0:{KafkaControllerPort}")
            .WithEnvironment(
                "KAFKA_ADVERTISED_LISTENERS",
                $"CLIENT://{StableHost}:{_relayPort}," +
                $"BROKER://{KafkaNetworkAlias}:{KafkaBrokerPort}")
            .WithEnvironment(
                "KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
                "CLIENT:PLAINTEXT,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "BROKER")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", $"1@localhost:{KafkaControllerPort}")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithEnvironment("CLUSTER_ID", "4L6g3nShT-eMCtK--X86sw")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged(".*Transitioning from RECOVERY to RUNNING.*"))
            .Build();

        await _kafka.StartAsync().ConfigureAwait(false);
        _upstreamHost = _kafka.Hostname;
        _upstreamPort = _kafka.GetMappedPublicPort(KafkaClientPort);
        SetRelayTargets();
        await WaitForBrokerAsync().ConfigureAwait(false);
    }

    public async ValueTask ResetRelaysAsync()
    {
        await DisposeRelaysAsync().ConfigureAwait(false);
        await StartRelaysAsync(_relayPort).ConfigureAwait(false);
        SetRelayTargets();
    }

    public ValueTask StopPrimaryRelayAsync() => _primaryRelay?.DisposeAsync() ?? default;

    public async ValueTask DisposeAsync()
    {
        await DisposeRelaysAsync().ConfigureAwait(false);
        if (_kafka is not null)
            await _kafka.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask StartRelaysAsync(int port = 0)
    {
        _primaryRelay = new TcpRelay(PrimaryAddress, port);
        _relayPort = _primaryRelay.Port;
        try
        {
            _secondaryRelay = new TcpRelay(SecondaryAddress, _relayPort);
        }
        catch
        {
            await _primaryRelay.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void SetRelayTargets()
    {
        if (_upstreamHost is null)
            return;

        _primaryRelay?.SetTarget(_upstreamHost, _upstreamPort);
        _secondaryRelay?.SetTarget(_upstreamHost, _upstreamPort);
    }

    private async ValueTask DisposeRelaysAsync()
    {
        if (_primaryRelay is not null)
            await _primaryRelay.DisposeAsync().ConfigureAwait(false);
        if (_secondaryRelay is not null)
            await _secondaryRelay.DisposeAsync().ConfigureAwait(false);
        _primaryRelay = null;
        _secondaryRelay = null;
    }

    private async Task WaitForBrokerAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_upstreamHost!, _upstreamPort, timeout.Token).ConfigureAwait(false);
                return;
            }
            catch (SocketException) when (!timeout.IsCancellationRequested)
            {
                await Task.Delay(100, timeout.Token).ConfigureAwait(false);
            }
        }
    }

    private sealed class TcpRelay : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();
        private readonly ConcurrentDictionary<long, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<long, Task> _connections = new();
        private readonly Task _acceptTask;
        private string? _targetHost;
        private int _targetPort;
        private long _nextConnectionId;
        private int _acceptedConnections;
        private int _disposeState;

        public TcpRelay(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptTask = AcceptAsync();
        }

        public int Port { get; }
        public int AcceptedConnections => Volatile.Read(ref _acceptedConnections);

        public void SetTarget(string host, int port)
        {
            _targetHost = host;
            _targetPort = port;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            _stopping.Cancel();
            _listener.Stop();
            foreach (var client in _clients.Values)
                client.Dispose();

            await _acceptTask.ConfigureAwait(false);
            if (!_connections.IsEmpty)
                await Task.WhenAll(_connections.Values).ConfigureAwait(false);
            _stopping.Dispose();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stopping.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                    var id = Interlocked.Increment(ref _nextConnectionId);
                    _clients[id] = client;
                    Interlocked.Increment(ref _acceptedConnections);
                    var task = RelayAsync(id, client);
                    _connections[id] = task;
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
            catch (SocketException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        private async Task RelayAsync(long id, TcpClient inbound)
        {
            using (inbound)
            using (var outbound = new TcpClient())
            {
                try
                {
                    var targetHost = _targetHost ?? throw new InvalidOperationException("Relay target not ready");
                    await outbound.ConnectAsync(targetHost, _targetPort, _stopping.Token).ConfigureAwait(false);
                    _clients[-id] = outbound;

                    var inboundStream = inbound.GetStream();
                    var outboundStream = outbound.GetStream();
                    var upstream = inboundStream.CopyToAsync(outboundStream, _stopping.Token);
                    var downstream = outboundStream.CopyToAsync(inboundStream, _stopping.Token);
                    await Task.WhenAny(upstream, downstream).ConfigureAwait(false);
                    inbound.Dispose();
                    outbound.Dispose();
                    await Task.WhenAll(upstream, downstream).ConfigureAwait(false);
                }
                catch (Exception) when (_stopping.IsCancellationRequested)
                {
                }
                catch (IOException)
                {
                }
                catch (SocketException)
                {
                }
                finally
                {
                    _clients.TryRemove(id, out _);
                    _clients.TryRemove(-id, out _);
                    _connections.TryRemove(id, out _);
                }
            }
        }
    }
}

internal sealed class SwitchingDnsLookup(params IPAddress[] addresses) : IDnsLookup
{
    private IPAddress[] _addresses = addresses;
    private int _invocationCount;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public void Use(params IPAddress[] currentAddresses) => Volatile.Write(ref _addresses, currentAddresses);

    public ValueTask<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocationCount);
        return ValueTask.FromResult(Volatile.Read(ref _addresses));
    }

    public ValueTask<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocationCount);
        return ValueTask.FromResult(new IPHostEntry
        {
            HostName = host,
            AddressList = Volatile.Read(ref _addresses),
            Aliases = []
        });
    }
}
