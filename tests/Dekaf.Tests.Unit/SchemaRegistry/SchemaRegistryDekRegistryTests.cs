using System.Net;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryDekRegistryTests
{
    [Test]
    public async Task RegisterKekAsync_SendsCreateRequestAndMapsResponse()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """
                {
                  "name": "payments-kek",
                  "kmsType": "aws-kms",
                  "kmsKeyId": "arn:aws:kms:us-west-2:123456789012:key/1234",
                  "kmsProps": { "region": "us-west-2" },
                  "doc": "payments",
                  "shared": true,
                  "deleted": false,
                  "ts": { "timestamp": 1631206325000 }
                }
                """);
        using var client = CreateClient(handler);

        var kek = await client.RegisterKekAsync(
            new RegisterKekRequest
            {
                Name = "payments-kek",
                KmsType = "aws-kms",
                KmsKeyId = "arn:aws:kms:us-west-2:123456789012:key/1234",
                KmsProps = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["region"] = "us-west-2"
                },
                Doc = "payments",
                Shared = true
            },
            testSharing: true);

        await Assert.That(kek.Name).IsEqualTo("payments-kek");
        await Assert.That(kek.KmsType).IsEqualTo("aws-kms");
        await Assert.That(kek.KmsProps!["region"]).IsEqualTo("us-west-2");
        await Assert.That(kek.Shared).IsTrue();
        await Assert.That(kek.Timestamp).IsEqualTo(1631206325000);

        var request = handler.Requests[0];
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Uri.PathAndQuery).IsEqualTo("/dek-registry/v1/keks?testSharing=true");

        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("payments-kek");
        await Assert.That(root.GetProperty("kmsType").GetString()).IsEqualTo("aws-kms");
        await Assert.That(root.GetProperty("kmsProps").GetProperty("region").GetString()).IsEqualTo("us-west-2");
        await Assert.That(root.GetProperty("shared").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task GetKekNamesAsync_UsesListQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """["payments-kek","orders-kek"]""");
        using var client = CreateClient(handler);

        var names = await client.GetKekNamesAsync(deleted: true, offset: 5, limit: 10);

        await Assert.That(names).IsEquivalentTo(["payments-kek", "orders-kek"]);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks?deleted=true&offset=5&limit=10");
    }

    [Test]
    public async Task GetKekAsync_UsesDeletedQueryAndMapsResponse()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """
                {
                  "name": "payments-kek",
                  "kmsType": "azure-kv",
                  "kmsKeyId": "https://vault.local/keys/payments",
                  "deleted": true,
                  "ts": 42
                }
                """);
        using var client = CreateClient(handler);

        var kek = await client.GetKekAsync("payments-kek", deleted: true);

        await Assert.That(kek.KmsType).IsEqualTo("azure-kv");
        await Assert.That(kek.Deleted).IsTrue();
        await Assert.That(kek.Timestamp).IsEqualTo(42);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek?deleted=true");
    }

    [Test]
    public async Task DeleteKekAsync_UsesPermanentQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.NoContent, "");
        using var client = CreateClient(handler);

        await client.DeleteKekAsync("payments-kek", permanent: true);

        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek?permanent=true");
    }

    [Test]
    public async Task RegisterDekAsync_SendsCreateRequestAndMapsResponse()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """
                {
                  "kekName": "payments-kek",
                  "subject": "orders-value",
                  "version": 3,
                  "algorithm": "AES256_SIV",
                  "encryptedKeyMaterial": "encrypted",
                  "keyMaterial": "plain",
                  "deleted": false,
                  "ts": 123
                }
                """);
        using var client = CreateClient(handler);

        var dek = await client.RegisterDekAsync(
            "payments-kek",
            new RegisterDekRequest
            {
                Subject = "orders-value",
                Version = 3,
                Algorithm = DekAlgorithm.Aes256Siv,
                EncryptedKeyMaterial = "encrypted"
            });

        await Assert.That(dek.KekName).IsEqualTo("payments-kek");
        await Assert.That(dek.Subject).IsEqualTo("orders-value");
        await Assert.That(dek.Version).IsEqualTo(3);
        await Assert.That(dek.Algorithm).IsEqualTo(DekAlgorithm.Aes256Siv);
        await Assert.That(dek.EncryptedKeyMaterial).IsEqualTo("encrypted");
        await Assert.That(dek.KeyMaterial).IsEqualTo("plain");
        await Assert.That(dek.Timestamp).IsEqualTo(123);

        var request = handler.Requests[0];
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Uri.PathAndQuery).IsEqualTo("/dek-registry/v1/keks/payments-kek/deks");

        using var document = JsonDocument.Parse(request.Body!);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("subject").GetString()).IsEqualTo("orders-value");
        await Assert.That(root.GetProperty("version").GetInt32()).IsEqualTo(3);
        await Assert.That(root.GetProperty("algorithm").GetString()).IsEqualTo("AES256_SIV");
        await Assert.That(root.GetProperty("encryptedKeyMaterial").GetString()).IsEqualTo("encrypted");
    }

    [Test]
    public async Task GetDekSubjectsAsync_UsesListQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, """["orders-value","customers-value"]""");
        using var client = CreateClient(handler);

        var subjects = await client.GetDekSubjectsAsync("payments-kek", deleted: true, offset: 2, limit: 20);

        await Assert.That(subjects).IsEquivalentTo(["orders-value", "customers-value"]);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks?deleted=true&offset=2&limit=20");
    }

    [Test]
    public async Task GetDekAsync_UsesAlgorithmAndDeletedQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, DekJson("payments-kek", "customer/orders-value", 4, "AES256_GCM"));
        using var client = CreateClient(handler);

        var dek = await client.GetDekAsync(
            "payments-kek",
            "customer/orders-value",
            DekAlgorithm.Aes256Gcm,
            deleted: true);

        await Assert.That(dek.Algorithm).IsEqualTo(DekAlgorithm.Aes256Gcm);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks/customer%2Forders-value?algorithm=AES256_GCM&deleted=true");
    }

    [Test]
    public async Task GetDekAsync_ByVersion_UsesVersionPath()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, DekJson("payments-kek", "orders-value", 5, "AES128_GCM"));
        using var client = CreateClient(handler);

        var dek = await client.GetDekAsync("payments-kek", "orders-value", version: 5, deleted: true);

        await Assert.That(dek.Version).IsEqualTo(5);
        await Assert.That(dek.Algorithm).IsEqualTo(DekAlgorithm.Aes128Gcm);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks/orders-value/versions/5?deleted=true");
    }

    [Test]
    public async Task GetDekVersionsAsync_UsesVersionListQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.OK, "[1,2,3]");
        using var client = CreateClient(handler);

        var versions = await client.GetDekVersionsAsync(
            "payments-kek",
            "orders-value",
            DekAlgorithm.Aes256Gcm,
            deleted: true,
            offset: 1,
            limit: 2);

        await Assert.That(versions).IsEquivalentTo([1, 2, 3]);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks/orders-value/versions?algorithm=AES256_GCM&deleted=true&offset=1&limit=2");
    }

    [Test]
    public async Task DeleteDekAsync_UsesSubjectPathWithAlgorithmAndPermanentQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.NoContent, "");
        using var client = CreateClient(handler);

        await client.DeleteDekAsync(
            "payments-kek",
            "orders-value",
            DekAlgorithm.Aes128Gcm,
            permanent: true);

        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks/orders-value?algorithm=AES128_GCM&permanent=true");
    }

    [Test]
    public async Task DeleteDekVersionAsync_UsesAlgorithmAndPermanentQuery()
    {
        using var handler = new CapturingSchemaRegistryHandler()
            .Enqueue(HttpStatusCode.NoContent, "");
        using var client = CreateClient(handler);

        await client.DeleteDekVersionAsync(
            "payments-kek",
            "orders-value",
            version: 7,
            DekAlgorithm.Aes256Siv,
            permanent: true);

        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/dek-registry/v1/keks/payments-kek/deks/orders-value/versions/7?algorithm=AES256_SIV&permanent=true");
    }

    private static SchemaRegistryClient CreateClient(HttpMessageHandler handler) =>
        new(new SchemaRegistryConfig { Url = "http://schema-registry.local" }, handler);

    private static string DekJson(string kekName, string subject, int version, string algorithm) =>
        $$"""
        {
          "kekName": "{{kekName}}",
          "subject": "{{subject}}",
          "version": {{version}},
          "algorithm": "{{algorithm}}",
          "encryptedKeyMaterial": "encrypted",
          "deleted": false,
          "ts": 123
        }
        """;

    private sealed class CapturingSchemaRegistryHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses = new();

        public List<CapturedRequest> Requests { get; } = [];

        public CapturingSchemaRegistryHandler Enqueue(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue((statusCode, content));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));

            var (statusCode, content) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "{}");

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8)
            };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);
}
