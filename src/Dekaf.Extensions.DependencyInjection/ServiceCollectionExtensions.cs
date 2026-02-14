using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Dekaf.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    public DekafBuilder AddProducer<TKey, TValue>(Action<ProducerServiceBuilder<TKey, TValue>> configure)
    {
        var builder = new ProducerServiceBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalProducerInterceptorTypes;

        _services.AddSingleton<IKafkaProducer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();

            List<IProducerInterceptor<TKey, TValue>>? globalInterceptors = null;
            if (globalTypes.Count > 0)
            {
                globalInterceptors = new List<IProducerInterceptor<TKey, TValue>>(globalTypes.Count);
                foreach (var type in globalTypes)
                {
                    var closedType = type.IsGenericTypeDefinition
                        ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                        : type;
                    var interceptor = (IProducerInterceptor<TKey, TValue>)
                        ActivatorUtilities.CreateInstance(sp, closedType);
                    globalInterceptors.Add(interceptor);
                }
            }

            return builder.Build(loggerFactory, globalInterceptors);
        });

        // Register as IInitializableKafkaClient (resolves the same singleton instance)
        _services.AddSingleton<IInitializableKafkaClient>(sp =>
            sp.GetRequiredService<IKafkaProducer<TKey, TValue>>());

        return this;
    }

    /// <summary>
    /// Adds a consumer to the service collection.
    /// </summary>
    public DekafBuilder AddConsumer<TKey, TValue>(Action<ConsumerServiceBuilder<TKey, TValue>> configure)
    {
        var builder = new ConsumerServiceBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalConsumerInterceptorTypes;

        _services.AddSingleton<IKafkaConsumer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();

            List<IConsumerInterceptor<TKey, TValue>>? globalInterceptors = null;
            if (globalTypes.Count > 0)
            {
                globalInterceptors = new List<IConsumerInterceptor<TKey, TValue>>(globalTypes.Count);
                foreach (var type in globalTypes)
                {
                    var closedType = type.IsGenericTypeDefinition
                        ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                        : type;
                    var interceptor = (IConsumerInterceptor<TKey, TValue>)
                        ActivatorUtilities.CreateInstance(sp, closedType);
                    globalInterceptors.Add(interceptor);
                }
            }

            return builder.Build(loggerFactory, globalInterceptors);
        });

        // Register as IInitializableKafkaClient (resolves the same singleton instance)
        _services.AddSingleton<IInitializableKafkaClient>(sp =>
            sp.GetRequiredService<IKafkaConsumer<TKey, TValue>>());

        // Register DLQ options if configured
        var dlqOptions = builder.BuildDeadLetterOptions();
        if (dlqOptions is not null)
        {
            _services.AddSingleton(dlqOptions);
        }

        return this;
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
}

/// <summary>
/// Builder for configuring a producer service.
/// </summary>
public sealed class ProducerServiceBuilder<TKey, TValue>
{
    private readonly ProducerBuilder<TKey, TValue> _builder = new();
    private readonly List<IProducerInterceptor<TKey, TValue>> _perInstanceInterceptors = [];

    public ProducerServiceBuilder<TKey, TValue> WithBootstrapServers(string servers)
    {
        _builder.WithBootstrapServers(servers);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> WithClientId(string clientId)
    {
        _builder.WithClientId(clientId);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> WithAcks(Acks acks)
    {
        _builder.WithAcks(acks);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> EnableIdempotence()
    {
        _builder.EnableIdempotence();
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> WithTransactionalId(string transactionalId)
    {
        _builder.WithTransactionalId(transactionalId);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseZstdCompression()
    {
        _builder.UseCompression(Protocol.Records.CompressionType.Zstd);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseGzipCompression()
    {
        _builder.UseGzipCompression();
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseLz4Compression()
    {
        _builder.UseCompression(Protocol.Records.CompressionType.Lz4);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> WithKeySerializer(ISerializer<TKey> serializer)
    {
        _builder.WithKeySerializer(serializer);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> WithValueSerializer(ISerializer<TValue> serializer)
    {
        _builder.WithValueSerializer(serializer);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseTls()
    {
        _builder.UseTls();
        return this;
    }

    /// <summary>
    /// Adds a per-instance producer interceptor. Per-instance interceptors execute after global interceptors.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ProducerServiceBuilder<TKey, TValue> AddInterceptor(IProducerInterceptor<TKey, TValue> interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _perInstanceInterceptors.Add(interceptor);
        return this;
    }

    internal IKafkaProducer<TKey, TValue> Build(
        ILoggerFactory? loggerFactory,
        List<IProducerInterceptor<TKey, TValue>>? globalInterceptors = null)
    {
        if (loggerFactory is not null)
        {
            _builder.WithLoggerFactory(loggerFactory);
        }

        // Add global interceptors first (execute first)
        if (globalInterceptors is not null)
        {
            foreach (var interceptor in globalInterceptors)
            {
                _builder.AddInterceptor(interceptor);
            }
        }

        // Add per-instance interceptors second (execute after globals)
        foreach (var interceptor in _perInstanceInterceptors)
        {
            _builder.AddInterceptor(interceptor);
        }

        return _builder.Build();
    }
}

/// <summary>
/// Builder for configuring a consumer service.
/// </summary>
public sealed class ConsumerServiceBuilder<TKey, TValue>
{
    private readonly ConsumerBuilder<TKey, TValue> _builder = new();
    private readonly List<IConsumerInterceptor<TKey, TValue>> _perInstanceInterceptors = [];
    private string? _bootstrapServers;

    public ConsumerServiceBuilder<TKey, TValue> WithBootstrapServers(string servers)
    {
        _bootstrapServers = servers;
        _builder.WithBootstrapServers(servers);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithClientId(string clientId)
    {
        _builder.WithClientId(clientId);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithGroupId(string groupId)
    {
        _builder.WithGroupId(groupId);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithGroupInstanceId(string groupInstanceId)
    {
        _builder.WithGroupInstanceId(groupInstanceId);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithOffsetCommitMode(OffsetCommitMode mode)
    {
        _builder.WithOffsetCommitMode(mode);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithAutoOffsetReset(AutoOffsetReset autoOffsetReset)
    {
        _builder.WithAutoOffsetReset(autoOffsetReset);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithKeyDeserializer(IDeserializer<TKey> deserializer)
    {
        _builder.WithKeyDeserializer(deserializer);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithValueDeserializer(IDeserializer<TValue> deserializer)
    {
        _builder.WithValueDeserializer(deserializer);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> WithRebalanceListener(IRebalanceListener listener)
    {
        _builder.WithRebalanceListener(listener);
        return this;
    }

    public ConsumerServiceBuilder<TKey, TValue> UseTls()
    {
        _builder.UseTls();
        return this;
    }

    /// <summary>
    /// Sets the partition assignment strategy for the classic consumer group protocol.
    /// </summary>
    /// <param name="strategy">The built-in assignment strategy to use.</param>
    public ConsumerServiceBuilder<TKey, TValue> WithPartitionAssignmentStrategy(PartitionAssignmentStrategy strategy)
    {
        _builder.WithPartitionAssignmentStrategy(strategy);
        return this;
    }

    /// <summary>
    /// Sets a custom partition assignment strategy implementation.
    /// When set, this takes precedence over the enum-based overload.
    /// </summary>
    /// <param name="strategy">The custom partition assignment strategy to use.</param>
    public ConsumerServiceBuilder<TKey, TValue> WithPartitionAssignmentStrategy(IPartitionAssignmentStrategy strategy)
    {
        _builder.WithPartitionAssignmentStrategy(strategy);
        return this;
    }

    private DeadLetterQueueBuilder? _dlqBuilder;

    /// <summary>
    /// Configures dead letter queue routing for the consumer service.
    /// </summary>
    /// <param name="configure">An action to configure the dead letter queue builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerServiceBuilder<TKey, TValue> WithDeadLetterQueue(Action<DeadLetterQueueBuilder> configure)
    {
        _dlqBuilder = new DeadLetterQueueBuilder();
        configure(_dlqBuilder);
        return this;
    }

    /// <summary>
    /// Builds the dead letter options if a DLQ was configured.
    /// </summary>
    /// <returns>The configured <see cref="DeadLetterOptions"/> or null if DLQ was not configured.</returns>
    internal DeadLetterOptions? BuildDeadLetterOptions()
    {
        if (_dlqBuilder is null)
        {
            return null;
        }

        // Inherit consumer's bootstrap servers if not explicitly set on DLQ
        if (_bootstrapServers is not null)
        {
            _dlqBuilder.WithDefaultBootstrapServers(_bootstrapServers);
        }

        return _dlqBuilder.Build();
    }

    /// <summary>
    /// Adds a per-instance consumer interceptor. Per-instance interceptors execute after global interceptors.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConsumerServiceBuilder<TKey, TValue> AddInterceptor(IConsumerInterceptor<TKey, TValue> interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        _perInstanceInterceptors.Add(interceptor);
        return this;
    }

    internal IKafkaConsumer<TKey, TValue> Build(
        ILoggerFactory? loggerFactory,
        List<IConsumerInterceptor<TKey, TValue>>? globalInterceptors = null)
    {
        if (loggerFactory is not null)
        {
            _builder.WithLoggerFactory(loggerFactory);
        }

        // Add global interceptors first (execute first)
        if (globalInterceptors is not null)
        {
            foreach (var interceptor in globalInterceptors)
            {
                _builder.AddInterceptor(interceptor);
            }
        }

        // Add per-instance interceptors second (execute after globals)
        foreach (var interceptor in _perInstanceInterceptors)
        {
            _builder.AddInterceptor(interceptor);
        }

        return _builder.Build();
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

    public AdminClientServiceBuilder UseTls()
    {
        _builder.UseTls();
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
