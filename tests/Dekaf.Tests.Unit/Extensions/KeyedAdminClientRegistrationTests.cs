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

    [Test]
    public async Task ProviderOverloads_WithOptionsAndConfiguration_ResolveServicesFromProvider()
    {
        var configuration = Configuration(new() { ["BootstrapServers"] = "config:9092" });
        var services = new ServiceCollection();
        services.AddSingleton(new Settings("provider-client"));
        services.AddDekaf(builder => builder
            .AddAdminClient("options", new AdminClientOptions { BootstrapServers = ["options:9092"] },
                (provider, admin) => admin.WithClientId(provider.GetRequiredService<Settings>().Servers))
            .AddAdminClient("config", configuration,
                (provider, admin) => admin.WithClientId(provider.GetRequiredService<Settings>().Servers)));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(Options(Keyed(provider, "options")).ClientId).IsEqualTo("provider-client");
        await Assert.That(Options(Keyed(provider, "options")).BootstrapServers).IsEquivalentTo(["options:9092"]);
        await Assert.That(Options(Keyed(provider, "config")).ClientId).IsEqualTo("provider-client");
    }

    [Test]
    public async Task KeyedRegistrations_DoNotRegisterUnkeyedClient_AndAreSingletonsPerKey()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddAdminClient("first", admin => { calls++; admin.WithBootstrapServers("first:9092"); })
            .AddAdminClient("second", (_, admin) => { calls++; admin.WithBootstrapServers("second:9092"); }));
        await Assert.That(calls).IsEqualTo(0);
        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetService<IAdminClient>()).IsNull();
        await Assert.That(Keyed(provider, "first")).IsSameReferenceAs(Keyed(provider, "first"));
        await Assert.That(Keyed(provider, "second")).IsSameReferenceAs(Keyed(provider, "second"));
        await Assert.That(Keyed(provider, "first")).IsNotSameReferenceAs(Keyed(provider, "second"));
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task KeyedClients_AreDisposedWithProvider()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddAdminClient("orders", admin => admin.WithBootstrapServers("orders:9092")));
        var provider = services.BuildServiceProvider();
        var admin = Keyed(provider, "orders");

        await provider.DisposeAsync();

        await Assert.That(async () => await admin.ListTopicsAsync()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ConfigurationWithServersAndControllers_IsRejected()
    {
        var configuration = Configuration(new()
        {
            ["BootstrapServers"] = "broker:9092",
            ["BootstrapControllers"] = "controller:9093"
        });
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder
            .AddAdminClient("config", configuration)
            .AddAdminClient("typed", new AdminClientOptions { BootstrapServers = ["broker:9092"], BootstrapControllers = ["controller:9093"] })
            .AddAdminClient("fluent", admin => admin.WithBootstrapServers("broker:9092").WithBootstrapControllers("controller:9093")));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(() => Keyed(provider, "config")).Throws<InvalidOperationException>().WithMessageContaining("mutually exclusive");
        await Assert.That(() => Keyed(provider, "typed")).Throws<InvalidOperationException>().WithMessageContaining("mutually exclusive");
        await Assert.That(() => Keyed(provider, "fluent")).Throws<InvalidOperationException>().WithMessageContaining("mutually exclusive");
    }

    [Test]
    public async Task KeyedOverloads_RejectNullArguments()
    {
        var services = new ServiceCollection();
        DekafBuilder builder = null!;
        services.AddDekaf(dekaf => builder = dekaf);
        var registered = services.Count;
        var configuration = Configuration([]);
        var options = new AdminClientOptions { BootstrapServers = ["broker:9092"] };
        Action<AdminClientServiceBuilder> configure = _ => { };
        Action<IServiceProvider, AdminClientServiceBuilder> providerConfigure = (_, _) => { };

        await Assert.That(() => builder.AddAdminClient((object)null!, configure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", (Action<AdminClientServiceBuilder>)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient((object)null!, options)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", (AdminClientOptions)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient((object)null!, configuration)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", (IConfiguration)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient((object)null!, providerConfigure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", (Action<IServiceProvider, AdminClientServiceBuilder>)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient((object)null!, options, providerConfigure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", options, (Action<IServiceProvider, AdminClientServiceBuilder>)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient((object)null!, configuration, providerConfigure)).Throws<ArgumentNullException>();
        await Assert.That(() => builder.AddAdminClient("key", configuration, (Action<IServiceProvider, AdminClientServiceBuilder>)null!)).Throws<ArgumentNullException>();
        await Assert.That(services.Count).IsEqualTo(registered);
    }

    private static IAdminClient Keyed(IServiceProvider provider, string key) => provider.GetRequiredKeyedService<IAdminClient>(key);

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static AdminClientOptions Options(IAdminClient admin) =>
        (AdminClientOptions)admin.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(admin)!;

    private sealed record Settings(string Servers);
}
