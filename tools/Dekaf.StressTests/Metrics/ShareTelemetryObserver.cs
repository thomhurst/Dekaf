using System.Text;
using Dekaf.Admin;
using Dekaf.Diagnostics;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Tools.Telemetry;

namespace Dekaf.StressTests.Metrics;

// HTTP polling and decoding run outside measurement. Kafka exports and the bounded
// broker receiver remain active during the identical control/candidate workloads.
internal sealed class ShareTelemetryObserver(Uri endpoint) : IDisposable
{
    internal const int IntervalMilliseconds = 1000;
    internal const int MaximumPayloads = 64;
    internal const int MaximumPayloadBytes = 65536;
    internal const string ApplicationMetric = "com.example.dekaf.stress.worker";
    internal const string BuiltInPrefix = "org.apache.kafka.consumer.share.";
    internal const string FetchMetric = BuiltInPrefix + "fetch.manager.fetch.total";
    internal const string RecordsMetric = BuiltInPrefix + "fetch.manager.records.consumed.total";
    internal const string AcknowledgementsMetric = BuiltInPrefix + "fetch.manager.acknowledgements.send.total";
    internal const string AcknowledgementErrorsMetric = BuiltInPrefix + "fetch.manager.acknowledgements.error.total";
    internal static readonly string[] RequestedMetrics = [BuiltInPrefix, ApplicationMetric];
    internal string[] ClientIds { get; } = [$"stress-telemetry-first-{Guid.NewGuid():N}", $"stress-telemetry-second-{Guid.NewGuid():N}"];
    private readonly HttpClient _http = new() { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(10) };
    private readonly Guid[] _identities = new Guid[2];

    internal async Task ConfigureAsync(string bootstrapServers, CancellationToken cancellationToken)
    {
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(bootstrapServers).Build();
        var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>();
        foreach (var client in ClientIds)
            configs.Add(new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = client },
                [ConfigAlter.Set("metrics", string.Join(',', RequestedMetrics)),
                 ConfigAlter.Set("interval.ms", IntervalMilliseconds.ToString()),
                 ConfigAlter.Set("match", $"client_id={client}")]);
        await admin.IncrementalAlterConfigsAsync(configs, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Acknowledging the config write does not prove the broker has applied it.
        var address = BootstrapServerList.Parse(bootstrapServers);
        foreach (var client in ClientIds)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await using var connection = new KafkaConnection(0, address.Host, address.Port, client,
                new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
            await connection.ConnectAsync(timeout.Token).ConfigureAwait(false);
            while (true)
            {
                var subscription = await connection.SendAsync<GetTelemetrySubscriptionsRequest, GetTelemetrySubscriptionsResponse>(
                    new GetTelemetrySubscriptionsRequest { ClientInstanceId = Guid.Empty }, 0, timeout.Token).ConfigureAwait(false);
                if (subscription.ErrorCode != ErrorCode.None)
                    throw new InvalidOperationException($"Telemetry subscription probe failed: {subscription.ErrorCode}.");
                if (subscription.PushIntervalMs == IntervalMilliseconds
                    && RequestedMetrics.All(subscription.RequestedMetrics.Contains)) break;
                await Task.Delay(100, timeout.Token).ConfigureAwait(false);
            }
        }
    }

    internal void ObserveIdentity(int worker, IKafkaShareConsumer<string, byte[]> consumer)
    {
        _identities[worker] = ((IKafkaClientInstanceIdentity)consumer).ClientInstanceId
            ?? throw new InvalidOperationException("Subscribed worker has no client instance identity.");
        if (_identities[worker] == Guid.Empty) throw new InvalidOperationException("Empty telemetry identity.");
    }

    internal async Task CaptureFailureAsync(string outputDirectory)
    {
        // A failed workload must remain failed even if its diagnostics cannot be read.
        try
        {
            var raw = await _http.GetStringAsync("payloads").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "telemetry-failure.tsv"), raw).ConfigureAwait(false);
        }
        catch (Exception error) { Console.WriteLine($"Could not retain telemetry failure evidence: {error.Message}"); }
    }

    internal async Task<ShareTelemetrySnapshot> VerifyAsync(bool terminating, string outputDirectory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var raw = await _http.GetStringAsync("payloads", timeout.Token).ConfigureAwait(false);
            // Preserve even incomplete evidence if a later read/validation times out.
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, terminating ? "telemetry-final.tsv" : "telemetry-warmup.tsv"), raw, timeout.Token).ConfigureAwait(false);
            var snapshot = Inspect(raw, ClientIds, _identities, terminating);
            if (snapshot is not null) return snapshot;
            await Task.Delay(100, timeout.Token).ConfigureAwait(false);
        }
    }

    internal static ShareTelemetrySnapshot? Inspect(string raw, string[] clients, Guid[] identities, bool requireTerminating)
    {
        if (clients.Length != 2 || identities.Length != 2 || clients.Any(string.IsNullOrEmpty)
            || clients[0] == clients[1] || identities.Any(id => id == Guid.Empty) || identities[0] == identities[1])
            throw new InvalidDataException("Telemetry requires two distinct nonempty worker identities.");
        var rows = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length > MaximumPayloads) throw new InvalidDataException("Telemetry receiver exceeded its history bound.");
        var observations = new ShareTelemetryWorkerSnapshot[clients.Length];
        for (var i = 0; i < clients.Length; i++) observations[i] = new() { ClientId = clients[i], ClientInstanceId = identities[i] };
        foreach (var row in rows)
        {
            var fields = row.Split('\t');
            if (fields.Length != 5) throw new InvalidDataException("Malformed telemetry receiver row.");
            var client = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2]));
            var worker = Array.IndexOf(clients, client);
            if (worker < 0) throw new InvalidDataException("Unexpected client exported telemetry.");
            if (Guid.Parse(fields[0]) != identities[worker]) throw new InvalidDataException("Telemetry client identity changed.");
            if (Encoding.UTF8.GetString(Convert.FromBase64String(fields[3])) != "OTLP")
                throw new InvalidDataException("Unexpected telemetry content type.");
            var bytes = Convert.FromBase64String(fields[4]);
            if (bytes.Length == 0 || bytes.Length > MaximumPayloadBytes) throw new InvalidDataException("Invalid telemetry payload size.");
            var metrics = MetricsData.Parser.ParseFrom(bytes).ResourceMetrics.SelectMany(resource => resource.ScopeMetrics)
                .SelectMany(scope => scope.Metrics).ToArray();
            var observation = observations[worker];
            var terminating = bool.Parse(fields[1]);
            var application = metrics.SingleOrDefault(metric => metric.Name == ApplicationMetric);
            if (application?.Gauge?.DataPoints.Count != 1 || application.Gauge.DataPoints[0].AsDouble != worker + 1)
                throw new InvalidDataException("Subscribed application gauge is missing or incorrect.");
            if (metrics.Any(metric => metric.Name == AcknowledgementErrorsMetric
                && (metric.Sum is null || metric.Sum.DataPoints.Any(point => Value(point) != 0))))
                throw new InvalidDataException("Broker received acknowledgement error telemetry.");
            observation.PayloadBytes += bytes.Length;
            if (terminating)
            {
                if (++observation.TerminatingPayloads > 1)
                    throw new InvalidDataException("Worker exported terminating telemetry more than once.");
                if (!metrics.Any(metric => metric.Name == FetchMetric && metric.Sum is not null))
                    throw new InvalidDataException("Terminating export omitted fetch accounting.");
            }
            else
            {
                observation.PeriodicPayloads++;
                observation.PositiveFetch |= PositiveSum(metrics, FetchMetric);
                observation.PositiveRecords |= PositiveSum(metrics, RecordsMetric);
                observation.PositiveAcknowledgements |= PositiveSum(metrics, AcknowledgementsMetric);
            }
        }
        if (observations.Any(worker => worker.PeriodicPayloads == 0 || !worker.PositiveFetch
            || !worker.PositiveRecords || !worker.PositiveAcknowledgements
            || requireTerminating && worker.TerminatingPayloads != 1)) return null;
        return new ShareTelemetrySnapshot { Workers = observations, RetainedPayloads = rows.Length };
    }

    private static bool PositiveSum(Metric[] metrics, string name) => metrics.Any(metric => metric.Name == name
        && metric.Sum is { IsMonotonic: true, AggregationTemporality: 1 } sum && sum.DataPoints.Any(point => Value(point) > 0));
    private static double Value(NumberDataPoint point) => point.ValueCase == NumberDataPoint.ValueOneofCase.AsInt ? point.AsInt : point.AsDouble;
    public void Dispose() => _http.Dispose();
}

internal sealed class ShareTelemetrySnapshot
{
    public int PushIntervalMilliseconds { get; init; } = ShareTelemetryObserver.IntervalMilliseconds;
    public int MaximumRetainedPayloads { get; init; } = ShareTelemetryObserver.MaximumPayloads;
    public int MaximumPayloadBytes { get; init; } = ShareTelemetryObserver.MaximumPayloadBytes;
    public string[] RequestedMetrics { get; init; } = ShareTelemetryObserver.RequestedMetrics;
    public required int RetainedPayloads { get; init; }
    public required ShareTelemetryWorkerSnapshot[] Workers { get; init; }
}

internal sealed class ShareTelemetryWorkerSnapshot
{
    public required string ClientId { get; init; }
    public required Guid ClientInstanceId { get; init; }
    public int PeriodicPayloads { get; set; }
    public int TerminatingPayloads { get; set; }
    public long PayloadBytes { get; set; }
    public bool PositiveFetch { get; set; }
    public bool PositiveRecords { get; set; }
    public bool PositiveAcknowledgements { get; set; }
}
