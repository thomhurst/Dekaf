using Dekaf.Pipeline;
using Dekaf.Pipeline.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Options;

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Services.Configure<NuGetOptions>(builder.Configuration.GetSection("NuGet"));
builder.ConfigureOptions(options => options with { FailureMode = FailureMode.ContinueOnFailure });

var skipUnitTests = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_UNIT_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
var skipAotSmokeTests = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_AOT_SMOKE_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
var skipIntegrationTests = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
var skipPackaging = string.Equals(
    Environment.GetEnvironmentVariable("SKIP_PACKAGING"), "true", StringComparison.OrdinalIgnoreCase);
var integrationTestCategory = Environment.GetEnvironmentVariable("INTEGRATION_TEST_CATEGORY");

// Benchmarks are opt-in: they add ~20 minutes to a run and nothing downstream consumes
// their output, so a plain build-and-test run must not pay for them.
var runBenchmarks = string.Equals(
    Environment.GetEnvironmentVariable("RUN_BENCHMARKS"), "true", StringComparison.OrdinalIgnoreCase);

// Core modules - always needed
builder.AddModule<RestoreModule>();
builder.AddModule<GenerateVersionModule>();
builder.AddModule<BuildModule>();

// Test and packaging modules are gated independently so a caller can drop one without
// losing the other. CI runs the unit tests in their own job, off the build critical path,
// while the build job still has to pack.
if (!skipUnitTests)
{
    builder.AddModule<RunUnitTestsModule>();
    builder.AddModule<RunStressTestsUnitTestsModule>();
}

if (!skipAotSmokeTests)
{
    builder.AddModule<RunAotSmokeTestsModule>();
}

if (!skipPackaging)
{
    builder.AddModule<PackModule>();
    builder.AddModule<UploadToNuGetModule>();
    builder.AddModule<CreateReleaseModule>();
}

if (runBenchmarks)
{
    builder.AddModule<RunBenchmarksModule>();
}

// Integration test modules - register only the matching category, or all if no category specified
if (!skipIntegrationTests)
{
    var integrationTestModules = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = () => builder.AddModule<RunAdminIntegrationTestsModule>(),
        ["Authentication"] = () => builder.AddModule<RunAuthenticationIntegrationTestsModule>(),
        ["Backpressure"] = () => builder.AddModule<RunBackpressureIntegrationTestsModule>(),
        ["Compression"] = () => builder.AddModule<RunCompressionIntegrationTestsModule>(),
        ["Consumer"] = () => builder.AddModule<RunConsumerIntegrationTestsModule>(),
        ["ConsumerGroup"] = () => builder.AddModule<RunConsumerGroupIntegrationTestsModule>(),
        ["ConsumerLag"] = () => builder.AddModule<RunConsumerLagIntegrationTestsModule>(),
        ["Interop"] = () => builder.AddModule<RunInteropIntegrationTestsModule>(),
        ["Messaging"] = () => builder.AddModule<RunMessagingIntegrationTestsModule>(),
        ["MessagingPatterns"] = () => builder.AddModule<RunMessagingPatternsIntegrationTestsModule>(),
        ["MessagingOrdering"] = () => builder.AddModule<RunMessagingOrderingIntegrationTestsModule>(),
        ["MultiInFlightProducer"] = () => builder.AddModule<RunMultiInFlightProducerIntegrationTestsModule>(),
        ["NetworkPartition"] = () => builder.AddModule<RunNetworkPartitionIntegrationTestsModule>(),
        ["Offsets"] = () => builder.AddModule<RunOffsetsIntegrationTestsModule>(),
        ["Producer"] = () => builder.AddModule<RunProducerIntegrationTestsModule>(),
        ["Authorization"] = () => builder.AddModule<RunAuthorizationIntegrationTestsModule>(),
        ["Resilience"] = () => builder.AddModule<RunResilienceIntegrationTestsModule>(),
        ["Retry"] = () => builder.AddModule<RunRetryIntegrationTestsModule>(),
        ["Serialization"] = () => builder.AddModule<RunSerializationIntegrationTestsModule>(),
        ["ShareConsumer"] = () => builder.AddModule<RunShareConsumerIntegrationTestsModule>(),
        ["ShareConsumerAdmin"] = () => builder.AddModule<RunShareConsumerAdminIntegrationTestsModule>(),
        ["Telemetry"] = () => builder.AddModule<RunTelemetryIntegrationTestsModule>(),
        ["Tls"] = () => builder.AddModule<RunTlsIntegrationTestsModule>(),
        ["Transaction"] = () => builder.AddModule<RunTransactionIntegrationTestsModule>(),
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
    builder.ConfigureOptions(options => options with { RunOnlyCategories = categoriesArg });
}

await builder.RunAsync();
