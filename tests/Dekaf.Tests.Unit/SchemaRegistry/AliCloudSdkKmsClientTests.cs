using System.Net;
using System.Net.Sockets;
using System.Text;
using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Kms20160120;
using Dekaf.SchemaRegistry.Kms.AliCloud;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AliCloudSdkKmsClientTests
{
    [Test]
    public async Task Encrypt_MapsSdkRequestAndReturnsOpaqueCiphertext()
    {
        await using var endpoint = new KmsEndpoint("""{"CiphertextBlob":"opaque+/ciphertext=="}""");
        var ciphertext = await endpoint.Client.EncryptAsync("alias/orders", new byte[] { 0, 1, 255 });
        var request = await endpoint.Request.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(request).Contains("x-acs-action: Encrypt");
        await Assert.That(Uri.UnescapeDataString(request)).Contains("KeyId=alias/orders");
        await Assert.That(Uri.UnescapeDataString(request)).Contains("Plaintext=AAH/");
        await Assert.That(Encoding.UTF8.GetString(ciphertext)).IsEqualTo("opaque+/ciphertext==");
    }

    [Test]
    public async Task Decrypt_MapsOpaqueCiphertextAndDecodesSdkPlaintext()
    {
        await using var endpoint = new KmsEndpoint("""{"Plaintext":"AAH/"}""");
        var plaintext = await endpoint.Client.DecryptAsync("opaque+/ciphertext=="u8.ToArray());
        var request = await endpoint.Request.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(request).Contains("x-acs-action: Decrypt");
        await Assert.That(Uri.UnescapeDataString(request)).Contains("CiphertextBlob=opaque+/ciphertext==");
        await Assert.That(plaintext).IsEquivalentTo(new byte[] { 0, 1, 255 });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingSdkPayload_ReturnsEmptyForProviderValidation(bool decrypt)
    {
        await using var endpoint = new KmsEndpoint("{}");
        var result = decrypt
            ? await endpoint.Client.DecryptAsync(new byte[] { 1 })
            : await endpoint.Client.EncryptAsync("key", new byte[] { 1 });
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Decrypt_MalformedBase64_FailsInsteadOfReturningCorruptedKey()
    {
        await using var endpoint = new KmsEndpoint("""{"Plaintext":"not base64!"}""");
        await Assert.That(async () => await endpoint.Client.DecryptAsync(new byte[] { 1 }))
            .Throws<FormatException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Cancellation_StopsWaitingForAnInFlightSdkCall(bool decrypt)
    {
        await using var endpoint = new KmsEndpoint("{}", holdResponse: true);
        using var cancellation = new CancellationTokenSource();
        var pending = decrypt
            ? endpoint.Client.DecryptAsync(new byte[] { 1 }, cancellation.Token).AsTask()
            : endpoint.Client.EncryptAsync("key", new byte[] { 1 }, cancellation.Token).AsTask();
        await endpoint.Request.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        try
        {
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            endpoint.ReleaseResponse.TrySetResult();
        }
    }

    [Test]
    public async Task ServiceFailure_PreservesSdkError()
    {
        await using var endpoint = new KmsEndpoint(
            """{"Code":"InvalidKeyId.NotFound","Message":"local test key missing","RequestId":"offline"}""", status: 400);
        var exception = await Assert.That(async () => await endpoint.Client.EncryptAsync("missing", new byte[] { 1 }))
            .ThrowsException();
        await Assert.That(exception!.Message).Contains("local test key missing");
    }

    // Exercises the unmodified, non-virtual SDK methods over loopback, including SDK wire encoding.
    private sealed class KmsEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new(TimeSpan.FromSeconds(15));
        private readonly Task _server;
        public TaskCompletionSource<string> Request { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AliCloudSdkKmsClient Client { get; }

        public KmsEndpoint(string response, bool holdResponse = false, int status = 200)
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Client = new AliCloudSdkKmsClient(new Client(new Config
            {
                AccessKeyId = "offline-test-id", AccessKeySecret = "offline-test-secret",
                Endpoint = $"127.0.0.1:{port}", Protocol = "http", RegionId = "cn-chengdu",
                ReadTimeout = 5000, ConnectTimeout = 5000
            }));
            if (!holdResponse)
                ReleaseResponse.SetResult();
            _server = ServeAsync(response, status);
        }

        private async Task ServeAsync(string response, int status)
        {
            using var socket = await _listener.AcceptTcpClientAsync(_stopping.Token);
            await using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var captured = new StringBuilder();
            var contentLength = 0;
            var expectContinue = false;
            while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } line)
            {
                captured.AppendLine(line);
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(line[15..], System.Globalization.CultureInfo.InvariantCulture);
                if (line.Equals("Expect: 100-continue", StringComparison.OrdinalIgnoreCase))
                    expectContinue = true;
            }
            if (expectContinue)
                await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), _stopping.Token);
            var body = new char[contentLength];
            if (contentLength > 0)
                await reader.ReadBlockAsync(body.AsMemory(), _stopping.Token);
            captured.Append(body);
            Request.TrySetResult(captured.ToString());
            await ReleaseResponse.Task.WaitAsync(_stopping.Token);
            var bytes = Encoding.UTF8.GetBytes(response);
            var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, _stopping.Token);
            await stream.WriteAsync(bytes, _stopping.Token);
        }

        public async ValueTask DisposeAsync()
        {
            ReleaseResponse.TrySetResult();
            try { await _server.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { /* Force shutdown below without masking the test failure. */ }
            finally
            {
                await _stopping.CancelAsync();
                _listener.Stop();
                try { await _server; }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
                finally { _stopping.Dispose(); }
            }
        }
    }
}
