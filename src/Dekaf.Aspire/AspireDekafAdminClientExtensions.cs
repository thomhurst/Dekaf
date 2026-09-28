using System.Diagnostics.CodeAnalysis;
using Dekaf.Admin;
using Dekaf.Aspire;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf admin clients with Aspire configuration, health checks and telemetry.</summary>
public static class AspireDekafAdminClientExtensions
{
    private const string Role = "AdminClient";
    private const string BootstrapControllers = nameof(AdminClientOptions.BootstrapControllers);

    private static readonly string[] ConnectionPaths =
        [.. DekafAspireRegistration.BrokerConnectionPaths, $"Config:{BootstrapControllers}"];

    /// <summary>Registers <see cref="IAdminClient"/> as a singleton with a broker connectivity health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafAdminClientSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the admin client, with access to application services.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:AdminClient</c> and its <paramref name="connectionName"/> subsection.
    /// Native options under <c>Config</c> include authentication, TLS and <c>BootstrapControllers</c> for
    /// controller-only administration. A connection string replaces configured controllers, because Dekaf
    /// requires bootstrap servers and controllers to be mutually exclusive.
    /// The health check describes the cluster; it never creates topics or produces messages.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafAdminClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafAdminClientSettings>? configureSettings = null,
        Action<IServiceProvider, AdminClientServiceBuilder>? configureBuilder = null)
        => AddAdminClient(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <summary>Registers <see cref="IAdminClient"/> as a keyed singleton with a broker connectivity health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafAdminClientSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the admin client, with access to application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:AdminClient</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafAdminClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafAdminClientSettings>? configureSettings = null,
        Action<IServiceProvider, AdminClientServiceBuilder>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddAdminClient(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddAdminClient(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafAdminClientSettings>? configureSettings,
        Action<IServiceProvider, AdminClientServiceBuilder>? configureBuilder)
    {
        var (settings, native) = DekafAspireRegistration.ReadSettings(
            builder, Role, connectionName, configureSettings, ConnectionPaths);

        if (settings.ConnectionString is not null)
        {
            // A broker connection replaces controller-only configuration.
            native = new ConfigurationBuilder().AddInMemoryCollection(native.AsEnumerable(makePathsRelative: true)
                .Where(entry => !DekafAspireRegistration.IsPathOrChild(entry.Key, BootstrapControllers))).Build();
        }

        void Configure(IServiceProvider services, AdminClientServiceBuilder admin)
        {
            if (settings.ConnectionString is not null)
                admin.WithBootstrapServers(settings.ConnectionString);
            configureBuilder?.Invoke(services, admin);
        }

        builder.Services.AddDekaf(dekaf =>
        {
            if (serviceKey is null)
                dekaf.AddAdminClient(native, Configure);
            else
                dekaf.AddAdminClient(serviceKey, native, Configure);
        });

        DekafAspireRegistration.AddTelemetry(builder, settings);

        if (!settings.DisableHealthChecks)
        {
            DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName("admin", serviceKey),
                services => new DekafBrokerHealthCheck(
                    DekafAspireRegistration.Resolve<IAdminClient>(services, serviceKey),
                    settings.HealthCheck));
        }
    }
}
