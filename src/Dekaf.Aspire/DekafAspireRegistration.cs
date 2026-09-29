using System.Diagnostics.CodeAnalysis;
using Dekaf.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Aspire;

/// <summary>Configuration, health check and telemetry plumbing shared by every Dekaf Aspire registration.</summary>
internal static class DekafAspireRegistration
{
    internal const string RequiresDynamicCodeMessage =
        "Dekaf Aspire registrations bind configuration with Microsoft.Extensions.Configuration.Binder. Register clients with Dekaf.Extensions.DependencyInjection typed options for NativeAOT.";
    internal const string RequiresUnreferencedCodeMessage =
        "Dekaf Aspire registrations bind configuration members that may be trimmed. Register clients with Dekaf.Extensions.DependencyInjection typed options for NativeAOT.";

    internal const string ConfigurationSectionPrefix = "Aspire:Dekaf";

    /// <summary>Connection fields that are replaced as a unit when a named section supplies any of them.</summary>
    internal static readonly string[] BrokerConnectionPaths = ["ConnectionString", "Config:BootstrapServers"];

    /// <summary>
    /// Reads the settings for one registration and returns the native client configuration section.
    /// </summary>
    /// <remarks>
    /// Precedence, lowest to highest: the shared role section, the section named after the connection,
    /// <c>ConnectionStrings:{connectionName}</c>, then <paramref name="configureSettings"/>.
    /// </remarks>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    internal static (TSettings Settings, IConfiguration NativeConfiguration) ReadSettings<TSettings>(
        IHostApplicationBuilder builder,
        string role,
        string connectionName,
        Action<TSettings>? configureSettings,
        IReadOnlyList<string> connectionPaths)
        where TSettings : class, IDekafAspireSettings, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var configuration = MergeConfiguration(builder.Configuration, $"{ConfigurationSectionPrefix}:{role}", connectionName, connectionPaths);
        var settings = configuration.Get<TSettings>() ?? new TSettings();
        settings.ConnectionString = builder.Configuration.GetConnectionString(connectionName) ?? settings.ConnectionString;
        configureSettings?.Invoke(settings);
        return (settings, configuration.GetSection("Config"));
    }

    /// <summary>
    /// Merges a role section with the subsection named after the connection.
    /// </summary>
    /// <remarks>
    /// Keys are merged individually so a named section can override one option and inherit the rest.
    /// Connection fields are the exception: when the named section sets any of them, all default
    /// connection fields are dropped. Merging indexed server lists key by key could otherwise keep a
    /// default backup broker from a different cluster.
    /// </remarks>
    internal static IConfigurationRoot MergeConfiguration(
        IConfiguration configuration,
        string sectionName,
        string connectionName,
        IReadOnlyList<string> connectionPaths)
    {
        var section = configuration.GetSection(sectionName);
        var namedSection = section.GetSection(connectionName);
        var replaceConnection = connectionPaths.Any(path => namedSection.GetSection(path).Exists());

        var defaults = section.AsEnumerable(makePathsRelative: true)
            .Where(entry => !replaceConnection || !connectionPaths.Any(path => IsPathOrChild(entry.Key, path)));

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .AddInMemoryCollection(namedSection.AsEnumerable(makePathsRelative: true))
            .Build();
    }

    internal static bool IsPathOrChild(string key, string path) =>
        key.Equals(path, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a health check name that distinguishes roles, message types and service keys.</summary>
    internal static string HealthCheckName(string role, string? serviceKey) =>
        serviceKey is null ? $"Dekaf_{role}" : $"Dekaf_{role}_{serviceKey}";

    internal static string HealthCheckName<TKey, TValue>(string role, string? serviceKey) =>
        HealthCheckName($"{role}<{typeof(TKey)},{typeof(TValue)}>", serviceKey);

    /// <summary>Adds a health check once per name, so repeated registrations do not duplicate it.</summary>
    /// <returns><see langword="true"/> when the check was added.</returns>
    internal static bool TryAddHealthCheck(
        IHostApplicationBuilder builder,
        string name,
        Func<IServiceProvider, IHealthCheck> factory,
        TimeSpan? timeout = null)
    {
        var propertyKey = $"Dekaf.Aspire.HealthChecks.{name}";
        if (builder.Properties.ContainsKey(propertyKey))
            return false;

        builder.Properties[propertyKey] = true;
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(name, factory, failureStatus: null, tags: null, timeout));
        return true;
    }

    /// <summary>Subscribes OpenTelemetry to Dekaf's shared meter and activity source unless disabled.</summary>
    /// <remarks>
    /// Dekaf uses one meter and one activity source for all clients. Disabling telemetry for one
    /// registration stops it subscribing; another registration can still collect the same signals.
    /// </remarks>
    internal static void AddTelemetry(IHostApplicationBuilder builder, IDekafAspireSettings settings)
    {
        if (!settings.DisableMetrics)
            builder.Services.AddOpenTelemetry().WithMetrics(static metrics => metrics.AddDekafInstrumentation());

        if (!settings.DisableTracing)
            builder.Services.AddOpenTelemetry().WithTracing(static tracing => tracing.AddDekafInstrumentation());
    }

    internal static T Resolve<T>(IServiceProvider services, object? serviceKey) where T : notnull =>
        serviceKey is null ? services.GetRequiredService<T>() : services.GetRequiredKeyedService<T>(serviceKey);
}
