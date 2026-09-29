using System.Diagnostics.CodeAnalysis;
using Dekaf.Aspire;
using Dekaf.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf Schema Registry clients with Aspire configuration and health checks.</summary>
public static class AspireDekafSchemaRegistryExtensions
{
    private const string SectionName = "Aspire:Dekaf:SchemaRegistry";

    private const string RequiresDynamicCodeMessage =
        "Dekaf Aspire registrations bind configuration with Microsoft.Extensions.Configuration.Binder. Construct SchemaRegistryClient directly for NativeAOT.";
    private const string RequiresUnreferencedCodeMessage =
        "Dekaf Aspire registrations bind configuration members that may be trimmed. Construct SchemaRegistryClient directly for NativeAOT.";

    private static readonly string[] UrlPaths = ["Config:Url", "Config:Urls"];

    /// <summary>Registers <see cref="ISchemaRegistryClient"/> as a singleton with a registry health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding one or more comma-separated registry URLs.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafSchemaRegistrySettings"/>.</param>
    /// <param name="clientFactory">
    /// Optional factory receiving application services and the bound configuration, for example to supply a custom
    /// HTTP handler. The container owns and disposes the returned client.
    /// </param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:SchemaRegistry</c> and its <paramref name="connectionName"/> subsection.
    /// Resolve the client in producer and consumer builder callbacks to use Dekaf's JSON, Avro or Protobuf serializers.
    /// The health check lists subjects; it never registers or modifies schemas.
    /// </remarks>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static void AddDekafSchemaRegistryClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafSchemaRegistrySettings>? configureSettings = null,
        Func<IServiceProvider, SchemaRegistryConfig, ISchemaRegistryClient>? clientFactory = null)
        => AddSchemaRegistryClient(builder, connectionName, serviceKey: null, configureSettings, clientFactory);

    /// <summary>Registers <see cref="ISchemaRegistryClient"/> as a keyed singleton with a registry health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafSchemaRegistrySettings"/>.</param>
    /// <param name="clientFactory">
    /// Optional factory receiving application services and the bound configuration. The container owns and disposes
    /// the returned client.
    /// </param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:SchemaRegistry</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafSchemaRegistryClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafSchemaRegistrySettings>? configureSettings = null,
        Func<IServiceProvider, SchemaRegistryConfig, ISchemaRegistryClient>? clientFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddSchemaRegistryClient(builder, name, serviceKey: name, configureSettings, clientFactory);
    }

    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    private static void AddSchemaRegistryClient(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafSchemaRegistrySettings>? configureSettings,
        Func<IServiceProvider, SchemaRegistryConfig, ISchemaRegistryClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var settings = ReadSettings(builder.Configuration, connectionName);
        configureSettings?.Invoke(settings);

        ISchemaRegistryClient Create(IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(settings.Config.Url) && settings.Config.Urls is not { Count: > 0 })
            {
                throw new InvalidOperationException(
                    $"No Schema Registry URL is configured for '{connectionName}'. Set ConnectionStrings:{connectionName} " +
                    $"or {SectionName}:Config:Url.");
            }

            return clientFactory is null ? new SchemaRegistryClient(settings.Config) : clientFactory(services, settings.Config);
        }

        if (serviceKey is null)
            builder.Services.AddSingleton(Create);
        else
            builder.Services.AddKeyedSingleton(serviceKey, (services, _) => Create(services));

        var healthCheckName = serviceKey is null ? "Dekaf_schema_registry" : $"Dekaf_schema_registry_{serviceKey}";
        var propertyKey = $"Dekaf.Aspire.HealthChecks.{healthCheckName}";
        if (settings.DisableHealthChecks || builder.Properties.ContainsKey(propertyKey))
            return;

        builder.Properties[propertyKey] = true;
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckName,
            services => new SchemaRegistryHealthCheck(serviceKey is null
                ? services.GetRequiredService<ISchemaRegistryClient>()
                : services.GetRequiredKeyedService<ISchemaRegistryClient>(serviceKey)),
            failureStatus: null,
            tags: null,
            timeout: settings.HealthCheck.Timeout));
    }

    /// <summary>Binds the shared section, then the named section, then the connection string.</summary>
    /// <remarks>
    /// Native options are init-only, so the connection string is written into the configuration before binding.
    /// <c>Urls</c> takes precedence over <c>Url</c> in Dekaf, so both are replaced as a unit: a named or
    /// connection-string URL list never keeps a failover URL from the shared defaults.
    /// </remarks>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    internal static DekafSchemaRegistrySettings ReadSettings(IConfiguration configuration, string connectionName)
    {
        var section = configuration.GetSection(SectionName);
        var namedSection = section.GetSection(connectionName);
        var connectionString = configuration.GetConnectionString(connectionName);
        var replaceShared = connectionString is not null || UrlPaths.Any(path => namedSection.GetSection(path).Exists());

        var merged = new ConfigurationBuilder()
            .AddInMemoryCollection(section.AsEnumerable(makePathsRelative: true)
                .Where(entry => !replaceShared || !IsUrlPath(entry.Key)))
            .AddInMemoryCollection(namedSection.AsEnumerable(makePathsRelative: true)
                .Where(entry => connectionString is null || !IsUrlPath(entry.Key)));
        if (connectionString is not null)
            merged.AddInMemoryCollection([new("Config:Url", connectionString)]);

        return merged.Build().Get<DekafSchemaRegistrySettings>() ?? new DekafSchemaRegistrySettings();
    }

    private static bool IsUrlPath(string key) => UrlPaths.Any(path =>
        key.Equals(path, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase));
}
