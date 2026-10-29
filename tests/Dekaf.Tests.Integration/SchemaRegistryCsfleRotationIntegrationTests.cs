using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Serialization")]
public sealed class SchemaRegistryCsfleRotationIntegrationTests
{
    [Test]
    [Arguments(DekAlgorithm.Aes256Gcm)]
    [Arguments(DekAlgorithm.Aes256Siv)]
    public async Task FreshWriter_ReusesLatestVersionAndReadsHistoricalCiphertext(DekAlgorithm algorithm)
    {
        using var endpoint = new DekRegistryEndpoint(algorithm);
        using var client = CreateClient(endpoint);
        var unversionedContext = CreateContext(algorithm, rotate: false);
        var payload = "historical payload"u8.ToArray();
        var historical = CreateHandler(client).TransformSerializedPayload(payload, unversionedContext).ToArray();
        endpoint.AddVersion(2);
        var rotatingContext = CreateContext(algorithm, rotate: true);

        var encrypted = CreateHandler(client).TransformSerializedPayload(payload, rotatingContext).ToArray();
        var reader = CreateHandler(client);

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(encrypted.AsSpan(1, 4))).IsEqualTo(2);
        await Assert.That(endpoint.RegistrationAttempts).IsEqualTo(0);
        await Assert.That(endpoint.Requests).Contains($"{DekRegistryEndpoint.DekPath}/versions/-1?algorithm={endpoint.AlgorithmName}");
        await Assert.That(reader.TransformDeserializedPayload(encrypted, rotatingContext).ToArray()).IsEquivalentTo(payload);
        await Assert.That(reader.TransformDeserializedPayload(historical, unversionedContext).ToArray()).IsEquivalentTo(payload);
        // New nonrotating writers must also keep using v1 after rotation has created v2.
        var unversioned = CreateHandler(client).TransformSerializedPayload(payload, unversionedContext).ToArray();
        await Assert.That(CreateHandler(client).TransformDeserializedPayload(unversioned, unversionedContext).ToArray()).IsEquivalentTo(payload);
        await Assert.That(endpoint.Requests.Count(path => path == $"{DekRegistryEndpoint.DekPath}?algorithm={endpoint.AlgorithmName}")).IsEqualTo(4);
    }

    [Test]
    [Arguments(DekAlgorithm.Aes256Gcm, false)]
    [Arguments(DekAlgorithm.Aes256Gcm, true)]
    [Arguments(DekAlgorithm.Aes256Siv, false)]
    [Arguments(DekAlgorithm.Aes256Siv, true)]
    public async Task SecondRotation_RegistersVersionThree(DekAlgorithm algorithm, bool reuseWriter)
    {
        // v2 is valid under the first policy, then expires when the policy tightens.
        // This exercises cached-key expiry without reflection or clock waits under NativeAOT too.
        using var endpoint = new DekRegistryEndpoint(algorithm) { VersionTwoAge = TimeSpan.FromHours(36) };
        using var client = CreateClient(endpoint);
        var context = CreateContext(algorithm, rotate: true, expiryDays: 2);
        var payload = "rotating payload"u8.ToArray();
        var writer = CreateHandler(client);
        var versionTwo = writer.TransformSerializedPayload(payload, context).ToArray();
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(versionTwo.AsSpan(1, 4))).IsEqualTo(2);
        context = CreateContext(algorithm, rotate: true);
        if (!reuseWriter)
            writer = CreateHandler(client);

        var versionThree = writer.TransformSerializedPayload(payload, context).ToArray();

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(versionThree.AsSpan(1, 4))).IsEqualTo(3);
        await Assert.That(endpoint.RegisteredVersions).IsEquivalentTo([2, 3]);
        await Assert.That(endpoint.Requests.Count(path => path == $"{DekRegistryEndpoint.DekPath}/versions/-1?algorithm={endpoint.AlgorithmName}")).IsEqualTo(2);
        var reader = CreateHandler(client);
        await Assert.That(reader.TransformDeserializedPayload(versionThree, context).ToArray()).IsEquivalentTo(payload);
        await Assert.That(reader.TransformDeserializedPayload(versionTwo, context).ToArray()).IsEquivalentTo(payload);
    }

    private static SchemaRegistryClient CreateClient(HttpMessageHandler endpoint) =>
        new(new SchemaRegistryConfig { Url = "http://dek-registry.test" }, endpoint);

    private static SchemaRegistryCsfleRuleHandler CreateHandler(ISchemaRegistryClient client) =>
        new(client, [new LocalKmsProvider(new Dictionary<string, byte[]>
        {
            ["local://payments"] = new byte[32]
        })]);

    private static SchemaRegistryRuleHandlerContext CreateContext(DekAlgorithm algorithm, bool rotate, int expiryDays = 1) => new()
    {
        Rule = new SchemaRule
        {
            Name = "encrypt",
            Type = SchemaRegistryCsfleRuleHandler.EncryptRuleType,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.WriteRead,
            Parameters = new Dictionary<string, string>
            {
                ["encrypt.kek.name"] = "payments-kek",
                ["encrypt.dek.algorithm"] = algorithm == DekAlgorithm.Aes256Siv ? "AES256_SIV" : "AES256_GCM",
                ["encrypt.dek.expiry.days"] = rotate ? expiryDays.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0"
            }
        },
        Direction = SchemaRegistryRuleDirection.Write,
        PayloadContext = new SchemaRegistryRuleContext
        {
            Topic = "orders",
            Subject = "orders-value",
            Component = SerializationComponent.Value,
            SchemaId = 1,
            PayloadFormat = SchemaRegistryPayloadFormat.Custom
        }
    };

    private sealed class DekRegistryEndpoint : HttpMessageHandler
    {
        internal const string DekPath = "/dek-registry/v1/keks/payments-kek/deks/orders-value";
        private readonly Dictionary<int, (string? Key, string? EncryptedKey, long Timestamp)> _versions = new();

        internal DekRegistryEndpoint(DekAlgorithm algorithm)
        {
            AlgorithmName = algorithm == DekAlgorithm.Aes256Siv ? "AES256_SIV" : "AES256_GCM";
            AddVersion(1);
            ExpireLatestVersion();
        }

        internal string AlgorithmName { get; }
        internal List<string> Requests { get; } = [];
        internal List<int> RegisteredVersions { get; } = [];
        internal int RegistrationAttempts { get; private set; }
        internal TimeSpan VersionTwoAge { get; init; }

        internal void AddVersion(int version) => _versions.Add(version,
            (Convert.ToBase64String(Enumerable.Repeat((byte)version, AlgorithmName == "AES256_SIV" ? 64 : 32).ToArray()),
                null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        internal void ExpireLatestVersion()
        {
            var latest = _versions.Keys.Max();
            var dek = _versions[latest];
            _versions[latest] = (dek.Key, dek.EncryptedKey, DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds());
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri.PathAndQuery);
            if (Requests.Count > 32)
                throw new InvalidOperationException("DEK resolution did not converge within 32 HTTP requests.");
            if (request.Method == HttpMethod.Get && uri.AbsolutePath == "/dek-registry/v1/keks/payments-kek")
                return Json(HttpStatusCode.OK, """{"name":"payments-kek","kmsType":"local-kms","kmsKeyId":"local://payments"}""");

            int version;
            if (request.Method == HttpMethod.Post && uri.AbsolutePath == "/dek-registry/v1/keks/payments-kek/deks")
            {
                RegistrationAttempts++;
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var root = body.RootElement;
                version = root.TryGetProperty("version", out var requestedVersion) ? requestedVersion.GetInt32() : 1;
                if (_versions.ContainsKey(version))
                    return Json(HttpStatusCode.Conflict, """{"error_code":40972,"message":"DEK already exists"}""");
                var timestamp = DateTimeOffset.UtcNow.Subtract(version == 2 ? VersionTwoAge : TimeSpan.Zero).ToUnixTimeMilliseconds();
                _versions.Add(version, (null, root.GetProperty("encryptedKeyMaterial").GetString(), timestamp));
                RegisteredVersions.Add(version);
            }
            else if (request.Method == HttpMethod.Get && uri.AbsolutePath == DekPath)
                version = 1; // Confluent's versionless endpoint defaults to v1, not latest.
            else if (request.Method == HttpMethod.Get && uri.AbsolutePath.StartsWith(DekPath + "/versions/", StringComparison.Ordinal))
            {
                version = int.Parse(uri.AbsolutePath.AsSpan((DekPath + "/versions/").Length), System.Globalization.CultureInfo.InvariantCulture);
                if (version == -1)
                    version = _versions.Keys.Max();
            }
            else
                throw new InvalidOperationException($"Unexpected registry request: {request.Method} {uri}");

            if (!_versions.TryGetValue(version, out var dek))
                return Json(HttpStatusCode.NotFound, """{"error_code":40471,"message":"DEK not found"}""");
            return Json(HttpStatusCode.OK, $$"""
                {"kekName":"payments-kek","subject":"orders-value","version":{{version}},"algorithm":"{{AlgorithmName}}",
                 "keyMaterial":{{(dek.Key is null ? "null" : $"\"{dek.Key}\"")}},
                 "encryptedKeyMaterial":{{(dek.EncryptedKey is null ? "null" : $"\"{dek.EncryptedKey}\"")}},"ts":{{dek.Timestamp}}}
                """);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
