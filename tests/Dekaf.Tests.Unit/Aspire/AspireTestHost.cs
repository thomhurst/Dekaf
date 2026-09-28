using System.Reflection;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dekaf.Tests.Unit.Aspire;

/// <summary>Builds host application builders and inspects the clients they register.</summary>
internal static class AspireTestHost
{
    internal static HostApplicationBuilder HostBuilder(params (string Key, string? Value)[] configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration.Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value)));
        return builder;
    }

    internal static IReadOnlyList<string> HealthCheckNames(IServiceProvider services) =>
        services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Select(r => r.Name).ToArray();

    internal static HealthCheckRegistration HealthCheck(IServiceProvider services, string name) =>
        services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(r => r.Name == name);

    internal static ProducerOptions Options<TKey, TValue>(IKafkaProducer<TKey, TValue> producer) =>
        (ProducerOptions)OptionsField(producer);

    internal static ConsumerOptions Options<TKey, TValue>(IKafkaConsumer<TKey, TValue> consumer) =>
        (ConsumerOptions)OptionsField(consumer);

    internal static ShareConsumerOptions Options<TKey, TValue>(IKafkaShareConsumer<TKey, TValue> consumer) =>
        (ShareConsumerOptions)OptionsField(consumer);

    internal static AdminClientOptions Options(IAdminClient admin) => (AdminClientOptions)OptionsField(admin);

    private static object OptionsField(object client) =>
        client.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
}
