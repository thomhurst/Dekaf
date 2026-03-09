using Dekaf.Pipeline;
using Dekaf.Pipeline.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;
using ModularPipelines.Options;

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Services.Configure<NuGetOptions>(builder.Configuration.GetSection("NuGet"));
builder.Options.ExecutionMode = ExecutionMode.WaitForAllModules;

var skipUnitTests = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_UNIT_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
var skipIntegrationTests = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
var integrationTestCategory = Environment.GetEnvironmentVariable("INTEGRATION_TEST_CATEGORY");

// Core modules - always needed
builder.Services.AddModule<RestoreModule>();
builder.Services.AddModule<GenerateVersionModule>();
builder.Services.AddModule<BuildModule>();

// Unit test, packaging, and benchmark modules
if (!skipUnitTests)
{
    builder.Services.AddModule<RunUnitTestsModule>();
    builder.Services.AddModule<PackModule>();
    builder.Services.AddModule<UploadToNuGetModule>();
    builder.Services.AddModule<RunBenchmarksModule>();
}

// Integration test modules - register only the matching category, or all if no category specified
if (!skipIntegrationTests)
{
    var integrationTestModules = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = () => builder.Services.AddModule<RunAdminIntegrationTestsModule>(),
        ["Backpressure"] = () => builder.Services.AddModule<RunBackpressureIntegrationTestsModule>(),
        ["Compression"] = () => builder.Services.AddModule<RunCompressionIntegrationTestsModule>(),
        ["Consumer"] = () => builder.Services.AddModule<RunConsumerIntegrationTestsModule>(),
        ["ConsumerGroup"] = () => builder.Services.AddModule<RunConsumerGroupIntegrationTestsModule>(),
        ["ConsumerLag"] = () => builder.Services.AddModule<RunConsumerLagIntegrationTestsModule>(),
        ["Messaging"] = () => builder.Services.AddModule<RunMessagingIntegrationTestsModule>(),
        ["MessagingPatterns"] = () => builder.Services.AddModule<RunMessagingPatternsIntegrationTestsModule>(),
        ["MessagingOrdering"] = () => builder.Services.AddModule<RunMessagingOrderingIntegrationTestsModule>(),
        ["MultiInFlightProducer"] = () => builder.Services.AddModule<RunMultiInFlightProducerIntegrationTestsModule>(),
        ["Offsets"] = () => builder.Services.AddModule<RunOffsetsIntegrationTestsModule>(),
        ["Producer"] = () => builder.Services.AddModule<RunProducerIntegrationTestsModule>(),
        ["Resilience"] = () => builder.Services.AddModule<RunResilienceIntegrationTestsModule>(),
        ["Serialization"] = () => builder.Services.AddModule<RunSerializationIntegrationTestsModule>(),
        ["Transaction"] = () => builder.Services.AddModule<RunTransactionIntegrationTestsModule>(),
    };

    if (!string.IsNullOrEmpty(integrationTestCategory))
    {
        if (integrationTestModules.TryGetValue(integrationTestCategory, out var register))
        {
            register();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown integration test category '{integrationTestCategory}'. " +
                $"Valid categories: {string.Join(", ", integrationTestModules.Keys)}");
        }
    }
    else
    {
        // Local dev - register all integration test modules
        foreach (var register in integrationTestModules.Values)
        {
            register();
        }
    }
}

// Check for --categories argument
var categoriesArg = args.SkipWhile(a => a != "--categories").Skip(1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
if (categoriesArg.Length > 0)
{
    builder.RunCategories(categoriesArg);
}

await builder.Build().RunAsync();
