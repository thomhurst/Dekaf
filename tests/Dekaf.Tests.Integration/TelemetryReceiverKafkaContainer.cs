using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

/// <summary>A dedicated broker that records real KIP-714 exports for telemetry tests.</summary>
public sealed class TelemetryReceiverKafkaContainer : KafkaContainerDefault
{
    private readonly IFutureDockerImage _image = new ImageFromDockerfileBuilder()
        .WithName($"apache/kafka:dekaf-telemetry-{Guid.NewGuid():N}")
        .WithDockerfileDirectory(Path.Combine(AppContext.BaseDirectory, "TelemetryReceiver"))
        .WithBuildArgument("KAFKA_IMAGE", $"apache/kafka:{ImageTag}")
        .Build();
    private readonly HttpClient _http = new();

    public override string ContainerName => _image.FullName;

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) =>
        base.ConfigureBuilder(builder)
            .WithPortBinding(8080, true)
            .WithEnvironment("KAFKA_METRIC_REPORTERS", "dekaf.testing.RecordingTelemetryReporter");

    public override async Task InitializeAsync()
    {
        try
        {
            using var imageBuildTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _image.CreateAsync(imageBuildTimeout.Token);
            await base.InitializeAsync();
            _http.BaseAddress = new UriBuilder("http", ContainerInstance!.Hostname,
                ContainerInstance.GetMappedPublicPort(8080)).Uri;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    internal async Task<ReceivedTelemetry> WaitForPayloadAsync(
        string clientId, Func<ReceivedTelemetry, bool> matches, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var text = await _http.GetStringAsync("payloads", timeout.Token);
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split('\t');
                if (fields.Length != 5)
                    throw new InvalidDataException("Malformed telemetry receiver output.");
                if (DecodeText(fields[2]) != clientId)
                    continue;
                var payload = new ReceivedTelemetry(Guid.Parse(fields[0]), bool.Parse(fields[1]),
                    DecodeText(fields[3]), Convert.FromBase64String(fields[4]));
                if (matches(payload))
                    return payload;
            }
            await Task.Delay(100, timeout.Token);
        }
    }

    private static string DecodeText(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally
        {
            _http.Dispose();
            await _image.DisposeAsync();
        }
    }
}

internal sealed record ReceivedTelemetry(Guid ClientInstanceId, bool IsTerminating, string ContentType, byte[] Data);
