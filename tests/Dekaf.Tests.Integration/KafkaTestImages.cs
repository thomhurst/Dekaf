namespace Dekaf.Tests.Integration;

internal static class KafkaTestImages
{
    public const string LaneEnvironmentVariable = "DEKAF_TEST_KAFKA_LANE";
    public const string FloorLane = "floor";
    public const string CurrentLane = "current";

    // renovate: datasource=docker packageName=apache/kafka depName=apache-kafka-floor
    public const string FloorImage = "apache/kafka:4.0.2@sha256:836cafdad9f4825880d7cf1d5a21202915ae2527bd0ef1c3600c526ed7814d1f";

    // renovate: datasource=docker packageName=apache/kafka depName=apache-kafka-current
    public const string CurrentImage = "apache/kafka:4.3.1@sha256:77e3df9054047a88b520d0cc46e16696d3b22022e1d580aeccd2632df6532837";

    private static readonly KafkaTestImage s_floor = Parse(FloorImage);
    private static readonly KafkaTestImage s_current = Parse(CurrentImage);

    public static int FloorVersionNumber => s_floor.VersionNumber;
    public static int CurrentVersionNumber => s_current.VersionNumber;

    public static KafkaTestImage Resolve(string? requestedLane)
    {
        return requestedLane switch
        {
            null or "" or CurrentLane => s_current,
            FloorLane => s_floor,
            _ => throw new InvalidOperationException(
                $"Unsupported Kafka test lane '{requestedLane}'. Supported lanes: {FloorLane}, {CurrentLane}.")
        };
    }

    private static KafkaTestImage Parse(string image)
    {
        var digestSeparator = image.IndexOf('@');
        var tagSeparator = digestSeparator > 0 ? image.LastIndexOf(':', digestSeparator) : -1;
        if (tagSeparator < 0 || digestSeparator <= tagSeparator + 1)
            throw new InvalidOperationException($"Kafka image '{image}' must contain a version tag and digest.");

        var release = image[(tagSeparator + 1)..digestSeparator];
        var components = release.Split('.');
        if (components.Length != 3 ||
            !int.TryParse(components[0], out var major) ||
            !int.TryParse(components[1], out var minor) ||
            !int.TryParse(components[2], out var patch))
        {
            throw new InvalidOperationException($"Kafka image '{image}' must use a major.minor.patch tag.");
        }

        var versionNumber = checked(major * 100 + minor * 10 + patch);
        return new KafkaTestImage(image, release, versionNumber);
    }
}

internal readonly record struct KafkaTestImage(string Image, string Release, int VersionNumber);
