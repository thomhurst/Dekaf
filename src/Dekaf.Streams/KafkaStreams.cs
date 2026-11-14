using Dekaf.Streams.Internal;
using Dekaf.Streams.Internal.Runtime;
using Microsoft.Extensions.Logging;

namespace Dekaf.Streams;

/// <summary>How output, state changes and consumed offsets are committed.</summary>
public enum ProcessingGuarantee
{
    /// <summary>
    /// Output records, changelog records and consumed offsets are committed in one Kafka transaction, so
    /// each input record affects output and state exactly once. Side effects outside Kafka are not covered.
    /// </summary>
    ExactlyOnce,

    /// <summary>
    /// Output is flushed before offsets are committed, without a transaction. After a failure, input since
    /// the last commit is processed again, so output and state updates can be duplicated.
    /// </summary>
    AtLeastOnce
}

/// <summary>The lifecycle state of a <see cref="KafkaStreams"/> instance.</summary>
public enum KafkaStreamsState
{
    /// <summary>Built, not started.</summary>
    Created,

    /// <summary>Running: joined or joining the group and processing the tasks it is assigned.</summary>
    Running,

    /// <summary>Stopped after the run was cancelled.</summary>
    Stopped,

    /// <summary>Stopped because of an error. <see cref="KafkaStreams.RunAsync"/> threw it.</summary>
    Failed
}

/// <summary>
/// Runs a <see cref="Topology"/> against a Kafka cluster as a member of the streams group named after the
/// application id. Requires Kafka 4.4 or later with the streams group protocol enabled.
/// </summary>
public sealed class KafkaStreams
{
    private readonly Topology _topology;
    private readonly StreamsConfig _config;
    private int _state;

    internal KafkaStreams(Topology topology, StreamsConfig config)
    {
        _topology = topology;
        _config = config;
    }

    /// <summary>Starts configuring an instance that runs <paramref name="topology"/>.</summary>
    public static KafkaStreamsBuilder Create(Topology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return new KafkaStreamsBuilder(topology);
    }

    /// <summary>Gets the lifecycle state.</summary>
    public KafkaStreamsState State => (KafkaStreamsState)Volatile.Read(ref _state);

    /// <summary>
    /// Runs until <paramref name="cancellationToken"/> is cancelled, then commits, closes its tasks and
    /// leaves the group. Throws when a thread fails; the other threads are stopped first. An instance runs once.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _state, (int)KafkaStreamsState.Running, (int)KafkaStreamsState.Created)
            != (int)KafkaStreamsState.Created)
        {
            throw new InvalidOperationException("This KafkaStreams instance was already started.");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var client = Kafka.Connect(_config.BootstrapServers, builder =>
        {
            if (_config.LoggerFactory is not null)
            {
                builder.WithLoggerFactory(_config.LoggerFactory);
            }

            _config.ConfigureClient?.Invoke(builder);
        });

        try
        {
            var threads = new Task[_config.ThreadCount];
            for (var i = 0; i < threads.Length; i++)
            {
                var thread = new StreamThread(_topology, _config, client, i);
                threads[i] = Task.Run(() => RunThreadAsync(thread, stop), CancellationToken.None);
            }

            await Task.WhenAll(threads).ConfigureAwait(false);
            Volatile.Write(ref _state, (int)KafkaStreamsState.Stopped);
        }
        catch
        {
            Volatile.Write(ref _state, (int)KafkaStreamsState.Failed);
            throw;
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    // One failed thread stops the others: a partly running instance would hold tasks it cannot process.
    private static async Task RunThreadAsync(StreamThread thread, CancellationTokenSource stop)
    {
        try
        {
            await thread.RunAsync(stop.Token).ConfigureAwait(false);
        }
        catch
        {
            await stop.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>Configures a <see cref="KafkaStreams"/> instance.</summary>
public sealed class KafkaStreamsBuilder
{
    private readonly Topology _topology;
    private string? _bootstrapServers;
    private Action<KafkaClientBuilder>? _configureClient;
    private ProcessingGuarantee _guarantee = ProcessingGuarantee.ExactlyOnce;
    private TimeSpan _commitInterval = TimeSpan.FromMilliseconds(100);
    private TimeSpan _transactionTimeout = TimeSpan.FromSeconds(10);
    private int _threads = 1;
    private string? _instanceId;
    private string? _stateDirectory;
    private IStateStoreEngine? _storeEngine;
    private ILoggerFactory? _loggerFactory;

    internal KafkaStreamsBuilder(Topology topology) => _topology = topology;

    /// <summary>Sets the bootstrap servers as a comma-separated list.</summary>
    public KafkaStreamsBuilder WithBootstrapServers(string servers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servers);
        _bootstrapServers = servers;
        return this;
    }

    /// <summary>Configures the underlying Kafka client: TLS, authentication, timeouts.</summary>
    public KafkaStreamsBuilder ConfigureClient(Action<KafkaClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureClient = configure;
        return this;
    }

    /// <summary>Sets the processing guarantee. The default is <see cref="ProcessingGuarantee.ExactlyOnce"/>.</summary>
    public KafkaStreamsBuilder WithProcessingGuarantee(ProcessingGuarantee guarantee)
    {
        _guarantee = guarantee;
        return this;
    }

    /// <summary>
    /// Sets how often output, state changes and offsets are committed. The default is 100 ms. Under
    /// exactly-once, downstream read-committed consumers see output only after a commit.
    /// </summary>
    public KafkaStreamsBuilder WithCommitInterval(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        _commitInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the transaction timeout for exactly-once processing. The default is 10 seconds. After a crash,
    /// the broker aborts the instance's open transaction once this time has passed.
    /// </summary>
    public KafkaStreamsBuilder WithTransactionTimeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _transactionTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the number of processing threads. Each one is a member of the group with its own tasks. The default is 1.
    /// </summary>
    public KafkaStreamsBuilder WithThreads(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        _threads = count;
        return this;
    }

    /// <summary>
    /// Makes the instance a static group member, so a restart within the session timeout keeps its tasks.
    /// The id must be unique among the running instances of the application.
    /// </summary>
    public KafkaStreamsBuilder WithInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        _instanceId = instanceId;
        return this;
    }

    /// <summary>
    /// Keeps state stores on disk under <paramref name="directory"/>, using <paramref name="engine"/>,
    /// instead of in memory. After a clean shutdown a restart reads only the changelog records written
    /// since; after a crash the state is rebuilt from the changelog in full. Instances that run on one
    /// machine need separate directories.
    /// </summary>
    public KafkaStreamsBuilder WithPersistentState(string directory, IStateStoreEngine engine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(engine);
        _stateDirectory = directory;
        _storeEngine = engine;
        return this;
    }

    /// <summary>Sets the logger factory for the instance and its Kafka client.</summary>
    public KafkaStreamsBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>Builds the instance. Nothing connects until <see cref="KafkaStreams.RunAsync"/>.</summary>
    public KafkaStreams Build()
    {
        if (_bootstrapServers is null)
        {
            throw new InvalidOperationException("Bootstrap servers are required. Call WithBootstrapServers.");
        }

        return new KafkaStreams(_topology, new StreamsConfig
        {
            BootstrapServers = _bootstrapServers,
            ConfigureClient = _configureClient,
            Guarantee = _guarantee,
            CommitInterval = _commitInterval,
            TransactionTimeout = _transactionTimeout,
            ThreadCount = _threads,
            InstanceId = _instanceId,
            StateDirectory = _stateDirectory,
            StoreEngine = _storeEngine,
            LoggerFactory = _loggerFactory
        });
    }
}
