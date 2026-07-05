using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using Dekaf.Security.Sasl;

await AotSmoke.RunAsync();

internal static class AotSmoke
{
    public static async Task RunAsync()
    {
        using var rsa = RSA.Create(2048);
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), CreateHttpClient());

        var token = await provider.GetTokenAsync(CancellationToken.None);
        Require(token.TokenValue == "access-token", "OAuth token value mismatch.");
        Require(token.PrincipalName == "aot-principal", "OAuth principal mismatch.");

        var headers = Headers.Create("aot", "ok");
        Require(headers.Count == 1, "Header smoke failed.");
        Require(headers[0].Key == "aot", "Header key mismatch.");

        RunJsonSerializationSmoke();
        await RunJsonSchemaRegistrySmokeAsync();
    }

    private static void RunJsonSerializationSmoke()
    {
        var serializer = new Dekaf.Serialization.Json.JsonSerializer<AotOrder>(
            AotJsonContext.Default.AotOrder);
        var context = new SerializationContext
        {
            Topic = "aot-orders",
            Component = SerializationComponent.Value
        };
        var expected = new AotOrder(42, "native");
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(expected, ref buffer, context);
        var actual = serializer.Deserialize(buffer.WrittenMemory, context);

        Require(actual == expected, "JSON source-generated serde roundtrip failed.");
    }

    private static async Task RunJsonSchemaRegistrySmokeAsync()
    {
        using var registry = new InMemorySchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<AotOrder>(
            registry,
            AotOrderSchema,
            AotJsonContext.Default.AotOrder);
        await using var deserializer = new JsonSchemaRegistryDeserializer<AotOrder>(
            registry,
            AotJsonContext.Default.AotOrder);
        var context = new SerializationContext
        {
            Topic = "aot-orders",
            Component = SerializationComponent.Value
        };
        var expected = new AotOrder(7, "schema-registry");
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(expected, ref buffer, context);
        var actual = deserializer.Deserialize(buffer.WrittenMemory, context);
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));

        Require(buffer.WrittenSpan[0] == 0, "Schema Registry magic byte mismatch.");
        Require(schemaId > 0, "Schema Registry schema ID missing.");
        Require(actual == expected, "JSON Schema Registry source-generated roundtrip failed.");
    }

    private static OAuthBearerConfig CreateJwtBearerConfig(RSA rsa) =>
        new()
        {
            GrantType = OAuthBearerGrantType.JwtBearer,
            TokenEndpointUrl = "https://auth.example.test/token",
            ClientId = "aot-client",
            Scope = "kafka:produce",
            JwtBearer = new OAuthBearerJwtBearerOptions
            {
                TokenEndpoint = "https://auth.example.test/token",
                ClientId = "aot-client",
                PrivateKey = rsa,
                Audience = "kafka",
                Scopes = new List<string> { "kafka:produce" },
                AdditionalClaims = new Dictionary<string, object?>
                {
                    ["tenant"] = "aot",
                    ["enabled"] = true,
                    ["metadata"] = new Dictionary<string, object?>
                    {
                        ["region"] = "test"
                    }
                }
            }
        };

    private static HttpClient CreateHttpClient() => new(new TokenEndpointHandler())
    {
        BaseAddress = new Uri("https://auth.example.test/")
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private const string AotOrderSchema = """
        {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "customer": { "type": "string" }
          },
          "required": ["id", "customer"]
        }
        """;

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Require(body.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", StringComparison.Ordinal),
                "JWT bearer grant type missing.");
            Require(body.Contains("assertion=", StringComparison.Ordinal), "JWT assertion missing.");

            const string json = """{"access_token":"access-token","expires_in":3600,"sub":"aot-principal"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class InMemorySchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private readonly Dictionary<int, Schema> _schemasById = [];
        private readonly Dictionary<string, List<RegisteredSchema>> _schemasBySubject = [];
        private int _nextId = 1;
        private bool _disposed;

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!_schemasBySubject.TryGetValue(subject, out var schemas))
            {
                schemas = [];
                _schemasBySubject[subject] = schemas;
            }

            var id = _nextId++;
            var registered = new RegisteredSchema
            {
                Id = id,
                Subject = subject,
                Version = schemas.Count + 1,
                Schema = schema
            };

            _schemasById[id] = schema;
            schemas.Add(registered);
            return Task.FromResult(id);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_schemasById.TryGetValue(id, out var schema))
                return Task.FromResult(schema);

            throw new InvalidOperationException($"Schema {id} not found.");
        }

        public bool TryGetCachedSchema(int id, out Schema schema)
        {
            ThrowIfDisposed();
            return _schemasById.TryGetValue(id, out schema!);
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var registered = FindSchemaBySubject(subject, version);
            return Task.FromResult(registered);
        }

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_schemasBySubject.TryGetValue(subject, out var schemas))
            {
                foreach (var registered in schemas)
                {
                    if (registered.Schema.SchemaType == schema.SchemaType
                        && string.Equals(registered.Schema.SchemaString, schema.SchemaString, StringComparison.Ordinal))
                    {
                        return Task.FromResult(registered.Id);
                    }
                }
            }

            return RegisterSchemaAsync(subject, schema, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Task.FromResult<IReadOnlyList<string>>([.. _schemasBySubject.Keys]);
        }

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!_schemasBySubject.TryGetValue(subject, out var schemas))
                throw new InvalidOperationException($"Subject '{subject}' not found.");

            return Task.FromResult<IReadOnlyList<int>>([.. schemas.Select(static schema => schema.Version)]);
        }

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_schemasBySubject.Remove(subject, out var schemas))
                throw new InvalidOperationException($"Subject '{subject}' not found.");

            foreach (var registered in schemas)
            {
                _schemasById.Remove(registered.Id);
            }

            return Task.FromResult<IReadOnlyList<int>>([.. schemas.Select(static schema => schema.Version)]);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private RegisteredSchema FindSchemaBySubject(string subject, string version)
        {
            if (!_schemasBySubject.TryGetValue(subject, out var schemas) || schemas.Count == 0)
                throw new InvalidOperationException($"Subject '{subject}' not found.");

            if (string.Equals(version, "latest", StringComparison.Ordinal))
                return schemas[^1];

            if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedVersion))
                throw new InvalidOperationException($"Schema version '{version}' is invalid.");

            foreach (var schema in schemas)
            {
                if (schema.Version == requestedVersion)
                    return schema;
            }

            throw new InvalidOperationException($"Version '{version}' not found for subject '{subject}'.");
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}

internal sealed record AotOrder(int Id, string Customer);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AotOrder))]
internal sealed partial class AotJsonContext : JsonSerializerContext;
