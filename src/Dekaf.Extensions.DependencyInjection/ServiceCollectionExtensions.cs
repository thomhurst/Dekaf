using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Metadata;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Producer;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Xml;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Dekaf services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dekaf services to the service collection.
    /// Registers a hosted service that automatically initializes all Kafka clients at host startup.
    /// </summary>
    public static IServiceCollection AddDekaf(this IServiceCollection services, Action<DekafBuilder> configure)
    {
        var builder = new DekafBuilder(services);
        configure(builder);

        // Register the initialization hosted service (TryAddEnumerable avoids duplicates if AddDekaf is called multiple times)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DekafInitializationService>());

        return services;
    }
}

/// <summary>
/// Builder for configuring Dekaf services.
/// </summary>
public sealed class DekafBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _globalProducerInterceptorTypes = [];
    private readonly List<Type> _globalConsumerInterceptorTypes = [];

    internal DekafBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers a global producer interceptor type that will be applied to all producers.
    /// Global interceptors execute before per-instance interceptors, in registration order.
    /// The interceptor is constructed via <see cref="ActivatorUtilities"/> for DI constructor injection support.
    /// </summary>
    /// <param name="interceptorType">
    /// The interceptor type. Can be an open generic type definition (e.g., <c>typeof(TracingInterceptor&lt;,&gt;)</c>)
    /// which will be closed with the producer's TKey/TValue, or a concrete type implementing
    /// <see cref="IProducerInterceptor{TKey, TValue}"/> for specific type combinations.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    public DekafBuilder AddGlobalProducerInterceptor(Type interceptorType)
    {
        ArgumentNullException.ThrowIfNull(interceptorType);
        _globalProducerInterceptorTypes.Add(interceptorType);
        return this;
    }

    /// <summary>
    /// Registers a global producer interceptor type that will be applied to all producers.
    /// </summary>
    /// <typeparam name="T">The concrete interceptor type.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public DekafBuilder AddGlobalProducerInterceptor<T>()
    {
        return AddGlobalProducerInterceptor(typeof(T));
    }

    /// <summary>
    /// Registers a global consumer interceptor type that will be applied to all consumers.
    /// Global interceptors execute before per-instance interceptors, in registration order.
    /// The interceptor is constructed via <see cref="ActivatorUtilities"/> for DI constructor injection support.
    /// </summary>
    /// <param name="interceptorType">
    /// The interceptor type. Can be an open generic type definition (e.g., <c>typeof(TracingInterceptor&lt;,&gt;)</c>)
    /// which will be closed with the consumer's TKey/TValue, or a concrete type implementing
    /// <see cref="IConsumerInterceptor{TKey, TValue}"/> for specific type combinations.
    /// </param>
    /// <returns>The builder instance for method chaining.</returns>
    public DekafBuilder AddGlobalConsumerInterceptor(Type interceptorType)
    {
        ArgumentNullException.ThrowIfNull(interceptorType);
        _globalConsumerInterceptorTypes.Add(interceptorType);
        return this;
    }

    /// <summary>
    /// Registers a global consumer interceptor type that will be applied to all consumers.
    /// </summary>
    /// <typeparam name="T">The concrete interceptor type.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public DekafBuilder AddGlobalConsumerInterceptor<T>()
    {
        return AddGlobalConsumerInterceptor(typeof(T));
    }

    /// <summary>
    /// Adds a producer to the service collection.
    /// </summary>
    /// <param name="configure">Configures the full producer builder surface.</param>
    public DekafBuilder AddProducer<TKey, TValue>(Action<ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return AddProducerCore(serviceKey: null, isKeyed: false, configure);
    }

    /// <summary>
    /// Adds a keyed producer to the service collection.
    /// </summary>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="configure">Configures the full producer builder surface.</param>
    public DekafBuilder AddProducer<TKey, TValue>(
        object serviceKey,
        Action<ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return AddProducerCore(serviceKey, isKeyed: true, configure);
    }

    private DekafBuilder AddProducerCore<TKey, TValue>(
        object? serviceKey,
        bool isKeyed,
        Action<ProducerBuilder<TKey, TValue>> configure)
    {
        var builder = new ProducerBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalProducerInterceptorTypes;

        if (isKeyed)
        {
            _services.AddKeyedSingleton<IKafkaProducer<TKey, TValue>>(
                serviceKey!,
                (sp, _) => BuildProducer(sp, builder, globalTypes));

            // Register as IInitializableKafkaClient (resolves the same keyed singleton instance).
            _services.AddSingleton<IInitializableKafkaClient>(sp =>
                sp.GetRequiredKeyedService<IKafkaProducer<TKey, TValue>>(serviceKey!));
        }
        else
        {
            _services.AddSingleton<IKafkaProducer<TKey, TValue>>(sp =>
                BuildProducer(sp, builder, globalTypes));

            // Register as IInitializableKafkaClient (resolves the same singleton instance).
            _services.AddSingleton<IInitializableKafkaClient>(sp =>
                sp.GetRequiredService<IKafkaProducer<TKey, TValue>>());
        }

        return this;
    }

    /// <summary>
    /// Adds a producer configured from an <see cref="IConfiguration"/> section.
    /// Fluent configuration runs after binding, so it can override config values and add services such as serializers.
    /// </summary>
    /// <param name="configuration">Configuration section using <see cref="ProducerOptions"/> property names.</param>
    /// <param name="configure">Optional additional producer configuration.</param>
    public DekafBuilder AddProducer<TKey, TValue>(
        IConfiguration configuration,
        Action<ProducerBuilder<TKey, TValue>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return AddProducer<TKey, TValue>(producer =>
        {
            DekafConfigurationBinding.ApplyProducer(configuration, producer);
            configure?.Invoke(producer);
        });
    }

    /// <summary>
    /// Adds a keyed producer configured from an <see cref="IConfiguration"/> section.
    /// Fluent configuration runs after binding, so it can override config values and add services such as serializers.
    /// </summary>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="configuration">Configuration section using <see cref="ProducerOptions"/> property names.</param>
    /// <param name="configure">Optional additional producer configuration.</param>
    public DekafBuilder AddProducer<TKey, TValue>(
        object serviceKey,
        IConfiguration configuration,
        Action<ProducerBuilder<TKey, TValue>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);

        return AddProducer<TKey, TValue>(serviceKey, producer =>
        {
            DekafConfigurationBinding.ApplyProducer(configuration, producer);
            configure?.Invoke(producer);
        });
    }

    /// <summary>
    /// Adds a consumer to the service collection.
    /// </summary>
    /// <param name="configure">Configures the full consumer builder surface.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for hosted consumer services.</param>
    public DekafBuilder AddConsumer<TKey, TValue>(
        Action<ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return AddConsumerCore(serviceKey: null, isKeyed: false, configure, configureDeadLetterQueue);
    }

    /// <summary>
    /// Adds a keyed consumer to the service collection.
    /// </summary>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configure">Configures the full consumer builder surface.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for hosted consumer services.</param>
    public DekafBuilder AddConsumer<TKey, TValue>(
        object serviceKey,
        Action<ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return AddConsumerCore(serviceKey, isKeyed: true, configure, configureDeadLetterQueue);
    }

    private DekafBuilder AddConsumerCore<TKey, TValue>(
        object? serviceKey,
        bool isKeyed,
        Action<ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue)
    {
        var builder = new ConsumerBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalConsumerInterceptorTypes;

        if (isKeyed)
        {
            _services.AddKeyedSingleton<IKafkaConsumer<TKey, TValue>>(
                serviceKey!,
                (sp, _) => BuildConsumer(sp, builder, globalTypes));

            // Register as IInitializableKafkaClient (resolves the same keyed singleton instance).
            _services.AddSingleton<IInitializableKafkaClient>(sp =>
                sp.GetRequiredKeyedService<IKafkaConsumer<TKey, TValue>>(serviceKey!));
        }
        else
        {
            _services.AddSingleton<IKafkaConsumer<TKey, TValue>>(sp =>
                BuildConsumer(sp, builder, globalTypes));

            // Register as IInitializableKafkaClient (resolves the same singleton instance).
            _services.AddSingleton<IInitializableKafkaClient>(sp =>
                sp.GetRequiredService<IKafkaConsumer<TKey, TValue>>());
        }

        if (configureDeadLetterQueue is not null)
        {
            var dlqBuilder = new DeadLetterQueueBuilder();
            configureDeadLetterQueue(dlqBuilder);

            var bootstrapServers = builder.BootstrapServersString;
            if (bootstrapServers is not null)
            {
                dlqBuilder.WithDefaultBootstrapServers(bootstrapServers);
            }

            var dlqOptions = dlqBuilder.Build();
            _services.AddSingleton(dlqOptions);
        }

        return this;
    }

    /// <summary>
    /// Adds a consumer configured from an <see cref="IConfiguration"/> section.
    /// Fluent configuration runs after binding, so it can override config values and add services such as deserializers.
    /// </summary>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for hosted consumer services.</param>
    public DekafBuilder AddConsumer<TKey, TValue>(
        IConfiguration configuration,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return AddConsumer<TKey, TValue>(
            consumer =>
            {
                DekafConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure?.Invoke(consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>
    /// Adds a keyed consumer configured from an <see cref="IConfiguration"/> section.
    /// Fluent configuration runs after binding, so it can override config values and add services such as deserializers.
    /// </summary>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for hosted consumer services.</param>
    public DekafBuilder AddConsumer<TKey, TValue>(
        object serviceKey,
        IConfiguration configuration,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);

        return AddConsumer<TKey, TValue>(
            serviceKey,
            consumer =>
            {
                DekafConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure?.Invoke(consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>
    /// Adds an admin client to the service collection.
    /// </summary>
    public DekafBuilder AddAdminClient(Action<AdminClientServiceBuilder> configure)
    {
        var builder = new AdminClientServiceBuilder();
        configure(builder);

        _services.AddSingleton<IAdminClient>(sp =>
        {
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            return builder.Build(loggerFactory);
        });

        return this;
    }

    /// <summary>
    /// Adds an admin client configured from an <see cref="IConfiguration"/> section.
    /// Fluent configuration runs after binding, so it can override config values.
    /// </summary>
    /// <param name="configuration">Configuration section using <see cref="AdminClientOptions"/> property names.</param>
    /// <param name="configure">Optional additional admin client configuration.</param>
    public DekafBuilder AddAdminClient(
        IConfiguration configuration,
        Action<AdminClientServiceBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return AddAdminClient(admin =>
        {
            admin.ApplyConfiguration(configuration);
            configure?.Invoke(admin);
        });
    }

    private static IKafkaProducer<TKey, TValue> BuildProducer<TKey, TValue>(
        IServiceProvider serviceProvider,
        ProducerBuilder<TKey, TValue> builder,
        IReadOnlyList<Type> globalTypes)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        if (globalTypes.Count > 0)
        {
            var globalInterceptors = new List<IProducerInterceptor<TKey, TValue>>(globalTypes.Count);
            foreach (var type in globalTypes)
            {
                var closedType = type.IsGenericTypeDefinition
                    ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                    : type;
                var interceptor = (IProducerInterceptor<TKey, TValue>)
                    ActivatorUtilities.CreateInstance(serviceProvider, closedType);
                globalInterceptors.Add(interceptor);
            }

            builder.AddInterceptorsFirst(globalInterceptors);
        }

        if (loggerFactory is not null)
        {
            builder.WithLoggerFactory(loggerFactory);
        }

        return builder.Build();
    }

    private static IKafkaConsumer<TKey, TValue> BuildConsumer<TKey, TValue>(
        IServiceProvider serviceProvider,
        ConsumerBuilder<TKey, TValue> builder,
        IReadOnlyList<Type> globalTypes)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        if (globalTypes.Count > 0)
        {
            var globalInterceptors = new List<IConsumerInterceptor<TKey, TValue>>(globalTypes.Count);
            foreach (var type in globalTypes)
            {
                var closedType = type.IsGenericTypeDefinition
                    ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                    : type;
                var interceptor = (IConsumerInterceptor<TKey, TValue>)
                    ActivatorUtilities.CreateInstance(serviceProvider, closedType);
                globalInterceptors.Add(interceptor);
            }

            builder.AddInterceptorsFirst(globalInterceptors);
        }

        if (loggerFactory is not null)
        {
            builder.WithLoggerFactory(loggerFactory);
        }

        return builder.Build();
    }
}

/// <summary>
/// Builder for configuring an admin client service.
/// </summary>
public sealed class AdminClientServiceBuilder
{
    private readonly AdminClientBuilder _builder = new();

    public AdminClientServiceBuilder WithBootstrapServers(string servers)
    {
        _builder.WithBootstrapServers(servers);
        return this;
    }

    public AdminClientServiceBuilder WithClientId(string clientId)
    {
        _builder.WithClientId(clientId);
        return this;
    }

    public AdminClientServiceBuilder WithTls()
    {
        _builder.WithTls();
        return this;
    }

    [Obsolete("Use WithTls instead.")]
    public AdminClientServiceBuilder UseTls()
    {
        return WithTls();
    }

    internal AdminClientServiceBuilder ApplyConfiguration(IConfiguration configuration)
    {
        DekafConfigurationBinding.ApplyAdmin(configuration, _builder);
        return this;
    }

    internal IAdminClient Build(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        if (loggerFactory is not null)
        {
            _builder.WithLoggerFactory(loggerFactory);
        }
        return _builder.Build();
    }
}

internal static class DekafConfigurationBinding
{
    public static void ApplyProducer<TKey, TValue>(
        IConfiguration configuration,
        ProducerBuilder<TKey, TValue> builder)
    {
        if (TryGetBootstrapServers(configuration, out var bootstrapServers))
            builder.WithBootstrapServers(bootstrapServers);
        if (TryGetValue<string>(configuration, nameof(ProducerOptions.ClientId), out var clientId))
            builder.WithClientId(clientId);
        if (TryGetValue<Acks>(configuration, nameof(ProducerOptions.Acks), out var acks))
            builder.WithAcks(acks);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.LingerMs), out var lingerMs))
            builder.WithLinger(TimeSpan.FromMilliseconds(lingerMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.BatchSize), out var batchSize))
            builder.WithBatchSize(batchSize);
        if (TryGetValue<ulong>(configuration, nameof(ProducerOptions.BufferMemory), out var bufferMemory))
            builder.WithBufferMemory(bufferMemory);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.MaxInFlightRequestsPerConnection), out var maxInFlight))
            builder.WithMaxInFlightRequestsPerConnection(maxInFlight);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.Retries), out var retries))
            builder.WithRetries(retries);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.RetryBackoffMs), out var retryBackoffMs))
            builder.WithRetryBackoff(TimeSpan.FromMilliseconds(retryBackoffMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.RetryBackoffMaxMs), out var retryBackoffMaxMs))
            builder.WithRetryBackoffMax(TimeSpan.FromMilliseconds(retryBackoffMaxMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.MaxBlockMs), out var maxBlockMs))
            builder.WithMaxBlock(TimeSpan.FromMilliseconds(maxBlockMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.DeliveryTimeoutMs), out var deliveryTimeoutMs))
            builder.WithDeliveryTimeout(TimeSpan.FromMilliseconds(deliveryTimeoutMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.RequestTimeoutMs), out var requestTimeoutMs))
            builder.WithRequestTimeout(TimeSpan.FromMilliseconds(requestTimeoutMs));
        if (TryGetValue<bool>(configuration, nameof(ProducerOptions.EnableIdempotence), out var enableIdempotence))
            builder.WithIdempotence(enableIdempotence);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.ConnectionsPerBroker), out var connectionsPerBroker))
            builder.WithConnectionsPerBroker(connectionsPerBroker);
        if (TryGetValue<string>(configuration, nameof(ProducerOptions.TransactionalId), out var transactionalId))
            builder.WithTransactionalId(transactionalId);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.TransactionTimeoutMs), out var transactionTimeoutMs))
            builder.WithTransactionTimeout(TimeSpan.FromMilliseconds(transactionTimeoutMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.CloseTimeoutMs), out var closeTimeoutMs))
            builder.WithCloseTimeout(TimeSpan.FromMilliseconds(closeTimeoutMs));
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.MaxRequestSize), out var maxRequestSize))
            builder.WithMaxRequestSize(maxRequestSize);
        if (TryGetValue<CompressionType>(configuration, nameof(ProducerOptions.CompressionType), out var compressionType))
            builder.WithCompression(compressionType);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.CompressionLevel), out var compressionLevel))
            builder.WithCompressionLevel(compressionLevel);
        if (TryGetValue<PartitionerType>(configuration, nameof(ProducerOptions.Partitioner), out var partitioner))
            builder.WithPartitioner(partitioner);
        ApplyTls(configuration, () => builder.WithTls(), tlsConfig => builder.WithTls(tlsConfig));
        if (TryReadSasl(configuration, out var mechanism, out var username, out var password, out var gssapi, out var oauth))
            builder.WithSaslOptions(mechanism, username, password, gssapi, oauth);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.SocketSendBufferBytes), out var sendBuffer))
            builder.WithSocketSendBufferBytes(sendBuffer);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.SocketReceiveBufferBytes), out var receiveBuffer))
            builder.WithSocketReceiveBufferBytes(receiveBuffer);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.ValueTaskSourcePoolSize), out var poolSize))
            builder.WithValueTaskSourcePoolSize(poolSize);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.ArenaCapacity), out var arenaCapacity))
            builder.WithArenaCapacity(arenaCapacity);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.InitialBatchRecordCapacity), out var initialBatchRecordCapacity))
            builder.WithInitialBatchRecordCapacity(initialBatchRecordCapacity);
        if (TryGetValue<MetadataRecoveryStrategy>(configuration, nameof(ProducerOptions.MetadataRecoveryStrategy), out var metadataRecoveryStrategy))
            builder.WithMetadataRecoveryStrategy(metadataRecoveryStrategy);
        if (TryGetValue<int>(configuration, nameof(ProducerOptions.MetadataRecoveryRebootstrapTriggerMs), out var rebootstrapMs))
            builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(rebootstrapMs));
        ApplyAdaptiveConnections(
            configuration,
            nameof(ProducerOptions.EnableAdaptiveConnections),
            nameof(ProducerOptions.MaxConnectionsPerBroker),
            builder.WithAdaptiveConnections,
            builder.WithoutAdaptiveConnections);
    }

    public static void ApplyConsumer<TKey, TValue>(
        IConfiguration configuration,
        ConsumerBuilder<TKey, TValue> builder)
    {
        if (TryGetBootstrapServers(configuration, out var bootstrapServers))
            builder.WithBootstrapServers(bootstrapServers);
        if (TryGetValue<string>(configuration, nameof(ConsumerOptions.ClientId), out var clientId))
            builder.WithClientId(clientId);
        if (TryGetValue<string>(configuration, nameof(ConsumerOptions.GroupId), out var groupId))
            builder.WithGroupId(groupId);
        if (TryGetValue<string>(configuration, nameof(ConsumerOptions.GroupInstanceId), out var groupInstanceId))
            builder.WithGroupInstanceId(groupInstanceId);
        if (TryGetValue<string>(configuration, nameof(ConsumerOptions.GroupRemoteAssignor), out var groupRemoteAssignor))
            builder.WithGroupRemoteAssignor(groupRemoteAssignor);
        if (TryGetValue<OffsetCommitMode>(configuration, nameof(ConsumerOptions.OffsetCommitMode), out var offsetCommitMode))
            builder.WithOffsetCommitMode(offsetCommitMode);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.AutoCommitIntervalMs), out var autoCommitIntervalMs))
            builder.WithAutoCommitInterval(TimeSpan.FromMilliseconds(autoCommitIntervalMs));
        if (TryGetAutoOffsetReset(configuration, out var autoOffsetReset, out var autoOffsetResetDuration))
        {
            if (autoOffsetReset == AutoOffsetReset.ByDuration)
            {
                builder.WithAutoOffsetResetByDuration(autoOffsetResetDuration!.Value);
            }
            else
            {
                builder.WithAutoOffsetReset(autoOffsetReset);
            }
        }
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.FetchMinBytes), out var fetchMinBytes))
            builder.WithFetchMinBytes(fetchMinBytes);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.FetchMaxBytes), out var fetchMaxBytes))
            builder.WithFetchMaxBytes(fetchMaxBytes);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.MaxPartitionFetchBytes), out var maxPartitionFetchBytes))
            builder.WithMaxPartitionFetchBytes(maxPartitionFetchBytes);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.FetchMaxWaitMs), out var fetchMaxWaitMs))
            builder.WithFetchMaxWait(TimeSpan.FromMilliseconds(fetchMaxWaitMs));
        if (TryGetValue<bool>(configuration, nameof(ConsumerOptions.EnableFetchSessions), out var enableFetchSessions))
            builder.WithFetchSessions(enableFetchSessions);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.MaxPollRecords), out var maxPollRecords))
            builder.WithMaxPollRecords(maxPollRecords);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.MaxPollIntervalMs), out var maxPollIntervalMs))
            builder.WithMaxPollInterval(TimeSpan.FromMilliseconds(maxPollIntervalMs));
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.SessionTimeoutMs), out var sessionTimeoutMs))
            builder.WithSessionTimeout(TimeSpan.FromMilliseconds(sessionTimeoutMs));
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.HeartbeatIntervalMs), out var heartbeatIntervalMs))
            builder.WithHeartbeatInterval(TimeSpan.FromMilliseconds(heartbeatIntervalMs));
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.RebalanceTimeoutMs), out var rebalanceTimeoutMs))
            builder.WithRebalanceTimeout(TimeSpan.FromMilliseconds(rebalanceTimeoutMs));
        if (TryGetValue<IsolationLevel>(configuration, nameof(ConsumerOptions.IsolationLevel), out var isolationLevel))
            builder.WithIsolationLevel(isolationLevel);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.RequestTimeoutMs), out var requestTimeoutMs))
            builder.WithRequestTimeout(TimeSpan.FromMilliseconds(requestTimeoutMs));
        if (TryGetValue<bool>(configuration, nameof(ConsumerOptions.CheckCrcs), out var checkCrcs))
            builder.WithCheckCrcs(checkCrcs);
        ApplyTls(configuration, () => builder.WithTls(), tlsConfig => builder.WithTls(tlsConfig));
        if (TryReadSasl(configuration, out var mechanism, out var username, out var password, out var gssapi, out var oauth))
            builder.WithSaslOptions(mechanism, username, password, gssapi, oauth);
        if (TryGetValue<bool>(configuration, nameof(ConsumerOptions.EnablePartitionEof), out var enablePartitionEof))
            builder.WithPartitionEof(enablePartitionEof);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.SocketSendBufferBytes), out var sendBuffer))
            builder.WithSocketSendBufferBytes(sendBuffer);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.SocketReceiveBufferBytes), out var receiveBuffer))
            builder.WithSocketReceiveBufferBytes(receiveBuffer);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.QueuedMinMessages), out var queuedMinMessages))
            builder.WithQueuedMinMessages(queuedMinMessages);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.QueuedMaxMessagesKbytes), out var queuedMaxMessagesKbytes))
            builder.WithQueuedMaxMessagesKbytes(queuedMaxMessagesKbytes);
        if (TryGetValue<MetadataRecoveryStrategy>(configuration, nameof(ConsumerOptions.MetadataRecoveryStrategy), out var metadataRecoveryStrategy))
            builder.WithMetadataRecoveryStrategy(metadataRecoveryStrategy);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.MetadataRecoveryRebootstrapTriggerMs), out var rebootstrapMs))
            builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(rebootstrapMs));
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.PrefetchPipelineDepth), out var prefetchPipelineDepth))
            builder.WithPrefetchPipelineDepth(prefetchPipelineDepth);
        if (TryGetValue<int>(configuration, nameof(ConsumerOptions.ConnectionsPerBroker), out var connectionsPerBroker))
            builder.WithConnectionsPerBroker(connectionsPerBroker);
        ApplyAdaptiveConnections(
            configuration,
            nameof(ConsumerOptions.EnableAdaptiveConnections),
            nameof(ConsumerOptions.MaxConnectionsPerBroker),
            builder.WithAdaptiveConnections,
            builder.WithoutAdaptiveConnections);
        ApplyAdaptiveFetchSizing(configuration, builder);
    }

    public static void ApplyAdmin(IConfiguration configuration, AdminClientBuilder builder)
    {
        if (TryGetBootstrapServers(configuration, out var bootstrapServers))
            builder.WithBootstrapServers(bootstrapServers);
        if (TryGetValue<string>(configuration, nameof(AdminClientOptions.ClientId), out var clientId))
            builder.WithClientId(clientId);
        if (TryGetValue<int>(configuration, nameof(AdminClientOptions.RequestTimeoutMs), out var requestTimeoutMs))
            builder.WithRequestTimeout(TimeSpan.FromMilliseconds(requestTimeoutMs));
        ApplyTls(configuration, () => builder.WithTls(), tlsConfig => builder.WithTls(tlsConfig));
        if (TryReadSasl(configuration, out var mechanism, out var username, out var password, out var gssapi, out var oauth))
            builder.WithSaslOptions(mechanism, username, password, gssapi, oauth);
        if (TryGetValue<MetadataRecoveryStrategy>(configuration, nameof(AdminClientOptions.MetadataRecoveryStrategy), out var metadataRecoveryStrategy))
            builder.WithMetadataRecoveryStrategy(metadataRecoveryStrategy);
        if (TryGetValue<int>(configuration, nameof(AdminClientOptions.MetadataRecoveryRebootstrapTriggerMs), out var rebootstrapMs))
            builder.WithMetadataRecoveryRebootstrapTrigger(TimeSpan.FromMilliseconds(rebootstrapMs));
    }

    private static void ApplyTls<TBuilder>(
        IConfiguration configuration,
        Func<TBuilder> useTls,
        Func<TlsConfig, TBuilder> useTlsConfig)
    {
        if (TryGetValue<TlsConfig>(configuration, nameof(ProducerOptions.TlsConfig), out var tlsConfig))
        {
            useTlsConfig(tlsConfig);
            return;
        }

        if (TryGetValue<bool>(configuration, nameof(ProducerOptions.UseTls), out var useTlsValue) && useTlsValue)
        {
            useTls();
        }
    }

    private static void ApplyAdaptiveConnections<TBuilder>(
        IConfiguration configuration,
        string enabledKey,
        string maxKey,
        Func<int, TBuilder> withAdaptiveConnections,
        Func<TBuilder> withoutAdaptiveConnections)
    {
        var hasEnabled = TryGetValue<bool>(configuration, enabledKey, out var enabled);
        var hasMax = TryGetValue<int>(configuration, maxKey, out var maxConnections);

        if (hasEnabled && !enabled)
        {
            withoutAdaptiveConnections();
            return;
        }

        if (hasMax)
        {
            withAdaptiveConnections(maxConnections);
        }
    }

    private static void ApplyAdaptiveFetchSizing<TKey, TValue>(
        IConfiguration configuration,
        ConsumerBuilder<TKey, TValue> builder)
    {
        var hasEnabled = TryGetValue<bool>(configuration, nameof(ConsumerOptions.EnableAdaptiveFetchSizing), out var enabled);
        var hasOptions = TryGetValue<AdaptiveFetchSizingOptions>(
            configuration,
            nameof(ConsumerOptions.AdaptiveFetchSizingOptions),
            out var options);

        if ((hasEnabled && enabled) || (!hasEnabled && hasOptions))
        {
            builder.WithAdaptiveFetchSizing(hasOptions ? options : null);
        }
    }

    private static bool TryReadSasl(
        IConfiguration configuration,
        out SaslMechanism mechanism,
        out string? username,
        out string? password,
        out GssapiConfig? gssapiConfig,
        out OAuthBearerConfig? oauthConfig)
    {
        var hasMechanism = TryGetValue<SaslMechanism>(configuration, nameof(ProducerOptions.SaslMechanism), out mechanism);
        var hasUsername = TryGetValue<string>(configuration, nameof(ProducerOptions.SaslUsername), out username);
        var hasPassword = TryGetValue<string>(configuration, nameof(ProducerOptions.SaslPassword), out password);
        var hasGssapi = TryGetValue<GssapiConfig>(configuration, nameof(ProducerOptions.GssapiConfig), out gssapiConfig);
        var hasOAuth = TryGetValue<OAuthBearerConfig>(configuration, nameof(ProducerOptions.OAuthBearerConfig), out oauthConfig);

        if (!hasMechanism)
        {
            mechanism = oauthConfig is not null
                ? SaslMechanism.OAuthBearer
                : gssapiConfig is not null
                    ? SaslMechanism.Gssapi
                    : hasUsername || hasPassword
                        ? SaslMechanism.Plain
                        : SaslMechanism.None;
        }

        return hasMechanism || hasUsername || hasPassword || hasGssapi || hasOAuth;
    }

    private static bool TryGetBootstrapServers(IConfiguration configuration, out string[] servers)
    {
        var section = configuration.GetSection(nameof(ProducerOptions.BootstrapServers));
        if (!section.Exists())
        {
            servers = [];
            return false;
        }

        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            servers = SplitBootstrapServers(section.Value);
            return servers.Length > 0;
        }

        servers = section.Get<string[]>()?
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Select(server => server.Trim())
            .ToArray() ?? [];

        return servers.Length > 0;
    }

    private static string[] SplitBootstrapServers(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryGetValue<T>(IConfiguration configuration, string key, out T value)
    {
        var section = configuration.GetSection(key);
        if (!section.Exists())
        {
            value = default!;
            return false;
        }

        var bound = section.Get<T>();
        if (bound is null)
        {
            value = default!;
            return false;
        }

        value = bound;
        return true;
    }

    private static bool TryGetAutoOffsetReset(
        IConfiguration configuration,
        out AutoOffsetReset autoOffsetReset,
        out TimeSpan? duration)
    {
        duration = null;

        var section = configuration.GetSection(nameof(ConsumerOptions.AutoOffsetReset));
        if (!section.Exists())
        {
            autoOffsetReset = default;
            return false;
        }

        var rawValue = section.Value;
        if (rawValue is not null &&
            rawValue.StartsWith("by_duration:", StringComparison.OrdinalIgnoreCase))
        {
            autoOffsetReset = AutoOffsetReset.ByDuration;
            duration = ParseDuration(
                rawValue["by_duration:".Length..],
                nameof(ConsumerOptions.AutoOffsetReset));
            return true;
        }

        if (rawValue is not null &&
            Enum.TryParse<AutoOffsetReset>(rawValue, ignoreCase: true, out var parsed))
        {
            autoOffsetReset = parsed;
        }
        else
        {
            autoOffsetReset = section.Get<AutoOffsetReset>();
        }

        if (autoOffsetReset == AutoOffsetReset.ByDuration)
        {
            var durationSection = configuration.GetSection(nameof(ConsumerOptions.AutoOffsetResetDuration));
            if (!durationSection.Exists())
            {
                throw new InvalidOperationException(
                    $"{nameof(ConsumerOptions.AutoOffsetResetDuration)} is required when {nameof(ConsumerOptions.AutoOffsetReset)} is {nameof(AutoOffsetReset.ByDuration)}.");
            }

            duration = ParseDuration(durationSection.Value, nameof(ConsumerOptions.AutoOffsetResetDuration));
        }

        return true;
    }

    private static TimeSpan ParseDuration(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} must specify a duration.");
        }

        value = value.Trim();
        if (TryParseHoursMinutesSeconds(value, out var hoursMinutesSeconds))
        {
            AutoOffsetResetStrategy.ValidateDuration(hoursMinutesSeconds);
            return hoursMinutesSeconds;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
        {
            AutoOffsetResetStrategy.ValidateDuration(timeSpan);
            return timeSpan;
        }

        try
        {
            var duration = XmlConvert.ToTimeSpan(value);
            AutoOffsetResetStrategy.ValidateDuration(duration);
            return duration;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"{key} must be a TimeSpan or ISO-8601 duration such as 'PT24H'.",
                ex);
        }
    }

    private static bool TryParseHoursMinutesSeconds(string value, out TimeSpan duration)
    {
        duration = default;

        var parts = value.Split(':');
        if (parts.Length != 3 ||
            parts[0].Contains('.', StringComparison.Ordinal) ||
            parts[0].Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            (uint)minutes > 59 ||
            (uint)seconds > 59)
        {
            return false;
        }

        duration = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
