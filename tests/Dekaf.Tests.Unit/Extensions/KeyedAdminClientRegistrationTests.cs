using System.Reflection;
using Dekaf.Admin;
using Dekaf.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Extensions;

public class KeyedAdminClientRegistrationTests
{
    [Test]
    public async Task KeyedRegistrations_ResolveIndependentClients()
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "config:9092", ["ClientId"] = "from-config" });
        var services = new ServiceCollection();
        services.AddSingleton(new Settings("provider:9092"));
        services.AddDekaf(builder => builder
            .AddAdminClient(admin => admin.WithBootstrapServers("default:9092"))
            .AddAdminClient("action", admin => admin.WithBootstrapServers("action:9092"))
            .AddAdminClient("options", new AdminClientOptions { BootstrapServers = ["options:9092"] })
            .AddAdminClient("config", configuration, admin => admin.WithClientId("override"))
            .AddAdminClient("provider", (provider, admin) => admin.WithBootstrapServers(provider.GetRequiredService<Settings>().Servers)));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(Options(provider.GetRequiredService<IAdminClient>()).BootstrapServers).IsEquivalentTo(["default:9092"]);
        await Assert.That(Options(Keyed(provider, "action")).BootstrapServers).IsEquivalentTo(["action:9092"]);
        await Assert.That(Options(Keyed(provider, "options")).BootstrapServers).IsEquivalentTo(["options:9092"]);
        await Assert.That(Options(Keyed(provider, "config")).BootstrapServers).IsEquivalentTo(["config:9092"]);
        await Assert.That(Options(Keyed(provider, "config")).ClientId).IsEqualTo("override");
        await Assert.That(Options(Keyed(provider, "provider")).BootstrapServers).IsEquivalentTo(["provider:9092"]);
        await Assert.That(Keyed(provider, "action")).IsSameReferenceAs(Keyed(provider, "action"));
    }

    [Test]
    public async Task ProviderOverloads_WithOptionsAndConfiguration_ApplyCallbackLast()
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "config:9092", ["ClientId"] = "from-config" });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddAdminClient("options", new AdminClientOptions { BootstrapServers = ["options:9092"], ClientId = "typed" },
                (_, admin) => admin.WithClientId("typed-override"))
            .AddAdminClient("config", configuration, (_, admin) => admin.WithClientId("config-override")));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(Options(Keyed(provider, "options")).ClientId).IsEqualTo("typed-override");
        await Assert.That(Options(Keyed(provider, "config")).ClientId).IsEqualTo("config-override");
        await Assert.That(Options(Keyed(provider, "config")).BootstrapServers).IsEquivalentTo(["config:9092"]);
    }

    [Test]
    [Arguments("controller-1:9093,controller-2:9093")]
    [Arguments(null)]
    public async Task ConfigurationBinding_AppliesBootstrapControllers(string? commaSeparated)
    {
        var values = commaSeparated is null
            ? new Dictionary<string, string?> { ["BootstrapControllers:0"] = "controller-1:9093", ["BootstrapControllers:1"] = "controller-2:9093" }
            : new Dictionary<string, string?> { ["BootstrapControllers"] = commaSeparated };
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddAdminClient(Configuration(values)));
        await using var provider = services.BuildServiceProvider();

        var options = Options(provider.GetRequiredService<IAdminClient>());

        await Assert.That(options.BootstrapControllers).IsEquivalentTo(["controller-1:9093", "controller-2:9093"]);
        await Assert.That(options.BootstrapServers).IsEmpty();
    }

    [Test]
    public async Task TypedOptionsAndFluentBuilder_ApplyBootstrapControllers()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddAdminClient("typed", new AdminClientOptions { BootstrapServers = [], BootstrapControllers = ["controller:9093"] })
            .AddAdminClient("fluent", admin => admin.WithBootstrapControllers("controller:9094")));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(Options(Keyed(provider, "typed")).BootstrapControllers).IsEquivalentTo(["controller:9093"]);
        await Assert.That(Options(Keyed(provider, "typed")).BootstrapServers).IsEmpty();
        await Assert.That(Options(Keyed(provider, "fluent")).BootstrapControllers).IsEquivalentTo(["controller:9094"]);
    }

    private static IAdminClient Keyed(IServiceProvider provider, string key) => provider.GetRequiredKeyedService<IAdminClient>(key);

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static AdminClientOptions Options(IAdminClient admin) =>
        (AdminClientOptions)admin.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(admin)!;

    private sealed record Settings(string Servers);
}
