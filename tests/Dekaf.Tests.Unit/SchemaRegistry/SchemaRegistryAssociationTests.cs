using System.Net;
using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryAssociationTests
{
    [Test]
    public async Task CreateAssociationAsync_SendsCompatibleContractAndMapsResponse()
    {
        var handler = new AssociationHandler(HttpStatusCode.OK, """
            {
              "resourceName": "orders/eu",
              "resourceNamespace": "lkc-123",
              "resourceId": "lkc-123:orders/eu",
              "resourceType": "topic",
              "associations": [
                {
                  "subject": "orders-value",
                  "associationType": "value",
                  "lifecycle": "STRONG",
                  "frozen": true,
                  "schema": {
                    "schema": "{\"type\":\"object\"}",
                    "schemaType": "JSON",
                    "references": [
                      { "name": "common", "subject": "common-value", "version": 2 }
                    ]
                  }
                }
              ]
            }
            """);
        using var client = CreateClient(handler);
        var request = new AssociationCreateOrUpdateRequest
        {
            ResourceName = "orders/eu",
            ResourceNamespace = "lkc-123",
            ResourceId = "lkc-123:orders/eu",
            ResourceType = "topic",
            Associations =
            [
                new AssociationCreateOrUpdateInfo
                {
                    Subject = "orders-value",
                    AssociationType = "value",
                    Lifecycle = "STRONG",
                    Frozen = true,
                    Normalize = true,
                    Schema = new Schema
                    {
                        SchemaString = "{\"type\":\"object\"}",
                        SchemaType = SchemaType.Json,
                        References =
                        [
                            new SchemaReference
                            {
                                Name = "common",
                                Subject = "common-value",
                                Version = 2
                            }
                        ]
                    }
                }
            ]
        };

        var result = await client.CreateAssociationAsync(request);

        await Assert.That(handler.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(handler.RequestUri!.AbsolutePath).IsEqualTo("/associations");
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        await Assert.That(root.GetProperty("resourceName").GetString()).IsEqualTo("orders/eu");
        await Assert.That(root.GetProperty("resourceNamespace").GetString()).IsEqualTo("lkc-123");
        await Assert.That(root.GetProperty("resourceId").GetString()).IsEqualTo("lkc-123:orders/eu");
        await Assert.That(root.GetProperty("resourceType").GetString()).IsEqualTo("topic");
        var association = root.GetProperty("associations")[0];
        await Assert.That(association.GetProperty("subject").GetString()).IsEqualTo("orders-value");
        await Assert.That(association.GetProperty("associationType").GetString()).IsEqualTo("value");
        await Assert.That(association.GetProperty("lifecycle").GetString()).IsEqualTo("STRONG");
        await Assert.That(association.GetProperty("frozen").GetBoolean()).IsTrue();
        await Assert.That(association.GetProperty("normalize").GetBoolean()).IsTrue();
        var schema = association.GetProperty("schema");
        await Assert.That(schema.GetProperty("schema").GetString()).IsEqualTo("{\"type\":\"object\"}");
        await Assert.That(schema.GetProperty("schemaType").GetString()).IsEqualTo("JSON");
        await Assert.That(schema.GetProperty("references")[0].GetProperty("subject").GetString())
            .IsEqualTo("common-value");

        await Assert.That(result.ResourceName).IsEqualTo("orders/eu");
        await Assert.That(result.Associations).Count().IsEqualTo(1);
        await Assert.That(result.Associations[0].Subject).IsEqualTo("orders-value");
        await Assert.That(result.Associations[0].Schema!.SchemaType).IsEqualTo(SchemaType.Json);
        await Assert.That(result.Associations[0].Schema!.References![0].Version).IsEqualTo(2);
    }

    [Test]
    public async Task GetAssociationsByResourceNameAsync_UsesCompatiblePathAndRepeatedFilters()
    {
        var handler = new AssociationHandler(HttpStatusCode.OK, """
            [
              {
                "subject": "orders-key",
                "guid": "guid-1",
                "resourceName": "orders/eu",
                "resourceNamespace": "lkc 123",
                "resourceId": "resource-1",
                "resourceType": "topic",
                "associationType": "key",
                "lifecycle": "WEAK",
                "frozen": false
              }
            ]
            """);
        using var client = CreateClient(handler);

        var result = await client.GetAssociationsByResourceNameAsync(
            "orders/eu",
            "lkc 123",
            resourceType: "topic/name",
            associationTypes: ["key", "value with space"],
            lifecycle: "WEAK",
            offset: 3,
            limit: 10);

        await Assert.That(handler.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.RequestUri!.AbsolutePath)
            .IsEqualTo("/associations/resources/lkc%20123/orders%2Feu");
        await Assert.That(handler.RequestUri.Query)
            .IsEqualTo("?resourceType=topic%2Fname&associationType=key&associationType=value%20with%20space&lifecycle=WEAK&offset=3&limit=10");
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Guid).IsEqualTo("guid-1");
        await Assert.That(result[0].ResourceNamespace).IsEqualTo("lkc 123");
    }

    [Test]
    public async Task GetAssociationsByResourceNameAsync_Defaults_OmitOptionalQuery()
    {
        var handler = new AssociationHandler(HttpStatusCode.OK, "[]");
        using var client = CreateClient(handler);

        var result = await client.GetAssociationsByResourceNameAsync("orders", "-");

        await Assert.That(result).IsEmpty();
        await Assert.That(handler.RequestUri!.PathAndQuery)
            .IsEqualTo("/associations/resources/-/orders");
    }

    [Test]
    public async Task DeleteAssociationsAsync_SendsFiltersAndAcceptsNoContent()
    {
        var handler = new AssociationHandler(HttpStatusCode.NoContent, null);
        using var client = CreateClient(handler);

        await client.DeleteAssociationsAsync(
            "lkc-123:orders/eu",
            resourceType: "topic",
            associationTypes: ["key", "value"],
            cascadeLifecycle: true);

        await Assert.That(handler.Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.RequestUri!.AbsolutePath)
            .IsEqualTo("/associations/resources/lkc-123%3Aorders%2Feu");
        await Assert.That(handler.RequestUri.Query)
            .IsEqualTo("?resourceType=topic&associationType=key&associationType=value&cascadeLifecycle=true");
    }

    [Test]
    public async Task DeleteAssociationsAsync_Defaults_IncludeFalseCascade()
    {
        var handler = new AssociationHandler(HttpStatusCode.NoContent, null);
        using var client = CreateClient(handler);

        await client.DeleteAssociationsAsync("resource-1");

        await Assert.That(handler.RequestUri!.PathAndQuery)
            .IsEqualTo("/associations/resources/resource-1?cascadeLifecycle=false");
    }

    [Test]
    public async Task AssociationOperations_ValidateArguments()
    {
        using var client = CreateClient(new AssociationHandler(HttpStatusCode.OK, "[]"));

        await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync("", "-"))
            .Throws<ArgumentException>();
        await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync("orders", "", offset: 0))
            .Throws<ArgumentException>();
        await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync("orders", "-", offset: -1))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync("orders", "-", limit: -2))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () => _ = await client.CreateAssociationAsync(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => client.DeleteAssociationsAsync(""))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task GetAssociationsByResourceNameAsync_PropagatesCancellation()
    {
        var handler = new AssociationHandler(HttpStatusCode.OK, "[]");
        using var client = CreateClient(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync(
                "orders",
                "-",
                cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(handler.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task GetAssociationsByResourceNameAsync_MapsRegistryError()
    {
        var handler = new AssociationHandler(HttpStatusCode.NotFound, """
            { "error_code": 40401, "message": "association not found" }
            """);
        using var client = CreateClient(handler);

        var exception = await Assert.That(async () => _ = await client.GetAssociationsByResourceNameAsync("orders", "-"))
            .Throws<SchemaRegistryException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(40401);
        await Assert.That(exception.Message).IsEqualTo("association not found");
    }

    [Test]
    public async Task MockSchemaRegistryClient_AssociationOperationsRoundTrip()
    {
        using var client = new MockSchemaRegistryClient();
        var request = new AssociationCreateOrUpdateRequest
        {
            ResourceName = "orders",
            ResourceNamespace = "lkc-123",
            ResourceId = "lkc-123:orders",
            ResourceType = "topic",
            Associations =
            [
                new AssociationCreateOrUpdateInfo
                {
                    Subject = "orders-value",
                    AssociationType = "value",
                    Lifecycle = "STRONG"
                }
            ]
        };

        var created = await client.CreateAssociationAsync(request);
        var found = await client.GetAssociationsByResourceNameAsync(
            "orders",
            "-",
            associationTypes: ["value"]);

        await Assert.That(created.Associations[0].Subject).IsEqualTo("orders-value");
        await Assert.That(found).Count().IsEqualTo(1);
        await Assert.That(found[0].ResourceId).IsEqualTo("lkc-123:orders");

        await client.DeleteAssociationsAsync(
            "lkc-123:orders",
            associationTypes: ["value"]);
        found = await client.GetAssociationsByResourceNameAsync("orders", "lkc-123");

        await Assert.That(found).IsEmpty();
    }

    private static SchemaRegistryClient CreateClient(HttpMessageHandler handler) => new(
        new SchemaRegistryConfig { Url = "http://schema-registry.local" },
        handler);

    private sealed class AssociationHandler(HttpStatusCode statusCode, string? responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            CancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            if (request.Content is not null)
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(statusCode)
            {
                Content = responseBody is null ? null : new StringContent(responseBody)
            };
        }
    }
}
