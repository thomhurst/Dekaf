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
    /// <param name="configure">Configures the full producer builder surface.</param>
    public DekafBuilder AddProducer<TKey, TValue>(Action<ProducerBuilder<TKey, TValue>> configure)
    {
        var builder = new ProducerBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalProducerInterceptorTypes;

        _services.AddSingleton<IKafkaProducer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();

            if (globalTypes.Count > 0)
            {
                var globalInterceptors = new List<IProducerInterceptor<TKey, TValue>>(globalTypes.Count);
                foreach (var type in globalTypes)
                {
                    var closedType = type.IsGenericTypeDefinition
                        ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                        : type;
                    var interceptor = (IProducerInterceptor<TKey, TValue>)
                        ActivatorUtilities.CreateInstance(sp, closedType);
                    globalInterceptors.Add(interceptor);
                }

                builder.AddInterceptorsFirst(globalInterceptors);
            }

            if (loggerFactory is not null)
            {
                builder.WithLoggerFactory(loggerFactory);
            }

            return builder.Build();
        });

        // Register as IInitializableKafkaClient (resolves the same singleton instance)
        _services.AddSingleton<IInitializableKafkaClient>(sp =>
            sp.GetRequiredService<IKafkaProducer<TKey, TValue>>());

        return this;
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
        var builder = new ConsumerBuilder<TKey, TValue>();
        configure(builder);

        var globalTypes = _globalConsumerInterceptorTypes;

        _services.AddSingleton<IKafkaConsumer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();

            if (globalTypes.Count > 0)
            {
                var globalInterceptors = new List<IConsumerInterceptor<TKey, TValue>>(globalTypes.Count);
                foreach (var type in globalTypes)
                {
                    var closedType = type.IsGenericTypeDefinition
                        ? type.MakeGenericType(typeof(TKey), typeof(TValue))
                        : type;
                    var interceptor = (IConsumerInterceptor<TKey, TValue>)
                        ActivatorUtilities.CreateInstance(sp, closedType);
                    globalInterceptors.Add(interceptor);
                }

                builder.AddInterceptorsFirst(globalInterceptors);
            }

            if (loggerFactory is not null)
            {
                builder.WithLoggerFactory(loggerFactory);
            }

            return builder.Build();
        });

        // Register as IInitializableKafkaClient (resolves the same singleton instance)
        _services.AddSingleton<IInitializableKafkaClient>(sp =>
            sp.GetRequiredService<IKafkaConsumer<TKey, TValue>>());

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
