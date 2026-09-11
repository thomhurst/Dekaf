package dekaf.testing;

import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.net.InetSocketAddress;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.ArrayDeque;
import org.apache.kafka.common.metrics.KafkaMetric;
import org.apache.kafka.common.metrics.MetricsReporter;
import org.apache.kafka.server.telemetry.ClientTelemetry;
import org.apache.kafka.server.telemetry.ClientTelemetryReceiver;

// Test-only reporter. The broker decompresses telemetry before invoking this receiver.
// Copy the borrowed payload before returning; HTTP reads never run on Kafka's request thread.
@SuppressWarnings("removal")
public final class RecordingTelemetryReporter implements MetricsReporter, ClientTelemetry {
    private final ArrayDeque<String> payloads = new ArrayDeque<>();
    // Integration tests retain their existing history; stress runs explicitly bound it.
    private final int maximumPayloads = Integer.parseInt(System.getenv().getOrDefault("DEKAF_TELEMETRY_MAX_PAYLOADS", "0"));
    private final int maximumBytes = Integer.parseInt(System.getenv().getOrDefault("DEKAF_TELEMETRY_MAX_BYTES", "0"));
    private HttpServer server;

    public void configure(Map<String, ?> configs) {
        try {
            server = HttpServer.create(new InetSocketAddress(8080), 0);
            server.createContext("/payloads", exchange -> {
                byte[] body;
                synchronized (payloads) {
                    body = String.join("\n", payloads).getBytes(StandardCharsets.UTF_8);
                }
                exchange.getResponseHeaders().set("Content-Type", "text/plain; charset=utf-8");
                exchange.sendResponseHeaders(200, body.length);
                try (var output = exchange.getResponseBody()) { output.write(body); }
                finally { exchange.close(); }
            });
            server.start();
        } catch (IOException error) {
            throw new UncheckedIOException(error);
        }
    }

    public ClientTelemetryReceiver clientReceiver() {
        return (context, payload) -> {
            ByteBuffer data = payload.data().duplicate();
            if (maximumBytes > 0 && data.remaining() > maximumBytes)
                throw new IllegalArgumentException("Telemetry payload exceeds receiver bound");
            byte[] copy = new byte[data.remaining()];
            data.get(copy);
            var id = payload.clientInstanceId();
            synchronized (payloads) {
                if (maximumPayloads > 0 && payloads.size() == maximumPayloads) payloads.removeFirst();
                payloads.addLast(new UUID(id.getMostSignificantBits(), id.getLeastSignificantBits()) + "\t"
                + payload.isTerminating() + "\t" + encode(context.clientId()) + "\t"
                + encode(payload.contentType()) + "\t" + Base64.getEncoder().encodeToString(copy));
            }
        };
    }

    private static String encode(String value) {
        return Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8));
    }

    public void init(List<KafkaMetric> metrics) { }
    public void metricChange(KafkaMetric metric) { }
    public void metricRemoval(KafkaMetric metric) { }
    public void close() {
        if (server != null) server.stop(0);
        synchronized (payloads) { payloads.clear(); }
    }
}
