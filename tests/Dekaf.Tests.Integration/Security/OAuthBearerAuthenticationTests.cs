using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests for the SASL/OAUTHBEARER authentication mechanism.
/// Verifies that producers, consumers, and admin clients can authenticate with a bearer
/// token, that messages round-trip over the authenticated connection, and that invalid
/// tokens are rejected by the broker with an <see cref="AuthenticationException"/>.
///
/// Uses a dedicated OAUTHBEARER-enabled Kafka container shared across the test session.
/// Tests are not run in parallel to avoid overwhelming the single broker.
/// </summary>
[Category("Authentication")]
[ClassDataSource<OAuthBearerKafkaContainer>(Shared = SharedType.PerTestSession)]
[NotInParallel("OAuthBearerKafka")]
public class OAuthBearerAuthenticationTests(OAuthBearerKafkaContainer oauthKafka)
{
    [Test]
    public async Task Producer_WithOAuthBearer_SuccessfullyProduces()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-producer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "oauth-key",
            Value = "oauth-value"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithOAuthBearerJwtBearer_SuccessfullyProduces()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();
        using var rsa = RSA.Create(2048);
        await using var tokenEndpoint = JwtBearerTokenEndpoint.Start(
            OAuthBearerKafkaContainer.CreateToken(OAuthBearerKafkaContainer.Principal));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-jwt-bearer-producer")
            .WithOAuthBearerJwtBearer(options =>
            {
                options.TokenEndpoint = tokenEndpoint.Url;
                options.ClientId = OAuthBearerKafkaContainer.Principal;
                options.PrivateKey = rsa;
                options.Audience = tokenEndpoint.Url;
                options.Scopes = ["kafka"];
            })
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "oauth-jwt-key",
            Value = "oauth-jwt-value"
        }, CancellationToken.None);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(tokenEndpoint.RequestCount).IsEqualTo(1);
        await Assert.That(tokenEndpoint.LastForm["grant_type"]).IsEqualTo("urn:ietf:params:oauth:grant-type:jwt-bearer");
        await Assert.That(tokenEndpoint.LastForm["assertion"].Split('.').Length).IsEqualTo(3);
    }

    [Test]
    public async Task Consumer_WithOAuthBearer_SuccessfullyConsumes()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();
        var groupId = $"oauth-consumer-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-producer-for-consumer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "oauth-consumer-key",
            Value = "oauth-consumer-value"
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-consumer")
            .WithGroupId(groupId)
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        var record = result!.Value;
        await Assert.That(record.Topic).IsEqualTo(topic);
        await Assert.That(record.Key).IsEqualTo("oauth-consumer-key");
        await Assert.That(record.Value).IsEqualTo("oauth-consumer-value");
    }

    [Test]
    public async Task AdminClient_WithOAuthBearer_SuccessfullyListsTopics()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-admin-client")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();

        var topics = await adminClient.ListTopicsAsync();

        await Assert.That(topics.Count).IsGreaterThan(0);
        await Assert.That(topics.Select(t => t.Name)).Contains(topic);
    }

    [Test]
    public async Task ProduceAndConsume_OverOAuthBearer_RoundTripsSuccessfully()
    {
        var topic = await oauthKafka.CreateTestTopicAsync();
        var groupId = $"oauth-roundtrip-group-{Guid.NewGuid():N}";
        const string expectedKey = "oauth-roundtrip-key";
        const string expectedValue = "oauth-roundtrip-value";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-roundtrip-producer")
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var produceResult = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = expectedKey,
            Value = expectedValue
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(oauthKafka.BootstrapServers)
            .WithClientId("oauth-roundtrip-consumer")
            .WithGroupId(groupId)
            .WithOAuthBearer(OAuthBearerKafkaContainer.GetTokenAsync)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumeResult = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(produceResult.Topic).IsEqualTo(topic);
        await Assert.That(produceResult.Offset).IsGreaterThanOrEqualTo(0);

        await Assert.That(consumeResult).IsNotNull();
        var record = consumeResult!.Value;
        await Assert.That(record.Key).IsEqualTo(expectedKey);
        await Assert.That(record.Value).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task Producer_WithInvalidToken_ThrowsAuthenticationException()
    {
        // The token is well-formed and not expired from the client's perspective (so it is
        // transmitted), but its JWS exp claim is in the past, so the broker rejects it.
        await Assert.ThrowsAsync<Dekaf.Errors.AuthenticationException>(async () =>
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(oauthKafka.BootstrapServers)
                .WithClientId("oauth-invalid-producer")
                .WithOAuthBearer(_ => new ValueTask<OAuthBearerToken>(
                    OAuthBearerKafkaContainer.CreateServerRejectedToken(OAuthBearerKafkaContainer.Principal)))
                .WithAcks(Acks.All)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "any-topic",
                Key = "key",
                Value = "value"
            }, CancellationToken.None);
        });
    }

    private sealed class JwtBearerTokenEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly OAuthBearerToken _token;
        private IReadOnlyDictionary<string, string>? _lastForm;
        private int _requestCount;

        private JwtBearerTokenEndpoint(TcpListener listener, string url, OAuthBearerToken token)
        {
            _listener = listener;
            Url = url;
            _token = token;
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public string Url { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);
        public IReadOnlyDictionary<string, string> LastForm => _lastForm ?? throw new InvalidOperationException("No token request received");

        public static JwtBearerTokenEndpoint Start(OAuthBearerToken token)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return new JwtBearerTokenEndpoint(listener, $"http://127.0.0.1:{port}/token", token);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();

            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                await HandleClientAsync(client, _cts.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                var stream = client.GetStream();
                var request = await ReadHttpRequestAsync(stream, cancellationToken);
                _lastForm = ParseForm(request.Body);
                Interlocked.Increment(ref _requestCount);

                var body = $$"""{"access_token":"{{_token.TokenValue}}","expires_in":3600,"sub":"{{_token.PrincipalName}}"}""";
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    "Connection: close\r\n\r\n");

                await stream.WriteAsync(headers, cancellationToken);
                await stream.WriteAsync(bodyBytes, cancellationToken);
            }
        }

        private static async Task<HttpRequest> ReadHttpRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            var readBuffer = new byte[1024];
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(readBuffer, cancellationToken);
                if (read == 0)
                    throw new InvalidOperationException("Token endpoint request ended before headers");

                buffer.Write(readBuffer, 0, read);
                headerEnd = IndexOf(buffer.GetBuffer(), (int)buffer.Length, "\r\n\r\n"u8.ToArray());
            }

            var requestBytes = buffer.ToArray();
            var headerText = Encoding.ASCII.GetString(requestBytes, 0, headerEnd);
            var contentLength = ParseContentLength(headerText);
            var bodyStart = headerEnd + 4;
            var bodyBytes = new byte[contentLength];
            var bufferedBodyLength = Math.Min(contentLength, requestBytes.Length - bodyStart);
            if (bufferedBodyLength > 0)
                Array.Copy(requestBytes, bodyStart, bodyBytes, 0, bufferedBodyLength);

            var remaining = contentLength - bufferedBodyLength;
            while (remaining > 0)
            {
                var offset = contentLength - remaining;
                var read = await stream.ReadAsync(bodyBytes.AsMemory(offset, remaining), cancellationToken);
                if (read == 0)
                    throw new InvalidOperationException("Token endpoint request ended before body");
                remaining -= read;
            }

            return new HttpRequest(Encoding.UTF8.GetString(bodyBytes));
        }

        private static int ParseContentLength(string headerText)
        {
            foreach (var line in headerText.Split("\r\n"))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    return int.Parse(line["Content-Length:".Length..].Trim());
            }

            return 0;
        }

        private static int IndexOf(byte[] buffer, int length, byte[] marker)
        {
            for (var i = 0; i <= length - marker.Length; i++)
            {
                var found = true;
                for (var j = 0; j < marker.Length; j++)
                {
                    if (buffer[i + j] != marker[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }

        private static IReadOnlyDictionary<string, string> ParseForm(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = DecodeFormValue(parts[0]);
                var value = parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty;
                result[key] = value;
            }

            return result;
        }

        private static string DecodeFormValue(string value) =>
            Uri.UnescapeDataString(value.Replace('+', ' '));

        private readonly record struct HttpRequest(string Body);
    }
}
