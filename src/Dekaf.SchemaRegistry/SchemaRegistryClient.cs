using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// HTTP client for Confluent Schema Registry.
/// </summary>
public sealed class SchemaRegistryClient : ISchemaRegistryClient
{
    private readonly HttpClient _httpClient;
    private readonly SchemaRegistryConfig _config;
    private readonly ConcurrentDictionary<int, Schema> _schemaByIdCache = new();
    private readonly ConcurrentDictionary<(string Subject, Schema Schema), int> _idBySchemaCache = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public SchemaRegistryClient(SchemaRegistryConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.Url.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMilliseconds(config.RequestTimeoutMs)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.schemaregistry.v1+json"));

        if (!string.IsNullOrEmpty(config.BasicAuthUserInfo))
        {
            var authBytes = Encoding.UTF8.GetBytes(config.BasicAuthUserInfo);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
    {
        var cacheKey = (subject, schema);
        if (_idBySchemaCache.TryGetValue(cacheKey, out var cachedId))
            return cachedId;

        var request = new RegisterSchemaRequest
        {
            Schema = schema.SchemaString,
            SchemaType = schema.SchemaType == SchemaType.Avro ? null : schema.SchemaType.ToString().ToUpperInvariant(),
            References = schema.References?.Select(r => new SchemaReferenceDto
            {
                Name = r.Name,
                Subject = r.Subject,
                Version = r.Version
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions",
            request,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<RegisterSchemaResponse>(
            _jsonOptions, cancellationToken).ConfigureAwait(false);

        var id = result!.Id;

        // Cache both directions
        _schemaByIdCache.TryAdd(id, schema);
        _idBySchemaCache.TryAdd(cacheKey, id);

        return id;
    }

    public async Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_schemaByIdCache.TryGetValue(id, out var cached))
            return cached;

        var response = await _httpClient.GetAsync(
            $"schemas/ids/{id}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSchemaResponse>(
            _jsonOptions, cancellationToken).ConfigureAwait(false);

        var schema = new Schema
        {
            SchemaString = result!.Schema,
            SchemaType = ParseSchemaType(result.SchemaType),
            References = result.References?.Select(r => new SchemaReference
            {
                Name = r.Name,
                Subject = r.Subject,
                Version = r.Version
            }).ToList()
        };

        _schemaByIdCache.TryAdd(id, schema);
        return schema;
    }

    public async Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            _jsonOptions, cancellationToken).ConfigureAwait(false);

        var schema = new Schema
        {
            SchemaString = result!.Schema,
            SchemaType = ParseSchemaType(result.SchemaType),
            References = result.References?.Select(r => new SchemaReference
            {
                Name = r.Name,
                Subject = r.Subject,
                Version = r.Version
            }).ToList()
        };

        // Cache the schema
        _schemaByIdCache.TryAdd(result.Id, schema);

        return new RegisteredSchema
        {
            Id = result.Id,
            Subject = result.Subject,
            Version = result.Version,
            Schema = schema
        };
    }

    public async Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
    {
        var cacheKey = (subject, schema);
        if (_idBySchemaCache.TryGetValue(cacheKey, out var cachedId))
            return cachedId;

        // Try to get existing schema first
        var request = new RegisterSchemaRequest
        {
            Schema = schema.SchemaString,
            SchemaType = schema.SchemaType == SchemaType.Avro ? null : schema.SchemaType.ToString().ToUpperInvariant(),
            References = schema.References?.Select(r => new SchemaReferenceDto
            {
                Name = r.Name,
                Subject = r.Subject,
                Version = r.Version
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"subjects/{Uri.EscapeDataString(subject)}",
            request,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Schema doesn't exist, register it
            return await RegisterSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false);
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            _jsonOptions, cancellationToken).ConfigureAwait(false);

        var id = result!.Id;

        // Cache both directions
        _schemaByIdCache.TryAdd(id, schema);
        _idBySchemaCache.TryAdd(cacheKey, id);

        return id;
    }

    public async Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("subjects", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<string>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default)
    {
        var request = new RegisterSchemaRequest
        {
            Schema = schema.SchemaString,
            SchemaType = schema.SchemaType == SchemaType.Avro ? null : schema.SchemaType.ToString().ToUpperInvariant(),
            References = schema.References?.Select(r => new SchemaReferenceDto
            {
                Name = r.Name,
                Subject = r.Subject,
                Version = r.Version
            }).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"compatibility/subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
            request,
            _jsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return true; // No existing schema, so compatible

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<CompatibilityResponse>(
            _jsonOptions, cancellationToken).ConfigureAwait(false);

        return result?.IsCompatible ?? true;
    }

    public async Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
    {
        var url = $"subjects/{Uri.EscapeDataString(subject)}";
        if (permanent)
            url += "?permanent=true";

        var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            _jsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static SchemaType ParseSchemaType(string? schemaType)
    {
        return schemaType?.ToUpperInvariant() switch
        {
            "JSON" => SchemaType.Json,
            "PROTOBUF" => SchemaType.Protobuf,
            _ => SchemaType.Avro
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? errorMessage = null;
        int? errorCode = null;

        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            errorMessage = errorResponse?.Message;
            errorCode = errorResponse?.ErrorCode;
        }
        catch
        {
            // Ignore JSON parse errors
        }

        throw new SchemaRegistryException(
            errorCode ?? (int)response.StatusCode,
            errorMessage ?? $"Schema Registry request failed with status {response.StatusCode}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }

    // DTOs for JSON serialization
    private sealed class RegisterSchemaRequest
    {
        public required string Schema { get; init; }
        public string? SchemaType { get; init; }
        public List<SchemaReferenceDto>? References { get; init; }
    }

    private sealed class RegisterSchemaResponse
    {
        public int Id { get; init; }
    }

    private sealed class GetSchemaResponse
    {
        public required string Schema { get; init; }
        public string? SchemaType { get; init; }
        public List<SchemaReferenceDto>? References { get; init; }
    }

    private sealed class GetSubjectVersionResponse
    {
        public required string Subject { get; init; }
        public int Version { get; init; }
        public int Id { get; init; }
        public required string Schema { get; init; }
        public string? SchemaType { get; init; }
        public List<SchemaReferenceDto>? References { get; init; }
    }

    private sealed class SchemaReferenceDto
    {
        public required string Name { get; init; }
        public required string Subject { get; init; }
        public int Version { get; init; }
    }

    private sealed class CompatibilityResponse
    {
        [JsonPropertyName("is_compatible")]
        public bool IsCompatible { get; init; }
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("error_code")]
        public int ErrorCode { get; init; }
        public string? Message { get; init; }
    }
}

/// <summary>
/// Configuration for Schema Registry client.
/// </summary>
public sealed class SchemaRegistryConfig
{
    /// <summary>
    /// Schema Registry URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Basic auth credentials in format "username:password".
    /// </summary>
    public string? BasicAuthUserInfo { get; init; }

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Maximum number of schemas to cache.
    /// </summary>
    public int MaxCachedSchemas { get; init; } = 1000;
}

/// <summary>
/// Exception thrown by Schema Registry operations.
/// </summary>
public sealed class SchemaRegistryException : Exception
{
    /// <summary>
    /// The Schema Registry error code.
    /// </summary>
    public int ErrorCode { get; }

    public SchemaRegistryException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public SchemaRegistryException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
