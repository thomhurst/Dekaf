using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dekaf.Networking;
using Dekaf.Security;

namespace Dekaf.Tests.Unit.Security;

/// <summary>
/// Tests for the custom-CA chain rebuild in <see cref="KafkaConnection.ValidateServerCertificate"/>.
/// SslStream reports <see cref="SslPolicyErrors.RemoteCertificateChainErrors"/> for any broker certificate
/// whose root is not in the OS trust store, so these tests pass that flag and a handshake chain shaped the
/// way SslStream hands it to the callback.
/// </summary>
[NotInParallel("CertificateGeneration")]
public class CustomCaServerCertificateValidationTests
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

    // One validity window, in whole seconds, for every certificate: an issued certificate may not
    // outlive its issuer, and revocation checks need a window around the current time.
    private static readonly DateTimeOffset NotBefore =
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds());
    private static readonly DateTimeOffset NotAfter = NotBefore.AddDays(31);

    [Test]
    public async Task IntermediateInHandshakeExtraStore_ValidatesAgainstRootOnlyTrust()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var intermediate = CreateCertificate("CN=Dekaf Test Intermediate", root, isCa: true);
        using var leaf = CreateCertificate("CN=broker", intermediate, isCa: false, ServerAuthOid);

        // On Linux and macOS SslStream passes the peer's intermediates in the chain's ExtraStore.
        using var handshakeChain = new X509Chain();
        handshakeChain.ChainPolicy.ExtraStore.Add(PublicOnly(intermediate));

        var isValid = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out var detail);

        await Assert.That(isValid).IsTrue();
        await Assert.That(detail).IsNull();
    }

    [Test]
    public async Task IntermediateInHandshakeChainElements_ValidatesAgainstRootOnlyTrust()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var intermediate = CreateCertificate("CN=Dekaf Test Intermediate", root, isCa: true);
        using var leaf = CreateCertificate("CN=broker", intermediate, isCa: false, ServerAuthOid);

        // A built handshake chain carries the intermediate in its elements, not in ExtraStore.
        using var handshakeChain = new X509Chain();
        handshakeChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        handshakeChain.ChainPolicy.ExtraStore.Add(PublicOnly(intermediate));
        handshakeChain.Build(PublicOnly(leaf));
        handshakeChain.ChainPolicy.ExtraStore.Clear();
        await Assert.That(handshakeChain.ChainElements.Count).IsGreaterThanOrEqualTo(2);

        var isValid = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out _);

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task IntermediateMissingFromHandshake_FailsAgainstRootOnlyTrust()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var intermediate = CreateCertificate("CN=Dekaf Test Intermediate", root, isCa: true);
        using var leaf = CreateCertificate("CN=broker", intermediate, isCa: false, ServerAuthOid);
        using var handshakeChain = new X509Chain();

        var isValid = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out var detail);

        await Assert.That(isValid).IsFalse();
        await Assert.That(detail).IsNotNull();
    }

    [Test]
    public async Task ClientAuthOnlyLeaf_IsRejected()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var leaf = CreateCertificate("CN=broker", root, isCa: false, ClientAuthOid);
        using var handshakeChain = new X509Chain();

        var isValid = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out var detail);

        await Assert.That(isValid).IsFalse();
        await Assert.That(detail).Contains(nameof(X509ChainStatusFlags.NotValidForUsage));
    }

    [Test]
    public async Task ServerAuthLeaf_FromSameRoot_IsAccepted()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var leaf = CreateCertificate("CN=broker", root, isCa: false, ServerAuthOid);
        using var handshakeChain = new X509Chain();

        var isValid = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out _);

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task OnlineRevocation_WithoutRevocationInformation_FailsValidation()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var leaf = CreateCertificate("CN=broker", root, isCa: false, ServerAuthOid);
        using var handshakeChain = new X509Chain();

        var withoutRevocation = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.NoCheck, out _);
        var withRevocation = Validate(leaf, handshakeChain, [PublicOnly(root)], X509RevocationMode.Online, out var detail);

        await Assert.That(withoutRevocation).IsTrue();
        await Assert.That(withRevocation).IsFalse();
        await Assert.That(detail).Contains(nameof(X509ChainStatusFlags.RevocationStatusUnknown));
    }

    [Test]
    public async Task OnlineRevocation_RejectsRevokedLeafAndAcceptsGoodLeaf()
    {
        await using var crlServer = CrlServer.Start();
        var rootCrlUrl = crlServer.UrlFor("root.crl");
        var intermediateCrlUrl = crlServer.UrlFor("intermediate.crl");

        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var intermediate = CreateCertificate("CN=Dekaf Test Intermediate", root, isCa: true, crlUrl: rootCrlUrl);
        using var goodLeaf = CreateCertificate("CN=broker-good", intermediate, isCa: false, ServerAuthOid, intermediateCrlUrl);
        using var revokedLeaf = CreateCertificate("CN=broker-revoked", intermediate, isCa: false, ServerAuthOid, intermediateCrlUrl);

        crlServer.Publish("root.crl", BuildCrl(root));
        crlServer.Publish("intermediate.crl", BuildCrl(intermediate, revokedLeaf));

        using var goodHandshake = new X509Chain();
        goodHandshake.ChainPolicy.ExtraStore.Add(PublicOnly(intermediate));
        using var revokedHandshake = new X509Chain();
        revokedHandshake.ChainPolicy.ExtraStore.Add(PublicOnly(intermediate));

        var goodIsValid = Validate(goodLeaf, goodHandshake, [PublicOnly(root)], X509RevocationMode.Online, out var goodDetail);
        var revokedIsValid = Validate(revokedLeaf, revokedHandshake, [PublicOnly(root)], X509RevocationMode.Online, out var revokedDetail);

        await Assert.That(goodIsValid).IsTrue().Because(goodDetail ?? string.Empty);
        await Assert.That(revokedIsValid).IsFalse();
        await Assert.That(revokedDetail).Contains(nameof(X509ChainStatusFlags.Revoked));
    }

    [Test]
    public async Task CustomCaCallback_AppliesCheckCertificateRevocation()
    {
        using var root = CreateCertificate("CN=Dekaf Test Root", issuer: null, isCa: true);
        using var leaf = CreateCertificate("CN=broker", root, isCa: false, ServerAuthOid);
        using var trustedRoot = PublicOnly(root);
        using var handshakeChain = new X509Chain();

        var withoutRevocation = await InvokeCustomCaCallbackAsync(trustedRoot, checkRevocation: false, leaf, handshakeChain);
        var withRevocation = await InvokeCustomCaCallbackAsync(trustedRoot, checkRevocation: true, leaf, handshakeChain);

        // The leaf has no revocation information, so only an online check can fail it.
        await Assert.That(withoutRevocation).IsTrue();
        await Assert.That(withRevocation).IsFalse();
    }

    private static bool Validate(
        X509Certificate2 leaf,
        X509Chain handshakeChain,
        X509Certificate2[] trustedCas,
        X509RevocationMode revocationMode,
        out string? failureDetail)
    {
        var trusted = new X509Certificate2Collection(trustedCas);
        try
        {
            return KafkaConnection.ValidateServerCertificate(
                leaf,
                handshakeChain,
                SslPolicyErrors.RemoteCertificateChainErrors,
                trusted,
                revocationMode,
                out failureDetail);
        }
        finally
        {
            foreach (var certificate in trusted)
            {
                certificate.Dispose();
            }
        }
    }

    private static async Task<bool> InvokeCustomCaCallbackAsync(
        X509Certificate2 trustedRoot,
        bool checkRevocation,
        X509Certificate2 leaf,
        X509Chain handshakeChain)
    {
        await using var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions
            {
                TlsConfig = new TlsConfig
                {
                    CaCertificateObject = trustedRoot,
                    CheckCertificateRevocation = checkRevocation
                }
            });

        var method = typeof(KafkaConnection).GetMethod(
            "BuildRemoteCertificateValidationCallback",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("BuildRemoteCertificateValidationCallback was not found.");
        var callback = (RemoteCertificateValidationCallback)method.Invoke(connection, null)!;

        return callback(connection, leaf, handshakeChain, SslPolicyErrors.RemoteCertificateChainErrors);
    }

    private static X509Certificate2 PublicOnly(X509Certificate2 certificate)
        => TestCertificateHelper.LoadCertificate(certificate.RawData);

    /// <summary>
    /// Issues a certificate with a fresh RSA key (see <see cref="TestCertificateHelper"/> for why keys
    /// are never shared). A null <paramref name="issuer"/> makes it self-signed.
    /// </summary>
    private static X509Certificate2 CreateCertificate(
        string subjectName,
        X509Certificate2? issuer,
        bool isCa,
        string? enhancedKeyUsageOid = null,
        string? crlUrl = null)
    {
        using var rsa = TestCertificateHelper.CreateRsaKey();
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            isCa
                ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign
                : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));

        var subjectKeyIdentifier = new X509SubjectKeyIdentifierExtension(request.PublicKey, false);
        request.CertificateExtensions.Add(subjectKeyIdentifier);
        request.CertificateExtensions.Add(issuer is null
            ? X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(subjectKeyIdentifier)
            : X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));

        if (enhancedKeyUsageOid is not null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(enhancedKeyUsageOid)], false));
        }

        if (crlUrl is not null)
        {
            request.CertificateExtensions.Add(
                CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([crlUrl]));
        }

        var serialNumber = new byte[9];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] = 0x01; // Positive, with no leading-zero ASN.1 encoding issue.

        if (issuer is null)
        {
            var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
            using var selfSigned = request.Create(request.SubjectName, generator, NotBefore, NotAfter, serialNumber);
            return selfSigned.CopyWithPrivateKey(rsa);
        }

        using var issued = request.Create(issuer, NotBefore, NotAfter, serialNumber);
        return issued.CopyWithPrivateKey(rsa);
    }

    private static byte[] BuildCrl(X509Certificate2 issuer, params X509Certificate2[] revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        foreach (var certificate in revoked)
        {
            builder.AddEntry(certificate, DateTimeOffset.UtcNow.AddMinutes(-5), X509RevocationReason.KeyCompromise);
        }

        return builder.Build(issuer, crlNumber: 1, DateTimeOffset.UtcNow.AddDays(7), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Minimal loopback HTTP/1.0 server for CRLs. A raw <see cref="TcpListener"/> avoids the URL ACLs
    /// HttpListener needs on Windows. Each run uses a fresh port and path prefix so OS CRL caches never
    /// serve a previous run's list.
    /// </summary>
    private sealed class CrlServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<string, byte[]> _documents = new(StringComparer.Ordinal);
        private readonly string _prefix = Guid.NewGuid().ToString("N");
        private readonly Task _acceptLoop;

        private CrlServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        public static CrlServer Start() => new();

        public string UrlFor(string name)
            => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{_prefix}/{name}";

        public void Publish(string name, byte[] document)
        {
            lock (_documents)
            {
                _documents[$"/{_prefix}/{name}"] = document;
            }
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
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                    return;
                }

                _ = ServeAsync(client);
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var path = await ReadRequestPathAsync(stream);
                    byte[]? body;
                    lock (_documents)
                    {
                        _documents.TryGetValue(path ?? string.Empty, out body);
                    }

                    var header = body is null
                        ? "HTTP/1.0 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : $"HTTP/1.0 200 OK\r\nContent-Type: application/pkix-crl\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(header), _cts.Token);
                    if (body is not null)
                    {
                        await stream.WriteAsync(body, _cts.Token);
                    }
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                }
                catch (IOException)
                {
                    // The chain engine may drop the connection early; nothing to serve.
                }
            }
        }

        private async Task<string?> ReadRequestPathAsync(NetworkStream stream)
        {
            var buffer = new byte[4096];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), _cts.Token);
                if (read == 0)
                    break;

                length += read;
                if (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) >= 0)
                    break;
            }

            // Request line: "GET /path HTTP/1.1"
            var requestLine = Encoding.ASCII.GetString(buffer, 0, length).Split("\r\n", 2)[0];
            var parts = requestLine.Split(' ');
            return parts.Length >= 2 ? parts[1] : null;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            await _acceptLoop;
            _cts.Dispose();
        }
    }
}
