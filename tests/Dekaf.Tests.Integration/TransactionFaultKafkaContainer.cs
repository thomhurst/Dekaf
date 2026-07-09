using Dekaf.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Testcontainers.Toxiproxy;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Owns an isolated Kafka/Toxiproxy topology for transaction coordinator fault tests.
/// Producer and consumer traffic use separate proxy listeners so coordinator faults do not
/// disrupt the read-committed verification lane or any shared integration-test container.
/// </summary>
public sealed class TransactionFaultKafkaContainer : IAsyncInitializer, IAsyncDisposable
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

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly ToxiproxyContainer _toxiproxy;
    private readonly HttpClient _toxiproxyClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
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

    public async Task InitializeAsync()
    {
        await _network.CreateAsync().ConfigureAwait(false);
        await _toxiproxy.StartAsync().ConfigureAwait(false);

        var toxiproxyHost = _toxiproxy.Hostname;
        var producerPublicPort = _toxiproxy.GetMappedPublicPort(ProducerProxyPort);
        var consumerPublicPort = _toxiproxy.GetMappedPublicPort(ConsumerProxyPort);
        ProducerBootstrapServers = $"{toxiproxyHost}:{producerPublicPort}";
        ConsumerBootstrapServers = $"{toxiproxyHost}:{consumerPublicPort}";

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
        await WaitForKafkaAsync().ConfigureAwait(false);
    }

    public async Task<string> CreateTestTopicAsync()
    {
        var topic = $"transaction-fault-{Guid.NewGuid():N}";
        await using var admin = CreateAdminClient();
        await admin.CreateTopicsAsync(
        [
            new NewTopic
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1,
            },
        ]).ConfigureAwait(false);
        return topic;
    }

    public async Task AddCoordinatorLatencyAsync(CancellationToken cancellationToken)
    {
        var toxic = new ToxiproxyLatencyConfiguration(
            CoordinatorFaultName,
            "latency",
            "downstream",
            1,
            new ToxiproxyLatencyAttributes(5_000, 0));
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

    public async ValueTask DisposeAsync()
    {
        try
        {
            await HealCoordinatorAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TransactionFaultKafkaContainer] Fault cleanup failed: {ex.Message}");
        }

        _toxiproxyClient.Dispose();

        if (_kafka is not null)
        {
            await _kafka.DisposeAsync().ConfigureAwait(false);
        }

        await _toxiproxy.DisposeAsync().ConfigureAwait(false);
        await _network.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private IContainer CreateKafkaContainer(
        string toxiproxyHost,
        ushort producerPublicPort,
        ushort consumerPublicPort)
    {
        return new ContainerBuilder("apache/kafka:4.0.1")
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

    private IAdminClient CreateAdminClient() => Kafka.CreateAdminClient()
        .WithBootstrapServers(ProducerBootstrapServers)
        .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
        .Build();

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

    private async Task WaitForKafkaAsync()
    {
        Exception? lastError = null;
        await using var admin = CreateAdminClient();
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                _ = await admin.ListTopicsAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Kafka did not become ready through Toxiproxy.", lastError);
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

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ToxiproxyProxyConfiguration))]
[JsonSerializable(typeof(ToxiproxyLatencyConfiguration))]
internal sealed partial class ToxiproxyJsonContext : JsonSerializerContext;
