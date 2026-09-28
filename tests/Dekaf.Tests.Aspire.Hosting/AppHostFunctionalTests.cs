using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Dekaf.Tests.Aspire.Hosting;

/// <summary>Starts the broker and registry containers. Requires Docker.</summary>
[Category("AspireHostingContainers")]
public class AppHostFunctionalTests
{
    [Test]
    [Timeout(300_000)]
    public async Task KafkaAndSchemaRegistry_BecomeHealthy(CancellationToken cancellationToken)
    {
        await using var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Dekaf_Tests_Aspire_Hosting_AppHost>(cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("messaging", cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("schema-registry", cancellationToken);

        // Talk to the registry over HTTP, independently of the Dekaf client packages.
        var url = await app.GetConnectionStringAsync("schema-registry", cancellationToken);
        // Default certificate validation: when Aspire serves the registry over HTTPS with the trusted
        // development certificate, clients must accept it without special handling.
        using var http = new HttpClient();
        var subjects = await http.GetStringAsync(new Uri(new Uri(url!), "subjects"), cancellationToken);

        await Assert.That(subjects).IsEqualTo("[]");
    }
}
