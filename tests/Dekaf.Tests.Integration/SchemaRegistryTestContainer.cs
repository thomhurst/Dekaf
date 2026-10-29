using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Errors;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Combined Kafka and Schema Registry container for integration tests.
/// This creates both containers on the same Docker network so Schema Registry can connect to Kafka.
/// </summary>
public class KafkaWithSchemaRegistryContainer : IAsyncInitializer, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, ContainerImageBootstrapCoordinator> ImageBootstrapCoordinators = new();

    private KafkaContainer? _kafkaContainer;
    private IContainer? _schemaRegistryContainer;
    private INetwork? _network;
    private bool _externalKafka;
    private bool _externalRegistry;
    private Task? _startupTask;
    private string _bootstrapServers = string.Empty;
    private string _registryUrl = string.Empty;
    private readonly ConcurrentDictionary<string, byte> _createdTopics = new();

    /// <summary>
    /// The Kafka bootstrap servers connection string.
    /// </summary>
    public string BootstrapServers => _bootstrapServers;

    /// <summary>
    /// The Schema Registry URL.
    /// </summary>
    public string RegistryUrl => _registryUrl;

    protected virtual string SchemaRegistryImage => "confluentinc/cp-schema-registry:7.9.0";
    protected virtual string KafkaImage => "apache/kafka:4.0.2";

    protected virtual KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) => builder;

    protected virtual Task StartKafkaContainerAsync(KafkaContainer container, CancellationToken cancellationToken) =>
        container.StartAsync(cancellationToken);

    protected virtual Task StartSchemaRegistryContainerAsync(IContainer container, CancellationToken cancellationToken) =>
        container.StartAsync(cancellationToken);

    /// <summary>
    /// Bounds fixture startup. TUnit's <c>[Timeout]</c> covers only the test body, not shared
    /// <see cref="IAsyncInitializer"/> fixtures, so without this deadline a stalled image pull or
    /// container start hangs every Schema Registry test until the job times out (run 38631204581).
    /// CI pre-pulls the images, so startup there takes well under a minute; local runs may first
    /// have to pull about 2.5 GB of images.
    /// </summary>
    internal static TimeSpan StartupDeadline { get; } = ResolveStartupDeadline(
        Environment.GetEnvironmentVariable("DEKAF_TEST_SCHEMA_REGISTRY_STARTUP_SECONDS"),
        Environment.GetEnvironmentVariable("CI"));

    internal static TimeSpan ResolveStartupDeadline(string? configuredSeconds, string? ci)
    {
        if (int.TryParse(configuredSeconds, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return string.Equals(ci, "true", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(3)
            : TimeSpan.FromMinutes(15);
    }

    public async Task InitializeAsync()
    {
        // Check for external Kafka (CI environment)
        var externalKafkaBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        var externalRegistryUrl = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL");

        if (!string.IsNullOrEmpty(externalKafkaBootstrap) && !string.IsNullOrEmpty(externalRegistryUrl))
        {
            _bootstrapServers = externalKafkaBootstrap;
            _registryUrl = externalRegistryUrl;
            _externalKafka = true;
            _externalRegistry = true;
            Console.WriteLine($"[KafkaWithSchemaRegistry] Using external Kafka at {_bootstrapServers}");
            Console.WriteLine($"[KafkaWithSchemaRegistry] Using external Schema Registry at {_registryUrl}");
            await WaitForKafkaAsync().ConfigureAwait(false);
            await WaitForServicesAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        using var deadline = new CancellationTokenSource(StartupDeadline);
        var startup = new StartupProgress();
        try
        {
            // A complete first startup guarantees Testcontainers has pulled both images.
            var bootstrapCoordinator = ImageBootstrapCoordinators.GetOrAdd(
                SchemaRegistryImage,
                static _ => new ContainerImageBootstrapCoordinator());
            startup.Enter("wait for the image bootstrap gate");
            _startupTask = bootstrapCoordinator.RunAsync(
                () => StartLocalContainersAsync(startup, deadline.Token),
                deadline.Token);
            // WaitAsync returns at the deadline even if a Docker call ignores cancellation;
            // DisposeAsync then waits (bounded) for the abandoned startup before cleanup.
            await _startupTask.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // Observe the abandoned startup's eventual failure.
            _ = _startupTask?.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            var containerDetails = await DescribeSchemaRegistryContainerAsync().ConfigureAwait(false);
            throw new TimeoutException(
                $"{GetType().Name} ({KafkaImage} + {SchemaRegistryImage}) did not start within " +
                $"{StartupDeadline.TotalSeconds:0}s: stalled in stage '{startup.Stage}' for " +
                $"{startup.StageElapsed.TotalSeconds:0}s. Images that are not present locally are pulled " +
                $"inside the container start stages.{containerDetails}");
        }
    }

    private async Task<string> DescribeSchemaRegistryContainerAsync()
    {
        if (_schemaRegistryContainer is null)
            return string.Empty;

        try
        {
            var (stdout, stderr) = await _schemaRegistryContainer.GetLogsAsync()
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
            return $"{Environment.NewLine}Schema Registry container state: {_schemaRegistryContainer.State}" +
                   $"{Environment.NewLine}stdout (tail):{Environment.NewLine}{Tail(stdout)}" +
                   $"{Environment.NewLine}stderr (tail):{Environment.NewLine}{Tail(stderr)}";
        }
        catch (Exception exception)
        {
            return $"{Environment.NewLine}Schema Registry container logs unavailable: {exception.Message}";
        }

        static string Tail(string text) => text.Length <= 4000 ? text : text[^4000..];
    }

    /// <summary>Names the startup stage a deadline failure stalled in.</summary>
    private sealed class StartupProgress
    {
        private long _stageStarted = Stopwatch.GetTimestamp();
        private string _stage = "not started";

        public string Stage => Volatile.Read(ref _stage);

        public TimeSpan StageElapsed => Stopwatch.GetElapsedTime(Volatile.Read(ref _stageStarted));

        public void Enter(string stage)
        {
            Volatile.Write(ref _stageStarted, Stopwatch.GetTimestamp());
            Volatile.Write(ref _stage, stage);
            Console.WriteLine($"[KafkaWithSchemaRegistry] {DateTimeOffset.UtcNow:HH:mm:ss.fff} {stage}");
        }
    }

    private Task StartLocalContainersAsync(StartupProgress startup, CancellationToken cancellationToken) =>
        ContainerStartupRetry.RunAsync(
            () => StartLocalContainersAttemptAsync(startup, cancellationToken),
            DisposeLocalContainersAsync,
            ContainerStartupRetry.IsKnownTransient,
            delay => Task.Delay(delay, cancellationToken));

    private async Task StartLocalContainersAttemptAsync(StartupProgress startup, CancellationToken cancellationToken)
    {
        startup.Enter("create Docker network");

        // Create a shared network for the containers
        _network = new NetworkBuilder()
            .WithName($"kafka-sr-network-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync(cancellationToken).ConfigureAwait(false);

        startup.Enter($"pull and start Kafka container {KafkaImage}");

        // Start Kafka with network alias
        _kafkaContainer = ConfigureKafkaBuilder(new KafkaBuilder(KafkaImage)
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .WithEnvironment("KAFKA_HEAP_OPTS", "-Xmx512m -Xms512m")
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "30000")
            .WithEnvironment("KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS", "10000")
            .WithEnvironment("KAFKA_LOG_SEGMENT_BYTES", "1048576")
            .WithEnvironment("KAFKA_LOG_CLEANUP_POLICY", "delete"))
            .Build();

        await StartKafkaContainerAsync(_kafkaContainer, cancellationToken).ConfigureAwait(false);
        _bootstrapServers = _kafkaContainer.GetBootstrapAddress();
        Console.WriteLine($"[KafkaWithSchemaRegistry] Kafka started at {_bootstrapServers}");

        // Schema Registry can fail its initial Noop write if topic creation has completed
        // but the _schemas partition is not serving leader requests yet.
        startup.Enter("prepare the _schemas topic");
        using var readinessTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readinessTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        await PrepareSchemaStoreAsync(_bootstrapServers, readinessTimeout.Token).ConfigureAwait(false);

        startup.Enter($"pull and start Schema Registry container {SchemaRegistryImage}");

        // Start Schema Registry connected to Kafka via network
        _schemaRegistryContainer = new ContainerBuilder(SchemaRegistryImage)
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka:9093")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment(
                "SCHEMA_REGISTRY_RESOURCE_EXTENSION_CLASS",
                "io.confluent.kafka.schemaregistry.rulehandler.RuleSetResourceExtension")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/subjects").ForPort(8081)))
            .Build();

        await StartSchemaRegistryContainerAsync(_schemaRegistryContainer, cancellationToken).ConfigureAwait(false);

        var port = _schemaRegistryContainer.GetMappedPublicPort(8081);
        // Hostname honours TESTCONTAINERS_HOST_OVERRIDE and remote Docker hosts, as the wait strategy does.
        _registryUrl = $"http://{_schemaRegistryContainer.Hostname}:{port}";
        Console.WriteLine($"[KafkaWithSchemaRegistry] Schema Registry started at {_registryUrl}");

        startup.Enter("wait for Schema Registry /subjects");
        await WaitForServicesAsync(cancellationToken).ConfigureAwait(false);
        startup.Enter("ready");
    }

    internal static async Task PrepareSchemaStoreAsync(string bootstrapServers, CancellationToken cancellationToken)
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(bootstrapServers)
            .Build();
        await admin.CreateTopicsAsync(
            [new NewTopic
            {
                Name = "_schemas",
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string> { ["cleanup.policy"] = "compact" }
            }], cancellationToken: cancellationToken).ConfigureAwait(false);

        var partition = new TopicPartition("_schemas", 0);
        TopicPartitionOffsetSpec[] offsets = [new() { TopicPartition = partition, Spec = OffsetSpec.Latest }];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Refresh routing before probing the partition's leader, rather than trusting
                // TCP readiness or only the controller's CreateTopics acknowledgement.
                await admin.DescribeTopicsAsync(["_schemas"], cancellationToken).ConfigureAwait(false);
                var result = await admin.ListOffsetsAsync(offsets, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result.TryGetValue(partition, out var offset) && offset.Offset >= 0)
                    return;
            }
            catch (KafkaException exception) when (exception.IsRetriable)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] Waiting for _schemas leader: {exception.Message}");
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForKafkaAsync()
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Waiting for Kafka to be ready...");
        const int maxAttempts = 30;

        var endpoint = BootstrapServerList.Parse(_bootstrapServers);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Host, endpoint.Port).ConfigureAwait(false);
                if (client.Connected)
                {
                    Console.WriteLine("[KafkaWithSchemaRegistry] Kafka is accepting connections");
                    await Task.Delay(2000).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] TCP connect attempt {attempt + 1} failed: {ex.Message}");
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts at {_bootstrapServers}");
    }

    private async Task WaitForServicesAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[KafkaWithSchemaRegistry] Waiting for Schema Registry to be ready...");

        // Wait for Schema Registry HTTP endpoint
        const int maxAttempts = 30;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"{_registryUrl}/subjects", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[KafkaWithSchemaRegistry] Schema Registry is ready");
                    return;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Ignore and retry
            }

            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Schema Registry not ready after {maxAttempts} attempts");
    }

    /// <summary>
    /// Creates a unique topic for a test and returns the topic name.
    /// </summary>
    public async Task<string> CreateTestTopicAsync(
        int partitions = 1,
        IReadOnlyDictionary<string, string>? configs = null)
    {
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        await CreateTopicAsync(topicName, partitions, configs: configs).ConfigureAwait(false);
        return topicName;
    }

    /// <summary>
    /// Creates a topic with the specified name.
    /// </summary>
    public async Task CreateTopicAsync(
        string topicName,
        int partitions = 1,
        int replicationFactor = 1,
        IReadOnlyDictionary<string, string>? configs = null)
    {
        if (_createdTopics.ContainsKey(topicName))
        {
            return;
        }

        Console.WriteLine($"[KafkaWithSchemaRegistry] Creating topic '{topicName}' with {partitions} partition(s)...");

        if (!string.IsNullOrEmpty(_bootstrapServers))
        {
            await using var adminClient = Kafka.CreateAdminClient()
                .WithBootstrapServers(_bootstrapServers)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .Build();

            await adminClient.CreateTopicsAsync([
                new Admin.NewTopic
                {
                    Name = topicName,
                    NumPartitions = partitions,
                    ReplicationFactor = (short)replicationFactor,
                    Configs = configs
                }
            ]).ConfigureAwait(false);
        }

        _createdTopics.TryAdd(topicName, 0);
        await Task.Delay(500).ConfigureAwait(false);
        Console.WriteLine($"[KafkaWithSchemaRegistry] Topic '{topicName}' created");
    }

    public async ValueTask DisposeAsync()
    {
        if (_externalKafka && _externalRegistry)
        {
            GC.SuppressFinalize(this);
            return;
        }

        // A startup abandoned at the deadline was cancelled; let it unwind so the
        // containers it created are assigned before they are disposed below. Its
        // failure was already reported by InitializeAsync. Anything still running
        // after the bound is left to the Ryuk reaper.
        if (_startupTask is { IsCompleted: false } startupTask)
        {
            try
            {
                await startupTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KafkaWithSchemaRegistry] Abandoned startup ended with {exception.GetType().Name}");
            }
        }

        await DisposeLocalContainersAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeLocalContainersAsync()
    {
        var schemaRegistry = _schemaRegistryContainer;
        var kafka = _kafkaContainer;
        var network = _network;
        _schemaRegistryContainer = null;
        _kafkaContainer = null;
        _network = null;
        _bootstrapServers = string.Empty;
        _registryUrl = string.Empty;

        // Release every resource even if another resource's cleanup fails.
        Exception? firstError = null;
        try
        {
            if (schemaRegistry is not null)
                await schemaRegistry.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError = exception;
        }

        try
        {
            if (kafka is not null)
                await kafka.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }

        try
        {
            if (network is not null)
                await network.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }

        if (firstError is not null)
            throw firstError;
    }
}

public sealed class KafkaWithAssociationSchemaRegistryContainer : KafkaWithSchemaRegistryContainer
{
    protected override string SchemaRegistryImage => "confluentinc/cp-schema-registry:8.2.0";
    protected override string KafkaImage => $"apache/kafka:{KafkaContainerDefault.DefaultTag}";

    protected override KafkaBuilder ConfigureKafkaBuilder(KafkaBuilder builder) =>
        KafkaContainerDefault.ConfigureBuilderForVersion(
            builder,
            KafkaContainerDefault.DefaultTag);
}
