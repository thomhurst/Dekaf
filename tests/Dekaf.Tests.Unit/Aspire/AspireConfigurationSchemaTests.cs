using System.Reflection;
using System.Text.Json;
using Dekaf.Aspire;

namespace Dekaf.Tests.Unit.Aspire;

/// <summary>Keeps the shipped appsettings schemas in step with the bindable settings.</summary>
public class AspireConfigurationSchemaTests
{
    [Test]
    [Arguments("Producer", typeof(DekafProducerSettings))]
    [Arguments("Consumer", typeof(DekafConsumerSettings))]
    [Arguments("ShareConsumer", typeof(DekafShareConsumerSettings))]
    [Arguments("AdminClient", typeof(DekafAdminClientSettings))]
    [Arguments("Client", typeof(DekafClientSettings))]
    public async Task ClientSchema_DescribesEveryBindableSetting(string role, Type settingsType)
    {
        var properties = SchemaProperties("Dekaf.Aspire", role);

        await Assert.That(properties).IsEquivalentTo(BindableProperties(settingsType, role != "Client"));
    }

    [Test]
    public async Task SchemaRegistrySchema_DescribesEveryBindableSetting()
    {
        var properties = SchemaProperties("Dekaf.Aspire.SchemaRegistry", "SchemaRegistry");

        await Assert.That(properties).IsEquivalentTo(BindableProperties(typeof(DekafSchemaRegistrySettings), includeConfig: false));
    }

    [Test]
    public async Task ConsumerSchema_DescribesHealthCheckThresholds()
    {
        using var document = Load("Dekaf.Aspire");
        var healthCheck = Section(document, "Consumer").GetProperty("properties").GetProperty("HealthCheck").GetProperty("properties");

        await Assert.That(healthCheck.EnumerateObject().Select(p => p.Name))
            .IsEquivalentTo(["Timeout", "DegradedThreshold", "UnhealthyThreshold", "NoAssignmentStatus"]);
    }

    private static string[] BindableProperties(Type settingsType, bool includeConfig)
    {
        // Delegates cannot come from configuration; native options live under the open-ended Config object.
        var names = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !typeof(Delegate).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name);
        return (includeConfig ? names.Append("Config") : names).ToArray();
    }

    private static string[] SchemaProperties(string package, string role)
    {
        using var document = Load(package);
        return Section(document, role).GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
    }

    private static JsonElement Section(JsonDocument document, string role) =>
        document.RootElement.GetProperty("properties").GetProperty("Aspire").GetProperty("properties")
            .GetProperty("Dekaf").GetProperty("properties").GetProperty(role);

    private static JsonDocument Load(string package)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dekaf.sln")))
            directory = directory.Parent;
        var path = Path.Combine(directory!.FullName, "src", package, "ConfigurationSchema.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
