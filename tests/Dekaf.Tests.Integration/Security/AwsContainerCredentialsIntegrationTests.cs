using System.Net;
using System.Net.Sockets;
using System.Text;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Integration.Security;

[NotInParallel("AwsEnvironment")]
public sealed class AwsContainerCredentialsIntegrationTests
{
    [Test]
    public async Task FullUri_FetchesCredentialsFromLoopbackHttpServer()
    {
        string[] names =
        [
            "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN", "AWS_ROLE_ARN",
            "AWS_WEB_IDENTITY_TOKEN_FILE", "AWS_PROFILE", "AWS_SHARED_CREDENTIALS_FILE", "AWS_CONFIG_FILE",
            "AWS_CONTAINER_CREDENTIALS_FULL_URI", "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_CONTAINER_AUTHORIZATION_TOKEN", "AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE", "AWS_EC2_METADATA_DISABLED"
        ];
        var snapshot = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<string[]>? server = null;
        try
        {
            foreach (var name in names)
                Environment.SetEnvironmentVariable(name, null);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "dekaf-test-missing-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
            Environment.SetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN", "container-token");
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", $"http://127.0.0.1:{port}/credentials");
            server = ServeCredentialsAsync(listener, timeout.Token);
            var provider = new AwsMskIamDefaultCredentialsProvider();

            var credentials = await provider.GetCredentialsAsync(timeout.Token);
            var request = await server;

            await Assert.That(credentials.AccessKeyId).IsEqualTo("loopback-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("loopback-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("loopback-token");
            await Assert.That(credentials.Expiration).IsEqualTo(DateTimeOffset.Parse("2099-01-01T00:00:00Z"));
            await Assert.That(request[0]).IsEqualTo("GET /credentials HTTP/1.1");
            await Assert.That(request).Contains("Authorization: container-token");
        }
        finally
        {
            try
            {
                await timeout.CancelAsync();
                listener.Stop();
                if (server is not null)
                {
                    try
                    {
                        await server;
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    {
                    }
                }
            }
            finally
            {
                foreach (var (name, value) in snapshot)
                    Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static async Task<string[]> ServeCredentialsAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var headers = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
            headers.Add(line);

        const string json = """
            {"AccessKeyId":"loopback-access","SecretAccessKey":"loopback-secret","Token":"loopback-token","Expiration":"2099-01-01T00:00:00Z"}
            """;
        var response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.ASCII.GetByteCount(json)}\r\nConnection: close\r\n\r\n{json}");
        await stream.WriteAsync(response, cancellationToken);
        return headers.ToArray();
    }
}
