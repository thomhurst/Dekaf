using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A broker that stores Streams topology descriptions (KIP-1331) with a test plugin built on the in-memory
/// plugin that Kafka 4.4+ ships. The plugin fails deletion for groups whose ID starts with
/// <see cref="FailingDeleteGroupPrefix"/>; see <c>tools/Shared/TopologyDescriptionPlugin</c>. Older brokers have
/// no plugin API and use the plain image.
/// </summary>
public sealed class TopologyDescriptionKafkaContainer : KafkaContainerDefault
{
    public const string FailingDeleteGroupPrefix = "dekaf-delete-fails-";

    private static readonly bool PluginSupported = SupportsVersion(ImageTag, 440);

    private readonly IFutureDockerImage? _image = PluginSupported
        ? new ImageFromDockerfileBuilder()
            .WithName($"apache/kafka:dekaf-topology-description-{Guid.NewGuid():N}")
            .WithDockerfileDirectory(Path.Combine(AppContext.BaseDirectory, "TopologyDescriptionPlugin"))
            .WithBuildArgument("KAFKA_IMAGE", $"apache/kafka:{ImageTag}")
            .Build()
        : null;

    public override string ContainerName => _image?.FullName ?? base.ContainerName;

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) =>
        _image is null
            ? base.ConfigureBuilder(builder)
            : base.ConfigureBuilder(builder)
                .WithEnvironment(
                    "KAFKA_GROUP_STREAMS_TOPOLOGY_DESCRIPTION_PLUGIN_CLASS",
                    "dekaf.testing.FailingDeleteTopologyDescriptionPlugin");

    public override async Task InitializeAsync()
    {
        try
        {
            if (_image is not null)
            {
                using var imageBuildTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await _image.CreateAsync(imageBuildTimeout.Token);
            }

            await base.InitializeAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally
        {
            if (_image is not null)
                await _image.DisposeAsync();
        }
    }
}
