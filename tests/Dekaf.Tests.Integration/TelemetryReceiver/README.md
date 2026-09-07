# Broker telemetry receiver fixture

`TelemetryReceiverKafkaContainer` builds this test-only reporter against the exact Kafka image selected by `KAFKA_TEST_IMAGE_TAG` (default 4.3.1). Docker builds the Java classes with a JDK; the host needs Docker and the repository .NET SDK, not Java. The image and broker are owned and disposed by Testcontainers. Image construction has a five-minute deadline.

The dedicated broker enables `metric.reporters=dekaf.testing.RecordingTelemetryReporter`. It copies the borrowed payload in Kafka's telemetry callback and serves captured exports at `/payloads` on a dynamically mapped port. Each line contains the client instance UUID, terminating flag, and Base64-encoded client ID, content type, and payload. HTTP reads retain exports so concurrent tests cannot consume one another's evidence. Captures live only for the fixture's lifetime.

Tests create real `ClientMetrics` configuration resources with an exact `client_id` match and poll for their own exports with a 30-second bound. Kafka decompresses the payload before the callback and identifies its format as `OTLP`. `Protos/telemetry_metrics.proto` defines the wire-compatible subset needed to assert metric values, sums, gauges, attributes, and timestamps. It does not replace the production encoder.

Run:

```powershell
dotnet test --project tests/Dekaf.Tests.Integration --configuration Release --framework net10.0 --treenode-filter "/*/*/ClientTelemetryReceiverIntegrationTests/*"
```

The `Telemetry` category already runs in the CI messaging group. Ordinary Kafka fixtures do not enable this reporter.

References: [Kafka receiver API](https://github.com/apache/kafka/blob/4.3.1/clients/src/main/java/org/apache/kafka/server/telemetry/ClientTelemetryReceiver.java), [broker payload handling](https://github.com/apache/kafka/blob/4.3.1/clients/src/main/java/org/apache/kafka/common/requests/PushTelemetryRequest.java), [OTLP metrics schema](https://github.com/open-telemetry/opentelemetry-proto/blob/v1.9.0/opentelemetry/proto/metrics/v1/metrics.proto). The receiver interface is deprecated since Kafka 4.2 but remains available throughout the supported Kafka 4.x range; Kafka 5 will require the exporter API.
