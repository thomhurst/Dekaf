using System.Net;
using System.Net.Sockets;
using System.Text;
using Dekaf.Producer;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

public sealed class EventHubsEmulatorContainer : IAsyncInitializer, IAsyncDisposable
{
    internal const int PartitionCount = 4;
    internal const string EventHubsImage =
        "mcr.microsoft.com/azure-messaging/eventhubs-emulator:2.2.1@sha256:be413f0d59541621879e6d197d73f64f3b3ac5fa45861641fdc1430252b8b44b";
    internal const string AzuriteImage =
        "mcr.microsoft.com/azure-storage/azurite:3.36.0@sha256:76b8127d608fab8287a14a4bfeb9a5502cdcffb4bf1e86f09f324ebb0e70edba";

    public const string SaslUsername = "$ConnectionString";

    public const string ProducerTopic = "dekaf-producer";
    public const string ConsumerTopic = "dekaf-consumer";
    public const string RoundTripTopic = "dekaf-roundtrip";
    public const string OffsetTopic = "dekaf-offsets";
    public const string BatchTopic = "dekaf-batch";
    public const string LifecycleTopic = "dekaf-lifecycle";

    public const string ConsumerGroup = "dekaf-consumer-group";
    public const string RoundTripGroup = "dekaf-roundtrip-group";
    public const string OffsetGroup = "dekaf-offset-group";
    public const string BatchGroup = "dekaf-batch-group";

    private const ushort AzuriteBlobPort = 10000;
    private const string AzuriteAlias = "azurite";
    private const string EmulatorAlias = "eventhubs-emulator";
    private const string ReadinessTopic = "dekaf-readiness";
    private const string ConfigPath = "/Eventhubs_Emulator/ConfigFiles/Config.json";
    private const string EndpointsConfigPath = "/Eventhubs_Emulator/ComponentFiles/Endpoints.config";

    private static readonly byte[] EmulatorConfig = Encoding.UTF8.GetBytes(
        """
        {
          "UserConfig": {
            "NamespaceConfig": [
              {
                "Type": "EventHub",
                "Name": "emulatorNs1",
                "Entities": [
                  { "Name": "dekaf-readiness", "PartitionCount": "1", "ConsumerGroups": [] },
                  { "Name": "dekaf-producer", "PartitionCount": "4", "ConsumerGroups": [] },
                  { "Name": "dekaf-consumer", "PartitionCount": "4", "ConsumerGroups": [{ "Name": "dekaf-consumer-group" }] },
                  { "Name": "dekaf-roundtrip", "PartitionCount": "4", "ConsumerGroups": [{ "Name": "dekaf-roundtrip-group" }] },
                  { "Name": "dekaf-offsets", "PartitionCount": "4", "ConsumerGroups": [{ "Name": "dekaf-offset-group" }] },
                  { "Name": "dekaf-batch", "PartitionCount": "4", "ConsumerGroups": [{ "Name": "dekaf-batch-group" }] },
                  { "Name": "dekaf-lifecycle", "PartitionCount": "4", "ConsumerGroups": [] }
                ]
              }
            ],
            "LoggingConfig": { "Type": "Console" }
          }
        }
        """);

    private INetwork? _network;
    private IContainer? _azurite;
    private IContainer? _emulator;

    public string BootstrapServers { get; private set; } = string.Empty;
    public string SaslPassword { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await ContainerStartupRetry.RunAsync(
            StartAttemptAsync,
            DisposeAttemptAsync,
            ContainerStartupRetry.IsKnownTransient).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAttemptAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task StartAttemptAsync()
    {
        var kafkaPort = GetFreeTcpPort();
        SaslPassword = CreateSaslPassword(kafkaPort);
        _network = new NetworkBuilder()
            .WithName($"dekaf-eventhubs-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        _azurite = new ContainerBuilder(AzuriteImage)
            .WithNetwork(_network)
            .WithNetworkAliases(AzuriteAlias)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(AzuriteBlobPort))
            .Build();
        await _azurite.StartAsync().ConfigureAwait(false);

        _emulator = new ContainerBuilder(EventHubsImage)
            .WithNetwork(_network)
            .WithNetworkAliases(EmulatorAlias)
            .WithPortBinding(kafkaPort, kafkaPort)
            .WithResourceMapping(EmulatorConfig, ConfigPath)
            .WithResourceMapping(CreateEndpointsConfig(kafkaPort), EndpointsConfigPath)
            .WithEnvironment("BLOB_SERVER", AzuriteAlias)
            .WithEnvironment("METADATA_SERVER", AzuriteAlias)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilExternalTcpPortIsAvailable(kafkaPort))
            .Build();

        try
        {
            await _emulator.StartAsync().ConfigureAwait(false);
            BootstrapServers = $"{_emulator.Hostname}:{_emulator.GetMappedPublicPort(kafkaPort)}";
            await WaitUntilReadyAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Event Hubs emulator failed to become Kafka-ready.{await GetLogTailAsync().ConfigureAwait(false)}",
                exception);
        }
    }

    private async Task WaitUntilReadyAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(BootstrapServers)
            .WithClientId("dekaf-eventhubs-readiness")
            .WithSaslPlain(SaslUsername, SaslPassword)
            .WithIdempotence(false)
            .WithMaxBlock(TimeSpan.FromSeconds(15))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(30))
            .WithRequestTimeout(TimeSpan.FromSeconds(10))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(timeout.Token).ConfigureAwait(false);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = ReadinessTopic,
            Key = "ready",
            Value = "ready"
        }, timeout.Token).ConfigureAwait(false);
    }

    private async ValueTask DisposeAttemptAsync()
    {
        var emulator = _emulator;
        var azurite = _azurite;
        var network = _network;
        _emulator = null;
        _azurite = null;
        _network = null;
        BootstrapServers = string.Empty;
        SaslPassword = string.Empty;

        Exception? firstError = null;
        firstError = await TryDisposeAsync(emulator, firstError).ConfigureAwait(false);
        firstError = await TryDisposeAsync(azurite, firstError).ConfigureAwait(false);
        firstError = await TryDisposeAsync(network, firstError).ConfigureAwait(false);

        if (firstError is not null)
            throw firstError;
    }

    private async Task<string> GetLogTailAsync()
    {
        var emulatorLogs = await GetLogTailAsync(_emulator).ConfigureAwait(false);
        var azuriteLogs = await GetLogTailAsync(_azurite).ConfigureAwait(false);
        return $"\nEvent Hubs emulator logs:\n{emulatorLogs}\nAzurite logs:\n{azuriteLogs}";
    }

    private static async Task<string> GetLogTailAsync(IContainer? container)
    {
        if (container is null)
            return "<container not created>";

        try
        {
            var (stdout, stderr) = await container.GetLogsAsync().ConfigureAwait(false);
            return Tail($"{stdout}\n{stderr}", 8_000);
        }
        catch (Exception exception)
        {
            return $"<logs unavailable: {exception.Message}>";
        }
    }

    private static async ValueTask<Exception?> TryDisposeAsync(
        IAsyncDisposable? resource,
        Exception? firstError)
    {
        if (resource is null)
            return firstError;

        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }

        return firstError;
    }

    private static string Tail(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[^maxChars..];

    private static byte[] CreateEndpointsConfig(int kafkaPort) => Encoding.UTF8.GetBytes(
        $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <endpoints>
          <input name="AmqpIn" csdefEndpoint="AmqpIn" pattern="amqp://localhost:5672" type="Internal" />
          <input name="KafkaIn" csdefEndpoint="KafkaIn" pattern="amqp://localhost:{{kafkaPort}}" type="Internal" />
          <input name="SharedAmqpEndpointTcp1" csdefEndpoint="SharedAmqpEndpointTcp1" pattern="amqp://localhost:23340" type="Internal" />
        </endpoints>
        """);

    // The emulator derives the Kafka broker metadata port from this Endpoint. Its documented
    // password omits a port and therefore hardcodes advertised metadata to localhost:9092.
    // Including the randomized listener port is required for parallel-safe host mappings.
    private static string CreateSaslPassword(int kafkaPort) =>
        $"Endpoint=sb://localhost:{kafkaPort};SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
