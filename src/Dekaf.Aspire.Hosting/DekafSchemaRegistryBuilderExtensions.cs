using Aspire.Hosting.ApplicationModel;

// The HTTPS certificate APIs are experimental in Aspire 13; Aspire's own Redis and Keycloak integrations use them.
#pragma warning disable ASPIRECERTIFICATES001

namespace Aspire.Hosting;

/// <summary>Adds Confluent Schema Registry containers backed by an Aspire Kafka broker.</summary>
public static class DekafSchemaRegistryBuilderExtensions
{
    private const int RegistryPort = 8081;

    /// <summary>Adds a Confluent Schema Registry container that stores its schemas in <paramref name="kafka"/>.</summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The resource name. It is also the connection string name when referenced.</param>
    /// <param name="kafka">The Kafka broker that stores the schemas.</param>
    /// <param name="port">The host port of the registry. A port is allocated when omitted.</param>
    /// <returns>The resource builder.</returns>
    /// <remarks>
    /// <para>
    /// This version of the package defaults to the <inheritdoc cref="ContainerImageTags.SchemaRegistryTag"/> tag of the
    /// <inheritdoc cref="ContainerImageTags.SchemaRegistryImage"/> image. The registry waits for the broker to be
    /// healthy, connects through the broker's container-network listener, and reports ready once <c>/subjects</c> responds.
    /// </para>
    /// <para>
    /// Schemas live in the broker's <c>_schemas</c> topic; call <c>WithDataVolume</c> on the broker to keep them.
    /// Referencing the registry supplies its URL as the connection string, plus <c>Host</c>, <c>Port</c> and <c>Uri</c>
    /// connection properties.
    /// </para>
    /// <code>
    /// var kafka = builder.AddDekafKafka("messaging");
    /// var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka);
    /// builder.AddProject&lt;Projects.Worker&gt;("worker").WithReference(registry).WaitFor(registry);
    /// </code>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<DekafSchemaRegistryResource> AddDekafSchemaRegistry(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<DekafKafkaServerResource> kafka,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(kafka);

        var registry = new DekafSchemaRegistryResource(name);
        var endpoint = registry.PrimaryEndpoint;

        // Confluent maps SCHEMA_REGISTRY_* variables to registry properties:
        // https://docs.confluent.io/platform/current/installation/docker/config-reference.html#sr-long-configuration
        var resource = builder.AddResource(registry)
            .WithImage(ContainerImageTags.SchemaRegistryImage, ContainerImageTags.SchemaRegistryTag)
            .WithImageRegistry(ContainerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: RegistryPort, name: DekafSchemaRegistryResource.PrimaryEndpointName)
            .WithEnvironment(context =>
            {
                // The host name other registry instances use to forward requests to this one.
                context.EnvironmentVariables["SCHEMA_REGISTRY_HOST_NAME"] = endpoint.Property(EndpointProperty.Host);
                // Bind every interface: container port publishing forwards to the container's network address.
                context.EnvironmentVariables["SCHEMA_REGISTRY_LISTENERS"] = ReferenceExpression.Create(
                    $"{endpoint.Property(EndpointProperty.Scheme)}://0.0.0.0:{endpoint.Property(EndpointProperty.TargetPort)}");
                // The container-network listener advertises addresses that resolve inside other containers.
                context.EnvironmentVariables["SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS"] = ReferenceExpression.Create(
                    $"PLAINTEXT://{kafka.Resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}");
                // Single-node development cluster: the schemas topic cannot be replicated.
                context.EnvironmentVariables["SCHEMA_REGISTRY_KAFKASTORE_TOPIC_REPLICATION_FACTOR"] = "1";
            })
            .WithHttpsCertificateConfiguration(context =>
            {
                // Aspire supplies a certificate, such as the ASP.NET Core development certificate, when one is available.
                var environment = context.EnvironmentVariables;
                environment["SCHEMA_REGISTRY_INTER_INSTANCE_PROTOCOL"] = "https";
                if (context.Password is null)
                {
                    environment["SCHEMA_REGISTRY_SSL_KEYSTORE_TYPE"] = "PEM";
                    environment["SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION"] = context.CertificateWithKeyPath;
                }
                else
                {
                    environment["SCHEMA_REGISTRY_SSL_KEYSTORE_TYPE"] = "PKCS12";
                    environment["SCHEMA_REGISTRY_SSL_KEYSTORE_LOCATION"] = context.PfxPath;
                    environment["SCHEMA_REGISTRY_SSL_KEYSTORE_PASSWORD"] = context.Password;
                    environment["SCHEMA_REGISTRY_SSL_KEY_PASSWORD"] = context.Password;
                }

                return Task.CompletedTask;
            })
            .WithHttpHealthCheck("/subjects")
            .WithIconName("DocumentData")
            .WaitFor(kafka);

        if (builder.ExecutionContext.IsRunMode)
        {
            // Serve HTTPS on the same port once a certificate is configured; the listener follows the endpoint scheme.
            resource.SubscribeHttpsEndpointsUpdate(_ =>
                resource.WithEndpoint(DekafSchemaRegistryResource.PrimaryEndpointName, endpoint => endpoint.UriScheme = "https"));
        }

        return resource;
    }
}
