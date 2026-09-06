using System.Net;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistrySubjectDeletionTests
{
    private static readonly Schema StringSchema = new() { SchemaString = "\"string\"", SchemaType = SchemaType.Avro };
    private static readonly Schema IntSchema = new() { SchemaString = "\"int\"", SchemaType = SchemaType.Avro };

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RegisterDeleteRegister_RestoresSubject(bool normalize, bool permanent)
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        await client.RegisterSchemaAsync("events", StringSchema, normalize);

        await client.DeleteSubjectAsync("events", permanent);
        await Assert.That(registry.ContainsSubject("events")).IsFalse();
        await client.RegisterSchemaAsync("events", StringSchema, normalize);

        await Assert.That(registry.Registrations("events")).IsEqualTo(2);
        await Assert.That(registry.ContainsSubject("events")).IsTrue();
    }

    [Test]
    public async Task Delete_InvalidatesEverySubjectEntryAndPreservesUnrelatedIdentities()
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        foreach (var schema in new[] { StringSchema, IntSchema })
        {
            await client.RegisterSchemaAsync("events", schema, normalize: false);
            await client.RegisterSchemaAsync("events", schema, normalize: true);
        }
        var sharedId = await client.RegisterSchemaAsync("events-other", StringSchema);
        await client.GetSchemaAsync(sharedId, "events");
        await client.GetSchemaAsync(sharedId, "events", "serialized");
        await client.GetSchemaAsync(sharedId, "events-other");

        await client.DeleteSubjectAsync("events", permanent: true);

        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(1);
        await Assert.That(client.CachedSchemaBySubjectAndIdCount).IsEqualTo(1);
        await Assert.That(client.TryGetCachedSchema(sharedId, "events", out _)).IsFalse();
        await Assert.That(client.TryGetCachedSchema(sharedId, "events-other", out _)).IsTrue();
        await Assert.That(client.TryGetCachedSchema(sharedId, out _)).IsTrue();
        await Assert.That(client.TryGetCachedSchema(StatefulRegistry.SchemaGuid(sharedId), null, out _)).IsTrue();
        await client.RegisterSchemaAsync("events-other", StringSchema);
        await Assert.That(registry.Registrations("events-other")).IsEqualTo(1);

        foreach (var schema in new[] { StringSchema, IntSchema })
        {
            await client.RegisterSchemaAsync("events", schema, normalize: false);
            await client.RegisterSchemaAsync("events", schema, normalize: true);
        }
        await Assert.That(registry.Registrations("events")).IsEqualTo(8);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedDelete_PreservesCachedRegistrationsAndSubjectLookups(bool permanent)
    {
        using var registry = new StatefulRegistry { FailDeletion = true };
        using var client = CreateClient(registry);
        var id = await client.RegisterSchemaAsync("events", StringSchema);
        var schema = await client.GetSchemaAsync(id, "events");

        await Assert.ThrowsAsync<SchemaRegistryException>(() => client.DeleteSubjectAsync("events", permanent));

        await Assert.That(await client.RegisterSchemaAsync("events", StringSchema)).IsEqualTo(id);
        await Assert.That(client.TryGetCachedSchema(id, "events", out var cached)).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(schema);
        await Assert.That(registry.Registrations("events")).IsEqualTo(1);
        await Assert.That(registry.ContainsSubject("events")).IsTrue();
    }

    [Test]
    [Arguments("register")]
    [Arguments("get-or-register")]
    [Arguments("lookup")]
    [Arguments("subject-id")]
    [Arguments("formatted-id")]
    [Arguments("formatted-version")]
    public async Task CompletionFromBeforeDeletion_CannotRestoreSubjectCache(string operation)
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        var id = registry.Seed("events", StringSchema.SchemaString);
        var paused = registry.PauseNext("events");
        var pending = ReadOrRegister(client, operation, "events", id);
        try
        {
            await paused.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await client.DeleteSubjectAsync("events");
        }
        finally
        {
            paused.Release.TrySetResult();
        }
        await pending;

        await Assert.That(client.CachedSchemaBySubjectAndIdCount).IsEqualTo(0);
        var registrations = registry.Registrations("events");
        await client.RegisterSchemaAsync("events", StringSchema);
        await Assert.That(registry.Registrations("events")).IsEqualTo(registrations + 1);
        await Assert.That(registry.ContainsSubject("events")).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnaffectedInFlightRegistration_RemainsCacheable(bool failedDelete)
    {
        using var registry = new StatefulRegistry { FailDeletion = failedDelete };
        using var client = CreateClient(registry);
        registry.Seed("events", StringSchema.SchemaString);
        var requestSubject = failedDelete ? "events" : "unrelated";
        var paused = registry.PauseNext(requestSubject);
        var pending = client.RegisterSchemaAsync(requestSubject, StringSchema);
        try
        {
            await paused.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (failedDelete)
                await Assert.ThrowsAsync<SchemaRegistryException>(() => client.DeleteSubjectAsync("events"));
            else
                await client.DeleteSubjectAsync("events");
        }
        finally
        {
            paused.Release.TrySetResult();
        }
        await pending;

        await client.RegisterSchemaAsync(requestSubject, StringSchema);
        await Assert.That(registry.Registrations(requestSubject)).IsEqualTo(1);
    }

    [Test]
    public async Task NewRegistrationDuringOlderCompletion_RemainsCached()
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        var paused = registry.PauseNext("events");
        var oldRequest = client.RegisterSchemaAsync("events", StringSchema);
        try
        {
            await paused.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await client.DeleteSubjectAsync("events");
            await client.RegisterSchemaAsync("events", IntSchema);
        }
        finally
        {
            paused.Release.TrySetResult();
        }
        await oldRequest;

        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(1);
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(0);
        await client.RegisterSchemaAsync("events", IntSchema);
        await Assert.That(registry.Registrations("events")).IsEqualTo(2);
        await client.RegisterSchemaAsync("events", StringSchema);
        await Assert.That(registry.Registrations("events")).IsEqualTo(3);
    }

    [Test]
    public async Task SuccessfulDeleteWithUnreadableBody_StillInvalidatesSubject()
    {
        using var registry = new StatefulRegistry { MalformedDeletionResponse = true };
        using var client = CreateClient(registry);
        await client.RegisterSchemaAsync("events", StringSchema);

        await Assert.ThrowsAsync<JsonException>(() => client.DeleteSubjectAsync("events"));
        await client.RegisterSchemaAsync("events", StringSchema);

        await Assert.That(registry.Registrations("events")).IsEqualTo(2);
        await Assert.That(registry.ContainsSubject("events")).IsTrue();
    }

    [Test]
    public async Task RequestTracking_IsReleasedAfterCancellationFailureAndRepeatedDeletion()
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        using var cancellation = new CancellationTokenSource();
        var paused = registry.PauseNext("cancelled");
        var pending = client.RegisterSchemaAsync("cancelled", StringSchema, cancellation.Token);
        await paused.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(1);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(0);

        await Assert.ThrowsAsync<SchemaRegistryException>(() => client.LookupSchemaAsync("missing", StringSchema));
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(0);
        for (var index = 0; index < 50; index++)
        {
            var subject = $"temporary-{index}";
            await client.RegisterSchemaAsync(subject, StringSchema);
            await client.DeleteSubjectAsync(subject);
        }
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(0);
        await Assert.That(client.CachedSchemaIdCount).IsEqualTo(0);
    }

    [Test]
    public async Task FormattedDeletedVersionLookup_DoesNotCacheActiveSubjectMembership()
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry);
        registry.Seed("events", StringSchema.SchemaString);

        await client.GetSchemaBySubjectAsync("events", "latest", ignoreDeletedSchemas: false, format: "serialized");

        await Assert.That(client.CachedSchemaBySubjectAndIdCount).IsEqualTo(0);
        await Assert.That(client.ActiveSubjectCacheRequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task ConfiguredNormalization_IsInvalidatedAfterDeletion()
    {
        using var registry = new StatefulRegistry();
        using var client = CreateClient(registry, normalize: true);
        await client.RegisterSchemaAsync("events", StringSchema);
        await client.DeleteSubjectAsync("events");
        await client.GetOrRegisterSchemaAsync("events", StringSchema);

        await Assert.That(registry.Registrations("events")).IsEqualTo(2);
        await Assert.That(registry.ContainsSubject("events")).IsTrue();
    }

    private static SchemaRegistryClient CreateClient(StatefulRegistry registry, bool normalize = false) => new(new SchemaRegistryConfig
    {
        Url = "https://registry.test", NormalizeSchemas = normalize
    }, registry);

    private static async Task ReadOrRegister(SchemaRegistryClient client, string operation, string subject, int id)
    {
        switch (operation)
        {
            case "register": await client.RegisterSchemaAsync(subject, StringSchema); break;
            case "get-or-register": await client.GetOrRegisterSchemaAsync(subject, StringSchema); break;
            case "lookup": await client.LookupSchemaAsync(subject, StringSchema); break;
            case "subject-id": await client.GetSchemaAsync(id, subject); break;
            case "formatted-id": await client.GetSchemaAsync(id, subject, "serialized"); break;
            case "formatted-version": await client.GetSchemaBySubjectAsync(subject, "latest", true, "serialized"); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private sealed class PausedResponse
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class StatefulRegistry : HttpMessageHandler
    {
        private static readonly int[] DeletedVersions = [1];
        private readonly object _gate = new();
        private readonly Dictionary<string, Dictionary<string, int>> _subjects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _registrations = new(StringComparer.Ordinal);
        private (string Subject, PausedResponse Response)? _nextPause;
        public bool FailDeletion { get; init; }
        public bool MalformedDeletionResponse { get; init; }

        public static Guid SchemaGuid(int id) => Guid.Parse(id.ToString("x32"));

        public int Seed(string subject, string schema)
        {
            lock (_gate)
            {
                if (!_ids.TryGetValue(schema, out var id))
                    _ids.Add(schema, id = _ids.Count + 1);
                if (!_subjects.TryGetValue(subject, out var schemas))
                    _subjects.Add(subject, schemas = new(StringComparer.Ordinal));
                schemas[schema] = id;
                return id;
            }
        }

        public bool ContainsSubject(string subject)
        {
            lock (_gate) return _subjects.ContainsKey(subject);
        }

        public int Registrations(string subject)
        {
            lock (_gate) return _registrations.GetValueOrDefault(subject);
        }

        public PausedResponse PauseNext(string subject)
        {
            lock (_gate)
            {
                var response = new PausedResponse();
                _nextPause = (subject, response);
                return response;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? schema = null;
            if (request.Content is not null)
            {
                using var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
                schema = document.RootElement.GetProperty("schema").GetString();
            }
            var parts = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
            var subject = parts[0] == "subjects" ? Uri.UnescapeDataString(parts[1]) : QuerySubject(request.RequestUri);
            HttpResponseMessage response;
            PausedResponse? pause = null;
            lock (_gate)
            {
                response = Respond(request.Method, parts, subject, schema);
                if (request.Method != HttpMethod.Delete && _nextPause is { } next && next.Subject == subject)
                {
                    pause = next.Response;
                    _nextPause = null;
                }
            }
            if (pause is not null)
            {
                pause.Entered.TrySetResult();
                try
                {
                    await pause.Release.Task.WaitAsync(cancellationToken);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }
            return response;
        }

        private HttpResponseMessage Respond(HttpMethod method, string[] parts, string subject, string? schema)
        {
            if (method == HttpMethod.Delete)
            {
                if (FailDeletion) return Json(new { error_code = 40301, message = "denied" }, HttpStatusCode.Forbidden);
                if (!_subjects.Remove(subject)) return Missing();
                if (MalformedDeletionResponse) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") };
                return Json(DeletedVersions);
            }
            if (method == HttpMethod.Post && parts.Length == 3)
            {
                _registrations[subject] = _registrations.GetValueOrDefault(subject) + 1;
                var id = Seed(subject, schema!);
                return Json(new { id, guid = SchemaGuid(id).ToString("D") });
            }
            if (!_subjects.TryGetValue(subject, out var schemas)) return Missing();
            if (parts[0] == "schemas")
            {
                var id = int.Parse(parts[2]);
                foreach (var pair in schemas)
                    if (pair.Value == id) return SchemaResponse(subject, pair.Key, id);
                return Missing();
            }
            if (schema is null)
            {
                var first = schemas.First();
                return SchemaResponse(subject, first.Key, first.Value);
            }
            return schemas.TryGetValue(schema, out var schemaId) ? SchemaResponse(subject, schema, schemaId) : Missing();
        }

        private static string QuerySubject(Uri uri)
        {
            foreach (var part in uri.Query.TrimStart('?').Split('&'))
                if (part.StartsWith("subject=", StringComparison.Ordinal)) return Uri.UnescapeDataString(part[8..]);
            throw new InvalidOperationException("Expected subject-qualified lookup");
        }

        private static HttpResponseMessage SchemaResponse(string subject, string schema, int id) => Json(new
        {
            subject, schema, id, version = 1, schemaType = "AVRO", guid = SchemaGuid(id).ToString("D")
        });

        private static HttpResponseMessage Missing() => Json(new { error_code = 40401, message = "missing subject" }, HttpStatusCode.NotFound);

        private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/vnd.schemaregistry.v1+json")
        };
    }
}
