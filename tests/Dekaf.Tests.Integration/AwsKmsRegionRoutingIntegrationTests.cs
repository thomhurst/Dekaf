using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Aws;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
[Timeout(30_000)]
public sealed class AwsKmsRegionRoutingIntegrationTests
{
    [Test]
    public async Task SingleStandardProvider_UsesEachKekRegionOverHttp(CancellationToken cancellationToken)
    {
        await using var endpoint = new LoopbackKmsEndpoint();
        using var provider = new AwsKmsProvider(new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            DefaultAWSCredentials = new BasicAWSCredentials("test-access-key", "test-secret-key"),
            UseHttp = true,
            MaxErrorRetry = 0,
            HttpClientFactory = new LoopbackHttpClientFactory(endpoint.Port)
        });
        byte[] material = [1, 2, 3, 4];

        foreach (var region in new[] { "us-east-1", "eu-west-2", "eu-west-1" })
        {
            var reference = new SchemaRegistryKmsKeyReference
            {
                KmsType = "aws-kms",
                KmsKeyId = $"aws-kms://arn:aws:kms:{region}:123456789012:key/integration"
            };
            var wrapped = await provider.WrapKeyAsync(material, reference, cancellationToken);
            var unwrapped = await provider.UnwrapKeyAsync(wrapped, reference, cancellationToken);
            await Assert.That(unwrapped).IsEquivalentTo(material);
        }

        await Assert.That(endpoint.RequestCount).IsEqualTo(6);
    }

    private sealed class LoopbackHttpClientFactory(int port) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new SocketsHttpHandler
        {
            UseProxy = false,
            // Preserve the SDK's URL and signing; route the socket to the test server.
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        });

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    private sealed class LoopbackKmsEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _serving;
        private int _requestCount;

        public LoopbackKmsEndpoint()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serving = ServeAsync();
        }

        public int Port { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);
                    await using var stream = connection.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _ = await reader.ReadLineAsync(_stopping.Token);
                    while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } line)
                    {
                        var separator = line.IndexOf(':');
                        headers[line[..separator]] = line[(separator + 1)..].Trim();
                    }
                    var body = new char[int.Parse(headers["Content-Length"], System.Globalization.CultureInfo.InvariantCulture)];
                    if (await reader.ReadBlockAsync(body, _stopping.Token) != body.Length)
                        throw new EndOfStreamException("Incomplete KMS request.");
                    using var document = JsonDocument.Parse(new string(body));
                    var region = document.RootElement.GetProperty("KeyId").GetString()!.Split(':')[3];
                    var routedCorrectly = headers["Host"] == $"kms.{region}.amazonaws.com" &&
                        headers["Authorization"].Contains($"/{region}/kms/aws4_request", StringComparison.Ordinal);
                    var encrypt = headers["X-Amz-Target"].EndsWith(".Encrypt", StringComparison.Ordinal);
                    var material = document.RootElement.GetProperty(encrypt ? "Plaintext" : "CiphertextBlob").GetString();
                    var responseBody = routedCorrectly
                        ? JsonSerializer.Serialize(
                            new Dictionary<string, string?> { [encrypt ? "CiphertextBlob" : "Plaintext"] = material },
                            AwsKmsRegionRoutingJsonContext.Default.DictionaryStringString)
                        : "{\"__type\":\"NotFoundException\",\"message\":\"Key is in another region\"}";
                    Interlocked.Increment(ref _requestCount);
                    var response = Encoding.UTF8.GetBytes(
                        $"HTTP/1.1 {(routedCorrectly ? "200 OK" : "400 Bad Request")}\r\nContent-Type: application/x-amz-json-1.1\r\nContent-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\nConnection: close\r\n\r\n{responseBody}");
                    await stream.WriteAsync(response, _stopping.Token);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            try
            {
                // Keep the listener active until cancellation ends the accept loop.
                await _serving;
            }
            finally
            {
                _listener.Stop();
                _stopping.Dispose();
            }
        }
    }
}

[JsonSerializable(typeof(Dictionary<string, string?>))]
internal sealed partial class AwsKmsRegionRoutingJsonContext : JsonSerializerContext;
