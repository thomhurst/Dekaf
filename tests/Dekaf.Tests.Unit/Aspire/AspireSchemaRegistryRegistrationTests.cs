using Dekaf.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Dekaf.Tests.Unit.Aspire.AspireTestHost;

namespace Dekaf.Tests.Unit.Aspire;

public class AspireSchemaRegistryRegistrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConnectionString_ReplacesConfiguredUrls_AndKeepsNativeOptions(bool keyed)
    {
        var builder = HostBuilder(
            ("ConnectionStrings:registry", "https://connection:8081"),
            ("Aspire:Dekaf:SchemaRegistry:Config:Urls:0", "https://shared-a:8081"),
            ("Aspire:Dekaf:SchemaRegistry:Config:Urls:1", "https://shared-b:8081"),
            ("Aspire:Dekaf:SchemaRegistry:Config:BasicAuthUserInfo", "user:secret"),
            ("Aspire:Dekaf:SchemaRegistry:registry:Config:RequestTimeoutMs", "1234"));
        SchemaRegistryConfig? captured = null;
        ISchemaRegistryClient Factory(IServiceProvider _, SchemaRegistryConfig config)
        {
            captured = config;
            return new SchemaRegistryClient(config);
        }

        if (keyed) builder.AddKeyedDekafSchemaRegistryClient("registry", clientFactory: Factory);
        else builder.AddDekafSchemaRegistryClient("registry", clientFactory: Factory);
        await using var provider = builder.Services.BuildServiceProvider();
        _ = keyed
            ? provider.GetRequiredKeyedService<ISchemaRegistryClient>("registry")
            : provider.GetRequiredService<ISchemaRegistryClient>();

        await Assert.That(captured!.Url).IsEqualTo("https://connection:8081");
        await Assert.That(captured.Urls).IsNull();
        await Assert.That(captured.BasicAuthUserInfo).IsEqualTo("user:secret");
        await Assert.That(captured.RequestTimeoutMs).IsEqualTo(1234);
    }

    [Test]
    [Arguments("Config:Url", "https://named:8081")]
    [Arguments("Config:Urls:0", "https://named:8081")]
    public async Task NamedUrls_ReplaceSharedUrls(string namedKey, string value)
    {
        var builder = HostBuilder(
            ("Aspire:Dekaf:SchemaRegistry:Config:Url", "https://shared:8081"),
            ("Aspire:Dekaf:SchemaRegistry:Config:Urls:0", "https://shared-a:8081"),
            ($"Aspire:Dekaf:SchemaRegistry:registry:{namedKey}", value));
        SchemaRegistryConfig? captured = null;
        builder.AddDekafSchemaRegistryClient("registry", clientFactory: (_, config) =>
        {
            captured = config;
            return new SchemaRegistryClient(config);
        });
        await using var provider = builder.Services.BuildServiceProvider();
        _ = provider.GetRequiredService<ISchemaRegistryClient>();

        var urls = captured!.Urls ?? [captured.Url];
        await Assert.That(urls).IsEquivalentTo([value]);
    }

    [Test]
    public async Task SettingsCallback_RunsAfterConfiguration()
    {
        var builder = HostBuilder(("ConnectionStrings:registry", "https://connection:8081"), ("Aspire:Dekaf:SchemaRegistry:DisableHealthChecks", "false"));
        builder.AddDekafSchemaRegistryClient("registry", settings =>
        {
            settings.Config = new SchemaRegistryConfig { Url = "https://code:8081" };
            settings.HealthCheck.Timeout = TimeSpan.FromSeconds(3);
        });
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(HealthCheck(provider, "Dekaf_schema_registry").Timeout).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(provider.GetRequiredService<ISchemaRegistryClient>()).IsTypeOf<SchemaRegistryClient>();
    }

    [Test]
    public async Task HealthChecks_ArePerKeyAndCanBeDisabled()
    {
        var builder = HostBuilder(
            ("ConnectionStrings:registry", "http://registry:8081"),
            ("ConnectionStrings:orders", "http://orders:8081"),
            ("ConnectionStrings:quiet", "http://quiet:8081"),
            ("Aspire:Dekaf:SchemaRegistry:quiet:DisableHealthChecks", "true"));
        builder.AddDekafSchemaRegistryClient("registry");
        builder.AddDekafSchemaRegistryClient("registry");
        builder.AddKeyedDekafSchemaRegistryClient("orders");
        builder.AddKeyedDekafSchemaRegistryClient("quiet");
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(HealthCheckNames(provider)).IsEquivalentTo(["Dekaf_schema_registry", "Dekaf_schema_registry_orders"]);
    }

    [Test]
    public async Task MissingUrl_FailsWhenTheClientIsResolved()
    {
        var builder = HostBuilder();
        builder.AddDekafSchemaRegistryClient("missing");
        await using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredService<ISchemaRegistryClient>())
            .Throws<InvalidOperationException>().WithMessageContaining("ConnectionStrings:missing");
    }
}
