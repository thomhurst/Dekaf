using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Streams.Extensions.Hosting;

/// <summary>Registers Dekaf.Streams applications with the generic host.</summary>
public static class StreamsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a streams application that starts and stops with the host. The host's logger factory is used
    /// unless <paramref name="configure"/> sets another. A failure of the application stops the host,
    /// following the host's <c>BackgroundServiceExceptionBehavior</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationId">The application id: the streams group id and internal topic prefix.</param>
    /// <param name="buildTopology">Declares the topology.</param>
    /// <param name="configure">Configures the instance: bootstrap servers, guarantee, state, client security.</param>
    public static IServiceCollection AddDekafStreams(
        this IServiceCollection services,
        string applicationId,
        Action<StreamsBuilder> buildTopology,
        Action<KafkaStreamsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(buildTopology);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddDekafStreams(
            applicationId,
            (_, builder) => buildTopology(builder),
            (_, builder) => configure(builder));
    }

    /// <summary>
    /// Adds a streams application that starts and stops with the host, with access to the service provider
    /// while the topology is declared and the instance configured.
    /// </summary>
    public static IServiceCollection AddDekafStreams(
        this IServiceCollection services,
        string applicationId,
        Action<IServiceProvider, StreamsBuilder> buildTopology,
        Action<IServiceProvider, KafkaStreamsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentNullException.ThrowIfNull(buildTopology);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton(provider =>
        {
            var topology = new StreamsBuilder(applicationId);
            buildTopology(provider, topology);

            var streams = KafkaStreams.Create(topology.Build());
            if (provider.GetService<ILoggerFactory>() is { } loggerFactory)
            {
                streams.WithLoggerFactory(loggerFactory);
            }

            configure(provider, streams);
            return streams.Build();
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, KafkaStreamsHostedService>());
        return services;
    }
}

/// <summary>Runs every registered streams application for the lifetime of the host.</summary>
internal sealed class KafkaStreamsHostedService(IEnumerable<KafkaStreams> applications) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runs = new List<Task>();
        foreach (var application in applications)
        {
            runs.Add(application.RunAsync(stoppingToken));
        }

        return Task.WhenAll(runs);
    }
}
