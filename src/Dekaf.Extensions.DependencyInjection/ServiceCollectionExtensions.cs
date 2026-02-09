using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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

    internal DekafBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a producer to the service collection.
    /// </summary>
    public DekafBuilder AddProducer<TKey, TValue>(Action<ProducerServiceBuilder<TKey, TValue>> configure)
    {
        var builder = new ProducerServiceBuilder<TKey, TValue>();
        configure(builder);

        _services.AddSingleton<IKafkaProducer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            return builder.Build(loggerFactory);
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

        _services.AddSingleton<IKafkaConsumer<TKey, TValue>>(sp =>
        {
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            return builder.Build(loggerFactory);
        });

        // Register as IInitializableKafkaClient (resolves the same singleton instance)
        _services.AddSingleton<IInitializableKafkaClient>(sp =>
            sp.GetRequiredService<IKafkaConsumer<TKey, TValue>>());

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
        _builder.UseCompression(Dekaf.Protocol.Records.CompressionType.Zstd);
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseGzipCompression()
    {
        _builder.UseGzipCompression();
        return this;
    }

    public ProducerServiceBuilder<TKey, TValue> UseLz4Compression()
    {
        _builder.UseCompression(Dekaf.Protocol.Records.CompressionType.Lz4);
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

    internal IKafkaProducer<TKey, TValue> Build(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        if (loggerFactory is not null)
        {
            _builder.WithLoggerFactory(loggerFactory);
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

    public ConsumerServiceBuilder<TKey, TValue> WithBootstrapServers(string servers)
    {
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

    internal IKafkaConsumer<TKey, TValue> Build(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        if (loggerFactory is not null)
        {
            _builder.WithLoggerFactory(loggerFactory);
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
