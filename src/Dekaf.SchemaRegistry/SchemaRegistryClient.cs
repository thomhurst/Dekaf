using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Security.Sasl;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// HTTP client for Confluent Schema Registry.
/// </summary>
public sealed class SchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
{
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    private readonly HttpClient _httpClient;
    private readonly SchemaRegistryConfig _config;
    private readonly ConcurrentDictionary<int, Schema> _schemaByIdCache = new();
    private readonly ConcurrentDictionary<(string Subject, Schema Schema), int> _idBySchemaCache = new();
    private readonly object _cacheLock = new();
    private readonly int _maxCachedSchemas;
    private bool _disposed;

    public SchemaRegistryClient(SchemaRegistryConfig config)
        : this(config, CreateConfiguredHttpHandler(config))
    {
    }

    internal SchemaRegistryClient(
        SchemaRegistryConfig config,
        HttpMessageHandler handler,
        Func<OAuthBearerConfig, Func<CancellationToken, ValueTask<OAuthBearerToken>>>? oauthBearerTokenProviderFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _maxCachedSchemas = Math.Max(0, config.MaxCachedSchemas);

        var authHandler = new SchemaRegistryAuthenticationHandler(
            handler,
            config,
            oauthBearerTokenProviderFactory);

        _httpClient = new HttpClient(authHandler, disposeHandler: true)
        {
            BaseAddress = new Uri(config.Url.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMilliseconds(config.RequestTimeoutMs)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.schemaregistry.v1+json"));
    }

    internal int CachedSchemaByIdCount => _schemaByIdCache.Count;
    internal int CachedSchemaIdCount => _idBySchemaCache.Count;

    internal static SocketsHttpHandler CreateHttpHandler(X509Certificate2? clientCertificate = null)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = PooledConnectionLifetime
        };

        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = new X509CertificateCollection
            {
                clientCertificate
            };
        }

        return handler;
    }

    private static SocketsHttpHandler CreateConfiguredHttpHandler(SchemaRegistryConfig? config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return CreateHttpHandler(config.ClientCertificate);
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

        using var response = await _httpClient.PostAsJsonAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions",
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<RegisterSchemaResponse>(
            SchemaRegistryJsonContext.Default.RegisterSchemaResponse, cancellationToken).ConfigureAwait(false);

        var id = result!.Id;

        CacheSchema(id, subject, schema);

        return id;
    }

    public async Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_schemaByIdCache.TryGetValue(id, out var cached))
            return cached;

        using var response = await _httpClient.GetAsync(
            $"schemas/ids/{id}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSchemaResponse>(
            SchemaRegistryJsonContext.Default.GetSchemaResponse, cancellationToken).ConfigureAwait(false);

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

        CacheSchema(id, subject: null, schema);
        return schema;
    }

    public bool TryGetCachedSchema(int id, out Schema schema)
        => _schemaByIdCache.TryGetValue(id, out schema!);

    public async Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse, cancellationToken).ConfigureAwait(false);

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

        CacheSchema(result.Id, subject: null, schema);

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

        using var response = await _httpClient.PostAsJsonAsync(
            $"subjects/{Uri.EscapeDataString(subject)}",
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Schema doesn't exist, register it
            return await RegisterSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false);
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse, cancellationToken).ConfigureAwait(false);

        var id = result!.Id;

        CacheSchema(id, subject, schema);

        return id;
    }

    internal void CacheSchema(int id, string? subject, Schema schema)
    {
        if (_maxCachedSchemas == 0)
            return;

        lock (_cacheLock)
        {
            if (_schemaByIdCache.Count >= _maxCachedSchemas || _idBySchemaCache.Count >= _maxCachedSchemas)
            {
                _schemaByIdCache.Clear();
                _idBySchemaCache.Clear();
            }

            _schemaByIdCache.TryAdd(id, schema);
            if (subject is not null)
            {
                _idBySchemaCache.TryAdd((subject, schema), id);
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("subjects", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<string>>(
            SchemaRegistryJsonContext.Default.ListString, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            SchemaRegistryJsonContext.Default.ListInt32, cancellationToken).ConfigureAwait(false) ?? [];
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

        using var response = await _httpClient.PostAsJsonAsync(
            $"compatibility/subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return true; // No existing schema, so compatible

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<CompatibilityResponse>(
            SchemaRegistryJsonContext.Default.CompatibilityResponse, cancellationToken).ConfigureAwait(false);

        return result?.IsCompatible ?? true;
    }

    public async Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
    {
        var url = $"subjects/{Uri.EscapeDataString(subject)}";
        if (permanent)
            url += "?permanent=true";

        using var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            SchemaRegistryJsonContext.Default.ListInt32, cancellationToken).ConfigureAwait(false) ?? [];
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
                SchemaRegistryJsonContext.Default.ErrorResponse, cancellationToken).ConfigureAwait(false);
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
    /// Static bearer token for Schema Registry requests. Takes precedence over
    /// <see cref="BasicAuthUserInfo"/> and <see cref="OAuthBearerConfig"/>.
    /// </summary>
    public string? BearerAuthToken { get; init; }

    /// <summary>
    /// OAuth 2.0 / OIDC client-credentials configuration used to fetch Schema
    /// Registry bearer tokens.
    /// </summary>
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }

    /// <summary>
    /// Custom bearer token provider for Schema Registry requests. Takes
    /// precedence over static tokens and <see cref="OAuthBearerConfig"/>.
    /// </summary>
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }

    /// <summary>
    /// Client certificate presented during TLS handshake for mutual TLS.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; init; }

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
