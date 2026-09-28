using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Dekaf.Admin;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>Adds Apache Kafka brokers and Kafka UI to an Aspire application.</summary>
public static class DekafKafkaBuilderExtensions
{
    private const int BrokerPort = 9092;
    private const int InternalBrokerPort = 9093;
    private const int ControllerPort = 9094;
    private const int LoopbackBrokerPort = 29092;
    private const int KafkaUIPort = 8080;
    private const string DataTarget = "/var/lib/kafka/data";
    private const string ManagesRelationship = "Manages";

    /// <summary>
    /// Adds an Apache Kafka broker. A single-node KRaft container is used for local development.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The resource name. It is also the connection string name when referenced.</param>
    /// <param name="port">The host port of the broker. A port is allocated when omitted.</param>
    /// <returns>The resource builder.</returns>
    /// <remarks>
    /// <para>
    /// This version of the package defaults to the <inheritdoc cref="ContainerImageTags.KafkaTag"/> tag of the
    /// <inheritdoc cref="ContainerImageTags.KafkaImage"/> image. Share groups (KIP-932) are enabled, and internal topics
    /// use a replication factor of one. The health check describes the cluster with a Dekaf admin client, so the
    /// AppHost does not load librdkafka.
    /// </para>
    /// <para>The connection string is a comma-separated bootstrap server list, as expected by Dekaf and other Kafka clients.</para>
    /// <code>
    /// var kafka = builder.AddDekafKafka("messaging");
    /// builder.AddProject&lt;Projects.Worker&gt;("worker").WithReference(kafka).WaitFor(kafka);
    /// </code>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<DekafKafkaServerResource> AddDekafKafka(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var kafka = new DekafKafkaServerResource(name);

        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(kafka, async (_, cancellationToken) =>
        {
            connectionString = await kafka.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{kafka.Name}' resource but the connection string was null.");
        });

        // DI owns the admin client, so it is reused across checks and disposed with the AppHost.
        // Keying it per resource keeps each broker's check on its own connection string.
        var healthCheckKey = $"{name}_check";
        builder.Services.AddKeyedSingleton<IAdminClient>(healthCheckKey, (_, _) => new AdminClientBuilder()
            .WithBootstrapServers(connectionString ?? throw new InvalidOperationException("Connection string is unavailable"))
            .Build());
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            services => new DekafBrokerHealthCheck(
                services.GetRequiredKeyedService<IAdminClient>(healthCheckKey),
                new DekafBrokerHealthCheckOptions()),
            failureStatus: null,
            tags: null));

        return builder.AddResource(kafka)
            .WithEndpoint(targetPort: BrokerPort, port: port, name: DekafKafkaServerResource.PrimaryEndpointName)
            .WithEndpoint(targetPort: InternalBrokerPort, name: DekafKafkaServerResource.InternalEndpointName)
            .WithImage(ContainerImageTags.KafkaImage, ContainerImageTags.KafkaTag)
            .WithImageRegistry(ContainerImageTags.Registry)
            .WithIconName("MailMultiple")
            .WithEnvironment(context => ConfigureKafkaContainer(context, kafka))
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a Kafka UI container that manages every Kafka broker in the application.
    /// </summary>
    /// <param name="builder">The Kafka resource builder.</param>
    /// <param name="configureContainer">Optional configuration of the Kafka UI container.</param>
    /// <param name="containerName">The Kafka UI container name. Defaults to <c>kafka-ui</c>.</param>
    /// <returns>The Kafka resource builder.</returns>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="ContainerImageTags.KafkaUiTag"/> tag of the
    /// <inheritdoc cref="ContainerImageTags.KafkaUiImage"/> image. Only one Kafka UI container is created per application.
    /// </remarks>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<DekafKafkaServerResource> WithKafkaUI(
        this IResourceBuilder<DekafKafkaServerResource> builder,
        Action<IResourceBuilder<DekafKafkaUIContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var application = builder.ApplicationBuilder;
        if (application.Resources.OfType<DekafKafkaUIContainerResource>().SingleOrDefault() is { } existing)
        {
            var existingBuilder = application.CreateResourceBuilder(existing);
            configureContainer?.Invoke(existingBuilder);
            existingBuilder.WithRelationship(builder.Resource, ManagesRelationship);
            return builder;
        }

        var kafkaUi = new DekafKafkaUIContainerResource(containerName ?? "kafka-ui");
        var kafkaUiBuilder = application.AddResource(kafkaUi)
            .WithImage(ContainerImageTags.KafkaUiImage, ContainerImageTags.KafkaUiTag)
            .WithImageRegistry(ContainerImageTags.Registry)
            .WithIconName("WindowDatabase")
            .WithHttpEndpoint(targetPort: KafkaUIPort, name: DekafKafkaUIContainerResource.PrimaryEndpointName)
            .ExcludeFromManifest();

        application.OnBeforeStart((@event, _) =>
        {
            foreach (var kafka in @event.Model.Resources.OfType<DekafKafkaServerResource>())
            {
                kafkaUiBuilder.WithRelationship(kafka, ManagesRelationship);
                application.CreateResourceBuilder(kafka).WithUrl($"{kafkaUi.PrimaryEndpoint.Property(EndpointProperty.Url)}", "Manage");
            }

            return Task.CompletedTask;
        });

        // Evaluated when the container starts, so brokers added after WithKafkaUI are included.
        kafkaUiBuilder.WithEnvironment(context =>
        {
            var index = 0;
            foreach (var kafka in application.Resources.OfType<DekafKafkaServerResource>())
                ConfigureKafkaUIContainer(context, kafka.InternalEndpoint, index++);
        });

        configureContainer?.Invoke(kafkaUiBuilder);
        kafkaUiBuilder.WithRelationship(builder.Resource, ManagesRelationship);
        return builder;
    }

    /// <summary>Sets the host port of the Kafka UI container instead of allocating one.</summary>
    /// <param name="builder">The Kafka UI resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to allocate one.</param>
    /// <returns>The Kafka UI resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<DekafKafkaUIContainerResource> WithHostPort(
        this IResourceBuilder<DekafKafkaUIContainerResource> builder,
        int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEndpoint(DekafKafkaUIContainerResource.PrimaryEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>Adds a named volume for the broker's data, so topics and schemas survive restarts.</summary>
    /// <remarks>The mount is always writable: the broker stores its logs and KRaft metadata there.</remarks>
    /// <param name="builder">The Kafka resource builder.</param>
    /// <param name="name">The volume name. Defaults to a name generated from the application and resource names.</param>
    /// <returns>The Kafka resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<DekafKafkaServerResource> WithDataVolume(
        this IResourceBuilder<DekafKafkaServerResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .WithEnvironment("KAFKA_LOG_DIRS", DataTarget)
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataTarget);
    }

    /// <summary>Adds a bind mount for the broker's data, so topics and schemas survive restarts.</summary>
    /// <remarks>The mount is always writable: the broker stores its logs and KRaft metadata there.</remarks>
    /// <param name="builder">The Kafka resource builder.</param>
    /// <param name="source">The host directory to mount.</param>
    /// <returns>The Kafka resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<DekafKafkaServerResource> WithDataBindMount(
        this IResourceBuilder<DekafKafkaServerResource> builder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);
        return builder
            .WithEnvironment("KAFKA_LOG_DIRS", DataTarget)
            .WithBindMount(source, DataTarget);
    }

    private static void ConfigureKafkaContainer(EnvironmentCallbackContext context, DekafKafkaServerResource resource)
    {
        // The apache/kafka image maps KAFKA_* variables to server properties:
        // https://github.com/apache/kafka/blob/trunk/docker/examples/README.md
        var environment = context.EnvironmentVariables;
        environment["KAFKA_NODE_ID"] = "1";
        environment["KAFKA_PROCESS_ROLES"] = "broker,controller";
        environment["KAFKA_CONTROLLER_QUORUM_VOTERS"] = $"1@localhost:{ControllerPort}";
        environment["KAFKA_CONTROLLER_LISTENER_NAMES"] = "CONTROLLER";
        environment["KAFKA_LISTENERS"] =
            $"PLAINTEXT://localhost:{LoopbackBrokerPort},CONTROLLER://localhost:{ControllerPort}," +
            $"PLAINTEXT_HOST://0.0.0.0:{BrokerPort},PLAINTEXT_INTERNAL://0.0.0.0:{InternalBrokerPort}";
        environment["KAFKA_LISTENER_SECURITY_PROTOCOL_MAP"] =
            "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT,PLAINTEXT_INTERNAL:PLAINTEXT";

        var primary = resource.PrimaryEndpoint;
        var @internal = resource.InternalEndpoint;
        // The PLAINTEXT listener is the broker's own loopback (inter-broker) listener, so it is always localhost.
        var loopbackPort = LoopbackBrokerPort.ToString(CultureInfo.InvariantCulture);
        environment["KAFKA_ADVERTISED_LISTENERS"] = context.ExecutionContext.IsRunMode
            // In run mode, containers reach the broker by resource name on the default Aspire container network.
            ? ReferenceExpression.Create(
                $"PLAINTEXT://localhost:{loopbackPort},PLAINTEXT_HOST://localhost:{primary.Property(EndpointProperty.Port)},PLAINTEXT_INTERNAL://{resource.Name}:{@internal.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create(
                $"PLAINTEXT://localhost:{loopbackPort},PLAINTEXT_HOST://{primary.Property(EndpointProperty.HostAndPort)},PLAINTEXT_INTERNAL://{@internal.Property(EndpointProperty.HostAndPort)}");

        // Single-node development cluster: internal topics cannot be replicated.
        environment["KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR"] = "1";
        environment["KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR"] = "1";
        environment["KAFKA_TRANSACTION_STATE_LOG_MIN_ISR"] = "1";
        environment["KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR"] = "1";
        environment["KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR"] = "1";
        environment["KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS"] = "0";

        // Enable share groups (KIP-932) so Dekaf share consumers work without broker changes.
        environment["KAFKA_GROUP_SHARE_ENABLE"] = "true";
        environment["KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS"] = "classic,consumer,share";
    }

    private static void ConfigureKafkaUIContainer(EnvironmentCallbackContext context, EndpointReference endpoint, int index)
    {
        var bootstrapServers = context.ExecutionContext.IsRunMode
            // In run mode, Kafka UI reaches the broker by resource name on the default Aspire container network.
            ? ReferenceExpression.Create($"{endpoint.Resource.Name}:{endpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.HostAndPort)}");

        context.EnvironmentVariables[$"KAFKA_CLUSTERS_{index}_NAME"] = endpoint.Resource.Name;
        context.EnvironmentVariables[$"KAFKA_CLUSTERS_{index}_BOOTSTRAPSERVERS"] = bootstrapServers;
    }
}
