using Docker.DotNet.Models;
using Dekaf.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Kafka;

namespace Dekaf.StressTests.Infrastructure;

/// <summary>
/// Manages Kafka environment for stress tests.
/// Supports external Kafka (via KAFKA_BOOTSTRAP_SERVERS env var), single-broker Testcontainers,
/// and multi-broker KRaft clusters.
/// </summary>
internal sealed class KafkaEnvironment : IAsyncDisposable
{
    // Retention tuned to balance two constraints:
    // 1. Original 5s retention was too aggressive — caused constant "offset out of range" resets
    // 2. Long time-based retention (e.g. 30 min) would retain ~360 GB at high throughput,
    //    exhausting disk/memory on CI runners
    // Solution: byte-based retention (128 MB/partition × 6 partitions ≈ 768 MB max disk) is
    // the primary bound. Time-based retention (5 min) is a secondary backstop. Small 16 MB
    // segments let Kafka reclaim space incrementally as the consumer keeps up.
    //
    // The 1s check interval is load-bearing: producers push ~1 GB/s, so disk usage can
    // overshoot the retention caps by roughly (produce rate × check interval) between
    // sweeps. A 10s interval meant ~10 GB overshoot windows — more than the broker tmpfs —
    // which filled the log dir and halted ingestion seconds into producer runs.
    private static readonly Dictionary<string, string> RetentionConfig = new()
    {
        ["KAFKA_LOG_RETENTION_MS"] = "300000",
        ["KAFKA_LOG_RETENTION_BYTES"] = "134217728",
        ["KAFKA_LOG_SEGMENT_BYTES"] = "16777216",
        ["KAFKA_LOG_SEGMENT_DELETE_DELAY_MS"] = "1000",
        ["KAFKA_LOG_RETENTION_CHECK_INTERVAL_MS"] = "1000",
        ["KAFKA_LOG_CLEANUP_POLICY"] = "delete",
    };

    // The image default (1 GB) is undersized for sustained ~1 GB/s ingestion; give the
    // broker JVM headroom so heap pressure doesn't masquerade as a client bottleneck.
    // Overridable via STRESS_BROKER_HEAP — the same channel as STRESS_BROKER_CPUSET /
    // STRESS_BROKER_TMPFS — so CI keeps this and its workflow-managed single broker in
    // sync from one place.
    private static readonly string BrokerHeapOpts =
        Environment.GetEnvironmentVariable("STRESS_BROKER_HEAP") ?? "-Xmx2g -Xms2g";

    // Optional broker resource constraints, primarily for CI. STRESS_BROKER_CPUSET pins
    // broker containers to specific cores so the client under test keeps dedicated cores
    // (making the client, not the broker, the measured bottleneck); STRESS_BROKER_TMPFS
    // mounts the Kafka log dir on a tmpfs of the given size (e.g. "4g") so disk I/O
    // never caps broker ingestion.
    private static readonly string? BrokerCpuset = Environment.GetEnvironmentVariable("STRESS_BROKER_CPUSET");
    private static readonly string? BrokerTmpfsSize = Environment.GetEnvironmentVariable("STRESS_BROKER_TMPFS");
    private const string KafkaLogDir = "/var/lib/kafka/data";

    public string BootstrapServers { get; }
    private readonly KafkaContainer? _container;
    private readonly List<IContainer>? _clusterContainers;
    private readonly INetwork? _network;

    private static readonly Action<CreateContainerParameters>? BrokerResourceModifier =
        string.IsNullOrEmpty(BrokerCpuset) && string.IsNullOrEmpty(BrokerTmpfsSize)
            ? null
            : parameters =>
            {
                parameters.HostConfig ??= new HostConfig();
                if (!string.IsNullOrEmpty(BrokerCpuset))
                {
                    parameters.HostConfig.CpusetCpus = BrokerCpuset;
                }
                if (!string.IsNullOrEmpty(BrokerTmpfsSize))
                {
                    // mode=1777: Kafka images run as a non-root user, which cannot
                    // write to a default root-owned tmpfs mount.
                    parameters.HostConfig.Tmpfs = new Dictionary<string, string>
                    {
                        [KafkaLogDir] = $"rw,size={BrokerTmpfsSize},mode=1777"
                    };
                }
            };

    private KafkaEnvironment(string bootstrapServers, KafkaContainer? container)
    {
        BootstrapServers = bootstrapServers;
        _container = container;
    }

    private KafkaEnvironment(string bootstrapServers, List<IContainer> clusterContainers, INetwork network)
    {
        BootstrapServers = bootstrapServers;
        _clusterContainers = clusterContainers;
        _network = network;
    }

    public static async Task<KafkaEnvironment> CreateAsync(int brokerCount = 1)
    {
        var externalBootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
        if (!string.IsNullOrEmpty(externalBootstrap))
        {
            Console.WriteLine($"Using external Kafka at {externalBootstrap}");
            return new KafkaEnvironment(externalBootstrap, null);
        }

        if (brokerCount > 1)
        {
            return await CreateMultiBrokerAsync(brokerCount).ConfigureAwait(false);
        }

        return await CreateSingleBrokerAsync().ConfigureAwait(false);
    }

    private static async Task<KafkaEnvironment> CreateSingleBrokerAsync()
    {
        Console.WriteLine("Starting Kafka container via Testcontainers...");
        var builder = new KafkaBuilder("confluentinc/cp-kafka:7.5.0")
            .WithPortBinding(9092, true)
            // Explicit log dir so the optional tmpfs mount target always matches
            .WithEnvironment("KAFKA_LOG_DIRS", KafkaLogDir)
            .WithEnvironment("KAFKA_HEAP_OPTS", BrokerHeapOpts);

        foreach (var (key, value) in RetentionConfig)
        {
            builder = builder.WithEnvironment(key, value);
        }

        if (BrokerResourceModifier is { } modifier)
        {
            builder = builder.WithCreateParameterModifier(modifier);
        }

        var container = builder.Build();

        await container.StartAsync().ConfigureAwait(false);

        var rawAddress = container.GetBootstrapAddress();
        var bootstrapServers = rawAddress;
        if (Uri.TryCreate(rawAddress, UriKind.Absolute, out var uri))
        {
            bootstrapServers = $"{uri.Host}:{uri.Port}";
        }

        Console.WriteLine($"Kafka started at {bootstrapServers}");
        await WaitForKafkaAsync(bootstrapServers).ConfigureAwait(false);

        return new KafkaEnvironment(bootstrapServers, container);
    }

    private static async Task<KafkaEnvironment> CreateMultiBrokerAsync(int brokerCount)
    {
        Console.WriteLine($"Starting {brokerCount}-broker KRaft cluster via Testcontainers...");

        var network = new NetworkBuilder()
            .WithName($"kafka-stress-{Guid.NewGuid():N}")
            .Build();

        await network.CreateAsync().ConfigureAwait(false);

        // Build controller quorum voters string: 1@kafka-1:9093,2@kafka-2:9093,...
        var quorumVoters = string.Join(",",
            Enumerable.Range(1, brokerCount).Select(id => $"{id}@kafka-{id}:9093"));

        var containers = new List<IContainer>(brokerCount);
        var startTasks = new List<Task>(brokerCount);

        for (var i = 1; i <= brokerCount; i++)
        {
            var nodeId = i;
            var hostname = $"kafka-{nodeId}";
            var externalPort = 29091 + nodeId; // 29092, 29093, 29094

            var containerBuilder = new ContainerBuilder("apache/kafka:4.1.0")
                .WithName($"{hostname}-{Guid.NewGuid():N}")
                .WithHostname(hostname)
                .WithNetwork(network)
                .WithNetworkAliases(hostname)
                .WithPortBinding(externalPort, 29092)
                .WithEnvironment("KAFKA_NODE_ID", nodeId.ToString())
                .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
                .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", quorumVoters)
                // All KRaft nodes must share the same cluster ID to form a quorum
                .WithEnvironment("CLUSTER_ID", "MkU3OEVBNTcwNTJENDM2Qg")
                .WithEnvironment("KAFKA_LISTENERS", $"PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093,EXTERNAL://0.0.0.0:29092")
                .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://{hostname}:9092,EXTERNAL://localhost:{externalPort}")
                .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT,EXTERNAL:PLAINTEXT")
                .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
                .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
                .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", Math.Min(brokerCount, 3).ToString())
                // Explicit log dir so the optional tmpfs mount target always matches
                .WithEnvironment("KAFKA_LOG_DIRS", KafkaLogDir)
                .WithEnvironment("KAFKA_HEAP_OPTS", BrokerHeapOpts);

            foreach (var (key, value) in RetentionConfig)
            {
                containerBuilder = containerBuilder.WithEnvironment(key, value);
            }

            if (BrokerResourceModifier is { } modifier)
            {
                containerBuilder = containerBuilder.WithCreateParameterModifier(modifier);
            }

            var container = containerBuilder.Build();

            containers.Add(container);
            startTasks.Add(container.StartAsync());
        }

        try
        {
            await Task.WhenAll(startTasks).ConfigureAwait(false);
        }
        catch
        {
            // Clean up already-started containers and network on partial startup failure
            foreach (var c in containers)
            {
                try { await c.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            await network.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        // Bootstrap servers = all external ports
        var bootstrapServers = string.Join(",",
            Enumerable.Range(1, brokerCount).Select(id => $"localhost:{29091 + id}"));

        Console.WriteLine($"KRaft cluster started at {bootstrapServers}");
        await WaitForKafkaAsync(bootstrapServers, maxAttempts: 60).ConfigureAwait(false);

        return new KafkaEnvironment(bootstrapServers, containers, network);
    }

    private static async Task WaitForKafkaAsync(string bootstrapServers, int maxAttempts = 30)
    {
        Console.WriteLine("Waiting for Kafka to be ready...");

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await using var producer = await Kafka.CreateProducer<string, string>()
                    .WithBootstrapServers(bootstrapServers)
                    .WithClientId("kafka-ready-check")
                    .WithAcks(Producer.Acks.Leader)
                    .BuildAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await producer.ProduceAsync(new Producer.ProducerMessage<string, string>
                {
                    Topic = "__kafka_ready_check",
                    Key = "check",
                    Value = "check"
                }, cts.Token).ConfigureAwait(false);

                Console.WriteLine("Kafka is ready");
                return;
            }
            catch
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Kafka not ready after {maxAttempts} attempts");
    }

    public async Task CreateTopicAsync(string topic, int partitions, int replicationFactor = 1,
        IReadOnlyDictionary<string, string>? configs = null)
    {
        if (_container is not null)
        {
            // Use the Dekaf admin client API instead of exec-ing kafka-topics inside the
            // container. The cp-kafka broker advertises its external host port in metadata,
            // which the CLI inside the container can't reach — causing a 60-second timeout.
            // The admin client runs on the host and connects via the external bootstrap address.
            await AdminCreateTopicAsync(BootstrapServers, topic, partitions, replicationFactor, configs)
                .ConfigureAwait(false);
            return;
        }

        if (_clusterContainers is { Count: > 0 })
        {
            await ExecCreateTopicAsync(_clusterContainers[0], "/opt/kafka/bin/kafka-topics.sh", "kafka-1:9092",
                topic, partitions, replicationFactor, configs).ConfigureAwait(false);
            return;
        }

        await AdminCreateTopicAsync(BootstrapServers, topic, partitions, replicationFactor, configs).ConfigureAwait(false);
    }

    private static async Task AdminCreateTopicAsync(
        string bootstrapServers, string topic, int partitions, int replicationFactor,
        IReadOnlyDictionary<string, string>? configs)
    {
        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId("stress-test-admin")
            .Build();

        try
        {
            await adminClient.CreateTopicsAsync([
                new NewTopic
                {
                    Name = topic,
                    NumPartitions = partitions,
                    ReplicationFactor = (short)replicationFactor,
                    Configs = configs
                }
            ]).ConfigureAwait(false);

            Console.WriteLine($"Created topic via Dekaf admin API: {topic} (partitions={partitions}, replication={replicationFactor})");
        }
        catch (Dekaf.Errors.KafkaException ex) when (ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAlreadyExists)
        {
            Console.WriteLine($"Topic {topic} already exists");
        }
    }

    private static async Task ExecCreateTopicAsync(
        IContainer container, string executablePath, string bootstrapServer,
        string topic, int partitions, int replicationFactor,
        IReadOnlyDictionary<string, string>? configs)
    {
        var command = new List<string>
        {
            executablePath,
            "--bootstrap-server", bootstrapServer,
            "--create",
            "--topic", topic,
            "--partitions", partitions.ToString(),
            "--replication-factor", replicationFactor.ToString(),
            "--if-not-exists"
        };

        if (configs is not null)
        {
            foreach (var (key, value) in configs)
            {
                command.Add("--config");
                command.Add($"{key}={value}");
            }
        }

        var result = await container.ExecAsync(command).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Console.WriteLine($"Warning: Topic creation returned exit code {result.ExitCode}: {result.Stderr}");
            return;
        }

        Console.WriteLine($"Created topic: {topic} (partitions={partitions}, replication={replicationFactor})");
    }

    /// <summary>
    /// Dumps broker log-dir usage and error/storage log lines before a container is
    /// stopped. Broker-side failures (e.g. a full tmpfs halting ingestion mid-run) are
    /// otherwise invisible: Testcontainers removes the container and its logs with it.
    /// </summary>
    private static async Task DumpBrokerDiagnosticsAsync(IContainer container, string name)
    {
        try
        {
            var df = await container.ExecAsync(["df", "-h", KafkaLogDir]).ConfigureAwait(false);
            Console.WriteLine($"[{name}] log-dir usage:");
            Console.WriteLine(df.Stdout.TrimEnd());

            var (stdout, stderr) = await container.GetLogsAsync(timestampsEnabled: false).ConfigureAwait(false);

            // Bounded scan instead of concat+Split: broker logs can be tens of MB after
            // a 15-minute run and several containers dispose concurrently.
            const int maxLines = 30;
            var interesting = new Queue<string>(maxLines);
            foreach (var line in EnumerateLines(stdout).Concat(EnumerateLines(stderr)))
            {
                if (!IsInterestingLogLine(line))
                {
                    continue;
                }

                if (interesting.Count == maxLines)
                {
                    interesting.Dequeue();
                }

                interesting.Enqueue(line);
            }

            if (interesting.Count > 0)
            {
                Console.WriteLine($"[{name}] broker errors/storage events (last {interesting.Count}):");
                foreach (var line in interesting)
                {
                    Console.WriteLine($"  {line}");
                }
            }
            else
            {
                Console.WriteLine($"[{name}] no broker errors/storage events logged");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{name}] broker diagnostics failed: {ex.Message}");
        }

        // Keep in sync with the triage grep in .github/workflows/stress-tests.yml
        // (Broker Diagnostics step), which covers the workflow-managed single broker.
        static bool IsInterestingLogLine(string line) =>
            line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
            || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase)
            || line.Contains("No space", StringComparison.OrdinalIgnoreCase)
            || line.Contains("IOException", StringComparison.OrdinalIgnoreCase)
            || line.Contains("offline", StringComparison.OrdinalIgnoreCase)
            || line.Contains("shutdown", StringComparison.OrdinalIgnoreCase);

        static IEnumerable<string> EnumerateLines(string text)
        {
            using var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await DumpBrokerDiagnosticsAsync(_container, "kafka").ConfigureAwait(false);
            Console.WriteLine("Stopping Kafka container...");
            await _container.DisposeAsync().ConfigureAwait(false);
        }

        if (_clusterContainers is not null)
        {
            Console.WriteLine($"Stopping {_clusterContainers.Count}-broker cluster...");
            var disposeTasks = _clusterContainers.Select(async c =>
            {
                try
                {
                    await DumpBrokerDiagnosticsAsync(c, c.Name).ConfigureAwait(false);
                    await c.DisposeAsync().ConfigureAwait(false);
                    return (Exception?)null;
                }
                catch (Exception ex) { return ex; }
            });

            var exceptions = (await Task.WhenAll(disposeTasks).ConfigureAwait(false))
                .Where(ex => ex is not null)
                .ToList();

            if (_network is not null)
            {
                try { await _network.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { exceptions.Add(ex); }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException("One or more cluster resources failed to stop", exceptions!);
            }
        }
    }
}
