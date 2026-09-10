using System.Text;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
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
        var completedPolls = 0;
        var lastPayloadCount = 0;
        var lastClientPayloadCount = 0;
        try
        {
            while (true)
            {
                var text = await _http.GetStringAsync("payloads", timeout.Token);
                completedPolls++;
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                lastPayloadCount = lines.Length;
                lastClientPayloadCount = 0;
                foreach (var line in lines)
                {
                    var fields = line.Split('\t');
                    if (fields.Length != 5)
                        throw new InvalidDataException("Malformed telemetry receiver output.");
                    if (DecodeText(fields[2]) != clientId)
                        continue;
                    lastClientPayloadCount++;
                    var payload = new ReceivedTelemetry(Guid.Parse(fields[0]), bool.Parse(fields[1]),
                        DecodeText(fields[3]), Convert.FromBase64String(fields[4]));
                    if (matches(payload))
                        return payload;
                }
                await Task.Delay(100, timeout.Token);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No matching telemetry payload for {clientId} within 30 seconds. " +
                $"Completed HTTP polls: {completedPolls}; last response payloads: {lastPayloadCount}; " +
                $"payloads for this client: {lastClientPayloadCount}.", exception);
        }
    }

    internal async Task<GetTelemetrySubscriptionsResponse> ReadSubscriptionAsync(
        string clientId, CancellationToken cancellationToken)
    {
        await using var connection = CreateSubscriptionConnection(clientId);
        await connection.ConnectAsync(cancellationToken);
        return await ReadFreshSubscriptionAsync(connection, cancellationToken);
    }

    internal async Task<GetTelemetrySubscriptionsResponse> WaitForSubscriptionAsync(
        string clientId, IReadOnlyList<string> metrics, int expectedPushIntervalMs, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await using var connection = CreateSubscriptionConnection(clientId);
        GetTelemetrySubscriptionsResponse? last = null;
        try
        {
            await connection.ConnectAsync(timeout.Token);
            while (true)
            {
                last = await ReadFreshSubscriptionAsync(connection, timeout.Token);
                if (last.ErrorCode != ErrorCode.None)
                    throw new InvalidOperationException($"Telemetry subscription probe failed: {last.ErrorCode}.");
                if (last.PushIntervalMs == expectedPushIntervalMs && metrics.All(metric => last.RequestedMetrics.Contains(metric)))
                    return last;
                await Task.Delay(100, timeout.Token);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Telemetry subscription for {clientId} was not ready within 30 seconds. " +
                $"Expected interval: {expectedPushIntervalMs}; last interval: {last?.PushIntervalMs}; requested metrics: " +
                $"{string.Join(",", last?.RequestedMetrics ?? [])}.", exception);
        }
    }

    private KafkaConnection CreateSubscriptionConnection(string clientId)
    {
        var endpoint = BootstrapServerList.Parse(BootstrapServers);
        return new KafkaConnection(0, endpoint.Host, endpoint.Port, clientId,
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
    }

    // A config write can be acknowledged before the broker applies its subscription.
    // Fresh instance IDs avoid retaining an earlier empty five-minute subscription.
    private static ValueTask<GetTelemetrySubscriptionsResponse> ReadFreshSubscriptionAsync(
        KafkaConnection connection, CancellationToken cancellationToken) =>
        connection.SendAsync<GetTelemetrySubscriptionsRequest, GetTelemetrySubscriptionsResponse>(
            new GetTelemetrySubscriptionsRequest { ClientInstanceId = Guid.Empty }, 0, cancellationToken);

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
