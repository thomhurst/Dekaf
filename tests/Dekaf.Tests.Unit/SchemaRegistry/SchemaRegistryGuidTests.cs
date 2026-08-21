using System.Net;
using System.Text;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryGuidTests
{
    private static readonly Guid FirstGuid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Test]
    public async Task GetSchemaByGuidAsync_EscapesEndpointAndMapsCompleteSchema()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, """
            {
              "schema": "message Order {}",
              "schemaType": "PROTOBUF",
              "references": [
                { "name": "common.proto", "subject": "common-value", "version": 2 }
              ],
              "metadata": {
                "tags": { "order_id": [ "PII" ] },
                "properties": { "owner": "orders" },
                "sensitive": [ "owner" ]
              },
              "ruleSet": {
                "domainRules": [
                  {
                    "name": "validate",
                    "kind": "CONDITION",
                    "mode": "READ",
                    "type": "CEL",
                    "expr": "message.order_id != ''"
                  }
                ]
              }
            }
            """)));
        using var client = CreateClient(handler);

        var schema = await client.GetSchemaByGuidAsync(
            "{01234567-89ab-cdef-0123-456789abcdef}",
            "serialized/view");

        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Requests[0].RequestUri!.PathAndQuery)
            .IsEqualTo("/schemas/guids/01234567-89ab-cdef-0123-456789abcdef?format=serialized%2Fview");
        await Assert.That(schema.SchemaType).IsEqualTo(SchemaType.Protobuf);
        await Assert.That(schema.SchemaString).IsEqualTo("message Order {}");
        await Assert.That(schema.References![0].Subject).IsEqualTo("common-value");
        await Assert.That(schema.Metadata!.Properties!["owner"]).IsEqualTo("orders");
        await Assert.That(schema.RuleSet!.DomainRules![0].Expr)
            .IsEqualTo("message.order_id != ''");
    }

    [Test]
    public async Task GetSchemaByGuidAsync_NormalizesGuidForCacheHit()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{ "schema": "{}", "schemaType": "JSON" }""")));
        using var client = CreateClient(handler);

        var first = await client.GetSchemaByGuidAsync(FirstGuid.ToString("B"), "serialized");
        var second = await client.GetSchemaByGuidAsync(FirstGuid.ToString("D").ToUpperInvariant(), "serialized");

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(client.TryGetCachedSchema(FirstGuid, "serialized", out var cached)).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(first);
    }

    [Test]
    public async Task GetSchemaByGuidAsync_NormalizesEmptyFormatToDefault()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{ "schema": "{}", "schemaType": "JSON" }""")));
        using var client = CreateClient(handler);

        var first = await client.GetSchemaByGuidAsync(FirstGuid.ToString());
        var second = await client.GetSchemaByGuidAsync(FirstGuid.ToString(), string.Empty);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(handler.Requests[0].RequestUri!.Query).IsEmpty();
    }

    [Test]
    public async Task GetSchemaByGuidAsync_SeparatesFormatsAndIntegerIdentity()
    {
        using var handler = new RecordingHandler(static (request, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            request.RequestUri!.Query.Contains("serialized", StringComparison.Ordinal)
                ? """{ "schema": "serialized" }"""
                : """{ "schema": "default" }""")));
        using var client = CreateClient(handler, maxCachedSchemas: 8);
        var idSchema = new Schema { SchemaString = "integer-id" };
        client.CacheSchema(1, subject: null, idSchema);

        var defaultSchema = await client.GetSchemaByGuidAsync(FirstGuid.ToString());
        var serializedSchema = await client.GetSchemaByGuidAsync(FirstGuid.ToString(), "serialized");

        await Assert.That(defaultSchema.SchemaString).IsEqualTo("default");
        await Assert.That(serializedSchema.SchemaString).IsEqualTo("serialized");
        await Assert.That(client.TryGetCachedSchema(1, out var cachedById)).IsTrue();
        await Assert.That(cachedById).IsSameReferenceAs(idSchema);
        await Assert.That(handler.Requests).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetSchemaByIdAndSubjectAsync_SeparatesFormattedCacheEntries()
    {
        using var handler = new RecordingHandler(static (request, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            request.RequestUri!.Query.Contains("serialized", StringComparison.Ordinal)
                ? """{ "schema": "serialized" }"""
                : """{ "schema": "default" }""")));
        using var client = CreateClient(handler, maxCachedSchemas: 8);

        var defaultSchema = await client.GetSchemaAsync(42, "orders-value");
        var serializedSchema = await client.GetSchemaAsync(42, "orders-value", "serialized");
        var cachedDefault = await client.GetSchemaAsync(42, "orders-value");
        var cachedSerialized = await client.GetSchemaAsync(42, "orders-value", "serialized");

        await Assert.That(defaultSchema.SchemaString).IsEqualTo("default");
        await Assert.That(serializedSchema.SchemaString).IsEqualTo("serialized");
        await Assert.That(cachedDefault).IsSameReferenceAs(defaultSchema);
        await Assert.That(cachedSerialized).IsSameReferenceAs(serializedSchema);
        await Assert.That(handler.Requests).Count().IsEqualTo(2);
        await Assert.That(handler.Requests[0].RequestUri!.Query)
            .IsEqualTo("?subject=orders-value");
        await Assert.That(handler.Requests[1].RequestUri!.Query)
            .IsEqualTo("?subject=orders-value&format=serialized");
    }

    [Test]
    public async Task GetSchemaBySubjectAsync_RequestsSerializedFormat()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "subject": "common-value",
              "version": 1,
              "id": 7,
              "schema": "serialized"
            }
            """)));
        using var client = CreateClient(handler);

        var schema = await client.GetSchemaBySubjectAsync(
            "common-value",
            "1",
            ignoreDeletedSchemas: true,
            format: "serialized");

        await Assert.That(schema.Schema.SchemaString).IsEqualTo("serialized");
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(handler.Requests[0].RequestUri!.Query)
            .IsEqualTo("?format=serialized");
    }

    [Test]
    public async Task GetSchemaBySubjectAsync_PreservesGuidContextAndSeedsGuidCache()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, $$"""
            {
              "subject": "orders-value",
              "version": 3,
              "id": 42,
              "guid": "{{FirstGuid:D}}",
              "schema": "{}",
              "schemaType": "JSON"
            }
            """)));
        using var client = CreateClient(handler);

        var registered = await client.GetSchemaBySubjectAsync("orders-value", "3");

        await Assert.That(registered.Guid).IsEqualTo(FirstGuid.ToString("D"));
        await Assert.That(client.TryGetCachedSchema(FirstGuid, format: null, out var cached)).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(registered.Schema);
    }

    [Test]
    public async Task GetOrRegisterSchemaAsync_RegistrationFallbackSeedsGuidCache()
    {
        var requestCount = 0;
        using var handler = new RecordingHandler((_, _) => Task.FromResult(
            Interlocked.Increment(ref requestCount) == 1
                ? JsonResponse(HttpStatusCode.NotFound, """{ "error_code": 40403, "message": "not found" }""")
                : JsonResponse(HttpStatusCode.OK, $$"""{ "id": 42, "guid": "{{FirstGuid:D}}" }""")));
        using var client = CreateClient(handler);
        var schema = NewSchema("registered");

        var id = await client.GetOrRegisterSchemaAsync("orders-value", schema);

        await Assert.That(id).IsEqualTo(42);
        await Assert.That(handler.Requests).Count().IsEqualTo(2);
        await Assert.That(client.TryGetCachedSchema(FirstGuid, format: null, out var cached)).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(schema);
    }

    [Test]
    public async Task RegisterSchemaAsync_NormalizedSchemaDoesNotSeedGuidAliasFromRequest()
    {
        var requestCount = 0;
        using var handler = new RecordingHandler((_, _) => Task.FromResult(
            Interlocked.Increment(ref requestCount) == 1
                ? JsonResponse(HttpStatusCode.OK, $$"""{ "id": 42, "guid": "{{FirstGuid:D}}" }""")
                : JsonResponse(HttpStatusCode.OK, """{ "schema": "normalized", "schemaType": "JSON" }""")));
        using var client = CreateClient(handler);

        _ = await client.RegisterSchemaAsync(
            "orders-value",
            NewSchema("unnormalized"),
            normalize: true);
        var resolved = await client.GetSchemaByGuidAsync(FirstGuid.ToString("D"));

        await Assert.That(handler.Requests).Count().IsEqualTo(2);
        await Assert.That(resolved.SchemaString).IsEqualTo("normalized");
    }

    [Test]
    public async Task RegisterSchemaAsync_GuidAliasPreservesRegistrationCacheAtCapacity()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            $$"""{ "id": 42, "guid": "{{FirstGuid:D}}" }""")));
        using var client = CreateClient(handler, maxCachedSchemas: 1);
        var schema = NewSchema("registered");

        var first = await client.RegisterSchemaAsync("orders-value", schema);
        var second = await client.RegisterSchemaAsync("orders-value", schema);

        await Assert.That(first).IsEqualTo(42);
        await Assert.That(second).IsEqualTo(42);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(client.TryGetCachedSchema(FirstGuid, null, out var cached)).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(schema);
    }

    [Test]
    public async Task GuidCache_NormalizesEmptyFormatAtDirectEntryPoints()
    {
        using var client = CreateClient(new RecordingHandler());
        var schema = NewSchema("default-format");

        client.CacheGuidSchema(FirstGuid, string.Empty, schema);

        await Assert.That(client.TryGetCachedSchema(FirstGuid, format: null, out var defaultCached)).IsTrue();
        await Assert.That(defaultCached).IsSameReferenceAs(schema);
        await Assert.That(client.TryGetCachedSchema(FirstGuid, string.Empty, out var emptyCached)).IsTrue();
        await Assert.That(emptyCached).IsSameReferenceAs(schema);
        await Assert.That(client.CachedSchemaByGuidCount).IsEqualTo(1);
    }

    [Test]
    public async Task GuidCache_EvictionSharesGlobalSchemaBudget()
    {
        using var client = CreateClient(new RecordingHandler(), maxCachedSchemas: 2);
        client.CacheSchema(1, "subject-1", NewSchema("id-1"));
        client.CacheSchema(2, "subject-2", NewSchema("id-2"));
        client.CacheGuidSchema(Guid.Parse("00000000-0000-0000-0000-000000000001"), null, NewSchema("guid-1"));
        client.CacheGuidSchema(Guid.Parse("00000000-0000-0000-0000-000000000002"), null, NewSchema("guid-2"));

        client.CacheGuidSchema(Guid.Parse("00000000-0000-0000-0000-000000000003"), null, NewSchema("guid-3"));

        await Assert.That(client.CachedSchemaByGuidCount).IsEqualTo(1);
        await Assert.That(client.CachedSchemaByIdCount).IsEqualTo(0);
        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(0);
    }

    [Test]
    public async Task GuidCache_MaxCachedSchemasZeroDisablesCaching()
    {
        using var client = CreateClient(new RecordingHandler(), maxCachedSchemas: 0);

        client.CacheGuidSchema(FirstGuid, null, NewSchema("not-cached"));

        await Assert.That(client.CachedSchemaByGuidCount).IsEqualTo(0);
        await Assert.That(client.TryGetCachedSchema(FirstGuid, null, out _)).IsFalse();
    }

    [Test]
    public async Task GuidCache_ConcurrentWritesRemainBoundedAndCoherent()
    {
        const int capacity = 32;
        using var client = CreateClient(new RecordingHandler(), capacity);

        Parallel.For(0, 4_096, index =>
        {
            var guid = GuidFromInt(index);
            client.CacheGuidSchema(guid, null, NewSchema(guid.ToString()));
        });

        await Assert.That(client.CachedSchemaByGuidCount).IsLessThanOrEqualTo(capacity);
        var mismatches = 0;
        for (var index = 0; index < 4_096; index++)
        {
            var guid = GuidFromInt(index);
            if (client.TryGetCachedSchema(guid, null, out var schema))
                mismatches += Guid.Parse(schema.SchemaString) == guid ? 0 : 1;
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    [Test]
    public async Task GetSchemaByGuidAsync_PropagatesRegistryError()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.NotFound,
            """{ "error_code": 40403, "message": "Schema not found" }""")));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            client.GetSchemaByGuidAsync(FirstGuid.ToString()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(40403);
        await Assert.That(exception.Message).Contains("Schema not found");
    }

    [Test]
    public async Task GetSchemaByGuidAsync_PropagatesCallerCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        using var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource();

        var operation = client.GetSchemaByGuidAsync(FirstGuid.ToString(), cancellationToken: cancellation.Token);
        await entered.Task;
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
    }

    [Test]
    public async Task GetSchemaByGuidAsync_RejectsInvalidGuidBeforeRequest()
    {
        using var handler = new RecordingHandler();
        using var client = CreateClient(handler);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetSchemaByGuidAsync("not-a-guid"));

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task GetSchemaBySubjectAsync_RejectsInvalidResponseGuid()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "subject": "orders-value",
              "version": 1,
              "id": 42,
              "guid": "not-a-guid",
              "schema": "{}"
            }
            """)));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            client.GetSchemaBySubjectAsync("orders-value"));

        await Assert.That(exception!.Message).Contains("invalid schema GUID");
    }

    [Test]
    public async Task RegisterSchemaAsync_RejectsInvalidResponseGuid()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{ "id": 42, "guid": "not-a-guid" }""")));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            client.RegisterSchemaAsync("orders-value", NewSchema("{}")));

        await Assert.That(exception!.Message).Contains("invalid schema GUID");
    }

    [Test]
    public async Task LookupSchemaAsync_RejectsInvalidResponseGuid()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "subject": "orders-value",
              "version": 1,
              "id": 42,
              "guid": "not-a-guid",
              "schema": "{}"
            }
            """)));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            client.LookupSchemaAsync("orders-value", NewSchema("{}")));

        await Assert.That(exception!.Message).Contains("invalid schema GUID");
    }

    [Test]
    public async Task GetOrRegisterSchemaAsync_RejectsInvalidResponseGuid()
    {
        using var handler = new RecordingHandler(static (_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "subject": "orders-value",
              "version": 1,
              "id": 42,
              "guid": "not-a-guid",
              "schema": "{}"
            }
            """)));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            client.GetOrRegisterSchemaAsync("orders-value", NewSchema("{}")));

        await Assert.That(exception!.Message).Contains("invalid schema GUID");
    }

    private static SchemaRegistryClient CreateClient(
        HttpMessageHandler handler,
        int maxCachedSchemas = 1_000) =>
        new(
            new SchemaRegistryConfig
            {
                Url = "https://schema-registry.example.test",
                MaxCachedSchemas = maxCachedSchemas
            },
            handler);

    private static Schema NewSchema(string identity) => new() { SchemaString = identity };

    private static Guid GuidFromInt(int value) => new(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) => new(statusCode)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? send = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return send?.Invoke(request, cancellationToken)
                ?? Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}"));
        }
    }
}
