using System.Text;
using DotNet.Testcontainers.Configurations;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The Kafka container used by all <see cref="KafkaIntegrationTest"/>-derived tests.
/// The broker image tag is driven by the <c>KAFKA_TEST_IMAGE_TAG</c> environment variable
/// (default 4.3.1), so a single test binary covers the whole supported broker range:
/// PR CI runs the current release, and the NuGet release gate sweeps the supported
/// versions (4.0.2, 4.1.2, 4.2.1, 4.3.1) by setting the variable per job.
/// </summary>
public class KafkaContainerDefault : KafkaTestContainer
{
    public const string DefaultTag = "4.3.1";

    private static readonly string Tag =
        Environment.GetEnvironmentVariable("KAFKA_TEST_IMAGE_TAG") is { Length: > 0 } tag ? tag : DefaultTag;

    public override string ContainerName => $"apache/kafka:{Tag}";

    public override int Version { get; } = ParseVersion(Tag);

    /// <summary>
    /// Parses a three-part image tag like "4.2.0" into the compact form used by
    /// <see cref="SupportsKafkaAttribute"/>: major*100 + minor*10 + patch.
    /// </summary>
    internal static int ParseVersion(string tag)
    {
        var parts = tag.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException(
                $"KAFKA_TEST_IMAGE_TAG must be a three-part version like '4.2.0', but was '{tag}'.");
        }

        return major * 100 + minor * 10 + patch;
    }

    // Testcontainers.Kafka generates a startup script that sets
    // KAFKA_ADVERTISED_LISTENERS with a trailing comma when no extra
    // listeners are configured. Kafka 4.2+ (KIP-1161) rejects empty
    // values in LIST-type configs, so the trailing comma causes a
    // ConfigException. This wrapper strips the trailing comma before
    // running the normal Kafka startup sequence.
    private static readonly byte[] RunWrapperScript = Encoding.UTF8.GetBytes(
        "#!/bin/bash\n" +
        "export KAFKA_ADVERTISED_LISTENERS=$(echo \"$KAFKA_ADVERTISED_LISTENERS\" | sed 's/,$//')\n" +
        "/etc/kafka/docker/configure\n" +
        "exec /etc/kafka/docker/launch\n");

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder)
    {
        if (Version < 420)
        {
            return builder;
        }

        return builder
            .WithResourceMapping(RunWrapperScript, "/etc/kafka/docker/run", 0, 0,
                UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute |
                UnixFileModes.GroupRead | UnixFileModes.GroupExecute |
                UnixFileModes.OtherRead | UnixFileModes.OtherExecute)
            // Enable share groups (KIP-932). Requires two pieces:
            // 1. group.share.enable — gates the share group client protocol
            // 2. group.coordinator.rebalance.protocols must include "share"
            //    for the coordinator to handle share group state
            .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
            .WithEnvironment("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS", "classic,consumer,share")
            .WithEnvironment("KAFKA_GROUP_SHARE_RECORD_LOCK_DURATION_MS", "15000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MIN_RECORD_LOCK_DURATION_MS", "5000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MAX_RECORD_LOCK_DURATION_MS", "60000")
            // Single-broker: the internal __share_group_state topic requires settings
            // compatible with a single-node cluster.
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS", "3");
    }
}
