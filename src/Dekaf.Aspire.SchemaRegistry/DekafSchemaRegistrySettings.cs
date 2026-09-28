using Dekaf.SchemaRegistry;

namespace Dekaf.Aspire;

/// <summary>Settings for a Dekaf Schema Registry client registered through Aspire.</summary>
public sealed class DekafSchemaRegistrySettings
{
    /// <summary>
    /// Gets or sets the native client configuration: registry URLs, authentication, TLS and caching.
    /// </summary>
    /// <remarks>
    /// A matching connection string replaces the configured <see cref="SchemaRegistryConfig.Url"/> and
    /// <see cref="SchemaRegistryConfig.Urls"/> before <c>configureSettings</c> runs. Native options are
    /// init-only, so assign a new instance to change them in code.
    /// </remarks>
    public SchemaRegistryConfig Config { get; set; } = new() { Url = string.Empty };

    /// <summary>Gets or sets whether the registry health check is disabled. The default is <see langword="false"/>.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Gets or sets the registry health check options.</summary>
    public DekafSchemaRegistryHealthCheckOptions HealthCheck { get; set; } = new();
}
