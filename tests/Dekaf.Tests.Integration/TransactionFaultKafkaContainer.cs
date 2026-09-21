using Dekaf.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Testcontainers.Toxiproxy;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Owns an isolated Kafka/Toxiproxy topology for transaction coordinator and network fault tests.
/// Producer and consumer traffic use separate proxy listeners so a fault on one lane does not
/// disrupt the verification lane or any shared integration-test container.
/// </summary>
public sealed class TransactionFaultKafkaContainer : KafkaTestContainer
{
    private const string KafkaNetworkAlias = "transaction-fault-kafka";
    private const string ProducerProxyName = "transaction-producer";
    private const string ConsumerProxyName = "transaction-consumer";
    private const string CoordinatorFaultName = "coordinator-latency";

    private const ushort ProducerProxyPort = ToxiproxyBuilder.FirstProxiedPort;
    private const ushort ConsumerProxyPort = ToxiproxyBuilder.FirstProxiedPort + 1;
    private const ushort KafkaProducerPort = 19_092;
    private const ushort KafkaConsumerPort = 19_093;
    private const ushort KafkaBrokerPort = 19_094;
    private const ushort KafkaControllerPort = 19_095;

    public override string ContainerName => "apache/kafka:4.0.2";

    public override int Version => 402;

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly ToxiproxyContainer _toxiproxy;
    private readonly HttpClient _toxiproxyClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
    private readonly object _activeToxicsLock = new();
    private readonly List<(string Proxy, string Toxic)> _activeToxics = [];
    private IContainer? _kafka;
    private bool _faultActive;

    public TransactionFaultKafkaContainer()
    {
        _toxiproxy = new ToxiproxyBuilder("ghcr.io/shopify/toxiproxy:2.12.0")
            .WithNetwork(_network)
            .Build();
    }

    public string ProducerBootstrapServers { get; private set; } = string.Empty;

    public string ConsumerBootstrapServers { get; private set; } = string.Empty;

    public override async Task InitializeAsync()
    {
        await _network.CreateAsync().ConfigureAwait(false);
        await _toxiproxy.StartAsync().ConfigureAwait(false);

        var toxiproxyHost = _toxiproxy.Hostname;
        var producerPublicPort = _toxiproxy.GetMappedPublicPort(ProducerProxyPort);
        var consumerPublicPort = _toxiproxy.GetMappedPublicPort(ConsumerProxyPort);
        ProducerBootstrapServers = $"{toxiproxyHost}:{producerPublicPort}";
        ConsumerBootstrapServers = $"{toxiproxyHost}:{consumerPublicPort}";
        BootstrapServers = ProducerBootstrapServers;

        _toxiproxyClient.BaseAddress = new Uri(
            $"http://{toxiproxyHost}:{_toxiproxy.GetMappedPublicPort(ToxiproxyBuilder.ToxiproxyControlPort)}/");

        await AddProxyAsync(
            ProducerProxyName,
            ProducerProxyPort,
            KafkaProducerPort).ConfigureAwait(false);
        await AddProxyAsync(
            ConsumerProxyName,
            ConsumerProxyPort,
            KafkaConsumerPort).ConfigureAwait(false);

        _kafka = CreateKafkaContainer(toxiproxyHost, producerPublicPort, consumerPublicPort);
        await _kafka.StartAsync().ConfigureAwait(false);
        await WaitForAdminReadyAsync("transaction fault proxy").ConfigureAwait(false);
    }

    public async Task AddCoordinatorLatencyAsync(
        CancellationToken cancellationToken,
        int latencyMs = 5_000)
    {
        var toxic = new ToxiproxyLatencyConfiguration(
            CoordinatorFaultName,
            "latency",
            "downstream",
            1,
            new ToxiproxyLatencyAttributes(latencyMs, 0));
        using var response = await _toxiproxyClient.PostAsJsonAsync(
            $"proxies/{ProducerProxyName}/toxics",
            toxic,
            ToxiproxyJsonContext.Default.ToxiproxyLatencyConfiguration,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        _faultActive = true;
    }

    public async Task HealCoordinatorAsync(CancellationToken cancellationToken = default)
    {
        if (!_faultActive)
        {
            return;
        }

        using var response = await _toxiproxyClient.DeleteAsync(
            $"proxies/{ProducerProxyName}/toxics/{CoordinatorFaultName}",
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        _faultActive = false;
    }

    /// <summary>
    /// Resets connections on the lane (TCP RST, "connection reset by peer"), including every
    /// connection opened while the toxic is active. <paramref name="afterMs"/> of 0 resets at once.
    /// </summary>
    public Task AddResetPeerAsync(
        ToxiproxyLane lane,
        CancellationToken cancellationToken,
        int afterMs = 0,
        ToxiproxyDirection direction = ToxiproxyDirection.Downstream) =>
        AddToxicAsync(lane, "reset_peer", direction, new ToxiproxyToxicAttributes(Timeout: afterMs), cancellationToken);

    /// <summary>
    /// Black-holes the lane: the proxy still accepts TCP connections but no data gets through, so
    /// connection setup (ApiVersions) and in-flight requests run into their own timeouts. This is
    /// the path a killed broker never reaches, because a refused connection fails fast.
    /// <paramref name="closeAfterMs"/> of 0 keeps connections open until the toxic is removed.
    /// </summary>
    public Task AddTimeoutAsync(
        ToxiproxyLane lane,
        CancellationToken cancellationToken,
        int closeAfterMs = 0,
        ToxiproxyDirection direction = ToxiproxyDirection.Downstream) =>
        AddToxicAsync(lane, "timeout", direction, new ToxiproxyToxicAttributes(Timeout: closeAfterMs), cancellationToken);

    /// <summary>
    /// Closes each connection once it has carried <paramref name="bytes"/> in the given direction.
    /// Downstream with a limit smaller than a response cuts the connection mid-frame.
    /// </summary>
    public Task AddLimitDataAsync(
        ToxiproxyLane lane,
        long bytes,
        CancellationToken cancellationToken,
        ToxiproxyDirection direction = ToxiproxyDirection.Downstream) =>
        AddToxicAsync(lane, "limit_data", direction, new ToxiproxyToxicAttributes(Bytes: bytes), cancellationToken);

    /// <summary>
    /// Removes every toxic added through <see cref="AddResetPeerAsync"/>,
    /// <see cref="AddTimeoutAsync"/> and <see cref="AddLimitDataAsync"/>. Safe to call when none is active.
    /// </summary>
    public async Task HealNetworkFaultsAsync(CancellationToken cancellationToken = default)
    {
        (string Proxy, string Toxic)[] toxics;
        lock (_activeToxicsLock)
        {
            toxics = [.. _activeToxics];
        }

        foreach (var (proxy, toxic) in toxics)
        {
            using var response = await _toxiproxyClient.DeleteAsync(
                $"proxies/{proxy}/toxics/{toxic}",
                cancellationToken).ConfigureAwait(false);
            // A toxic that is already gone is healed; anything else must not be mistaken for it.
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();

            lock (_activeToxicsLock)
            {
                _activeToxics.Remove((proxy, toxic));
            }
        }
    }

    private async Task AddToxicAsync(
        ToxiproxyLane lane,
        string type,
        ToxiproxyDirection direction,
        ToxiproxyToxicAttributes attributes,
        CancellationToken cancellationToken)
    {
        var proxy = lane == ToxiproxyLane.Producer ? ProducerProxyName : ConsumerProxyName;
        var streamName = direction == ToxiproxyDirection.Upstream ? "upstream" : "downstream";
        var name = $"{type}-{streamName}";

        // Tracked before the request so a request that fails after the proxy applied it is still healed.
        lock (_activeToxicsLock)
        {
            if (!_activeToxics.Contains((proxy, name)))
                _activeToxics.Add((proxy, name));
        }

        using var response = await _toxiproxyClient.PostAsJsonAsync(
            $"proxies/{proxy}/toxics",
            new ToxiproxyToxicConfiguration(name, type, streamName, 1, attributes),
            ToxiproxyJsonContext.Default.ToxiproxyToxicConfiguration,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public override async ValueTask DisposeAsync()
    {
        var cleanup = new CleanupFailureCollector();
        await cleanup.CaptureTaskAsync("coordinator fault cleanup", () => HealCoordinatorAsync())
            .ConfigureAwait(false);
        await cleanup.CaptureTaskAsync("network fault cleanup", () => HealNetworkFaultsAsync())
            .ConfigureAwait(false);
        cleanup.Capture("Toxiproxy HTTP client disposal", _toxiproxyClient.Dispose);

        if (_kafka is not null)
        {
            await cleanup.CaptureValueTaskAsync("Kafka container disposal", _kafka.DisposeAsync)
                .ConfigureAwait(false);
        }

        await cleanup.CaptureValueTaskAsync("Toxiproxy container disposal", _toxiproxy.DisposeAsync)
            .ConfigureAwait(false);
        await cleanup.CaptureValueTaskAsync("transaction network disposal", _network.DisposeAsync)
            .ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
        cleanup.ThrowIfAny();
    }

    private IContainer CreateKafkaContainer(
        string toxiproxyHost,
        ushort producerPublicPort,
        ushort consumerPublicPort)
    {
        return new ContainerBuilder("apache/kafka:4.0.2")
            .WithNetwork(_network)
            .WithNetworkAliases(KafkaNetworkAlias)
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment(
                "KAFKA_LISTENERS",
                $"PRODUCER://0.0.0.0:{KafkaProducerPort}," +
                $"CONSUMER://0.0.0.0:{KafkaConsumerPort}," +
                $"BROKER://0.0.0.0:{KafkaBrokerPort}," +
                $"CONTROLLER://0.0.0.0:{KafkaControllerPort}")
            .WithEnvironment(
                "KAFKA_ADVERTISED_LISTENERS",
                $"PRODUCER://{toxiproxyHost}:{producerPublicPort}," +
                $"CONSUMER://{toxiproxyHost}:{consumerPublicPort}," +
                $"BROKER://{KafkaNetworkAlias}:{KafkaBrokerPort}")
            .WithEnvironment(
                "KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
                "PRODUCER:PLAINTEXT,CONSUMER:PLAINTEXT,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
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
    }

    public override IAdminClient CreateAdminClient() => Kafka.CreateAdminClient()
        .WithBootstrapServers(ProducerBootstrapServers)
        .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
        .Build();

    protected override async Task<string> TryGetBrokerLogTailAsync()
    {
        return _kafka is null
            ? string.Empty
            : await TryGetContainerLogTailAsync(_kafka).ConfigureAwait(false);
    }

    private async Task AddProxyAsync(string name, ushort listenPort, ushort upstreamPort)
    {
        var proxy = new ToxiproxyProxyConfiguration(
            name,
            $"0.0.0.0:{listenPort}",
            $"{KafkaNetworkAlias}:{upstreamPort}",
            true);
        using var response = await _toxiproxyClient.PostAsJsonAsync(
            "proxies",
            proxy,
            ToxiproxyJsonContext.Default.ToxiproxyProxyConfiguration).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

}

internal sealed record ToxiproxyProxyConfiguration(
    string Name,
    string Listen,
    string Upstream,
    bool Enabled);

internal sealed record ToxiproxyLatencyConfiguration(
    string Name,
    string Type,
    string Stream,
    double Toxicity,
    ToxiproxyLatencyAttributes Attributes);

internal sealed record ToxiproxyLatencyAttributes(int Latency, int Jitter);

internal sealed record ToxiproxyToxicConfiguration(
    string Name,
    string Type,
    string Stream,
    double Toxicity,
    ToxiproxyToxicAttributes Attributes);

/// <summary>Only the attribute the toxic type uses is set; the rest are omitted from the request.</summary>
internal sealed record ToxiproxyToxicAttributes(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Timeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Bytes = null);

/// <summary>Which proxied listener a network fault applies to.</summary>
public enum ToxiproxyLane
{
    Producer,
    Consumer,
}

/// <summary>Upstream is client to broker (requests); downstream is broker to client (responses).</summary>
public enum ToxiproxyDirection
{
    Upstream,
    Downstream,
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ToxiproxyProxyConfiguration))]
[JsonSerializable(typeof(ToxiproxyLatencyConfiguration))]
[JsonSerializable(typeof(ToxiproxyToxicConfiguration))]
internal sealed partial class ToxiproxyJsonContext : JsonSerializerContext;
