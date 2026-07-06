using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization.Metadata;
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
    private readonly ConcurrentDictionary<(string Subject, Schema Schema, bool Normalize), int> _idBySchemaCache = new();
    private readonly object _cacheLock = new();
    private readonly int _maxCachedSchemas;
    private readonly Uri[] _baseUris;
    private int _activeBaseUriIndex;
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
        _baseUris = ResolveBaseUris(config);

        var authHandler = new SchemaRegistryAuthenticationHandler(
            handler,
            config,
            oauthBearerTokenProviderFactory);

        _httpClient = new HttpClient(authHandler, disposeHandler: true)
        {
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

    private static Uri[] ResolveBaseUris(SchemaRegistryConfig config)
    {
        var urls = config.Urls is { Count: > 0 }
            ? config.Urls
            : config.Url.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var baseUris = urls
            .Select(static url => new Uri(url.TrimEnd('/') + "/", UriKind.Absolute))
            .ToArray();

        if (baseUris.Length == 0)
            throw new ArgumentException("At least one Schema Registry URL is required.", nameof(config));

        return baseUris;
    }

    private static string WithNormalizeQuery(string path, bool normalize)
    {
        if (!normalize)
            return path;

        return path.Contains('?', StringComparison.Ordinal)
            ? path + "&normalize=true"
            : path + "?normalize=true";
    }

    private Task<HttpResponseMessage> GetWithFailoverAsync(string path, CancellationToken cancellationToken) =>
        SendWithFailoverAsync(baseUri => _httpClient.GetAsync(new Uri(baseUri, path), cancellationToken), cancellationToken);

    private Task<HttpResponseMessage> DeleteWithFailoverAsync(string path, CancellationToken cancellationToken) =>
        SendWithFailoverAsync(baseUri => _httpClient.DeleteAsync(new Uri(baseUri, path), cancellationToken), cancellationToken);

    private Task<HttpResponseMessage> PostAsJsonWithFailoverAsync<T>(
        string path,
        T value,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken) =>
        SendWithFailoverAsync(
            baseUri => _httpClient.PostAsJsonAsync(new Uri(baseUri, path), value, jsonTypeInfo, cancellationToken),
            cancellationToken);

    private async Task<HttpResponseMessage> SendWithFailoverAsync(
        Func<Uri, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        var startIndex = Volatile.Read(ref _activeBaseUriIndex);
        Exception? lastException = null;

        for (var attempt = 0; attempt < _baseUris.Length; attempt++)
        {
            var index = (startIndex + attempt) % _baseUris.Length;
            try
            {
                var response = await sendAsync(_baseUris[index]).ConfigureAwait(false);
                if (!IsRetriableStatus(response.StatusCode))
                {
                    Volatile.Write(ref _activeBaseUriIndex, index);
                    return response;
                }

                if (attempt == _baseUris.Length - 1)
                    return response;

                response.Dispose();
            }
            catch (Exception ex) when (IsRetriableException(ex, cancellationToken))
            {
                lastException = ex;
            }
        }

        if (lastException is not null)
            throw lastException;

        throw new SchemaRegistryException(0, "Schema Registry request failed before receiving a response.");
    }

    private static bool IsRetriableStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or (HttpStatusCode)429 ||
        (int)statusCode >= 500;

    private static bool IsRetriableException(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException ||
        (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    public Task<int> RegisterSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken = default) =>
        RegisterSchemaAsync(subject, schema, normalize: false, cancellationToken);

    public async Task<int> RegisterSchemaAsync(
        string subject,
        Schema schema,
        bool normalize,
        CancellationToken cancellationToken = default)
    {
        var effectiveNormalize = normalize || _config.NormalizeSchemas;
        var cacheKey = (subject, schema, effectiveNormalize);
        if (_idBySchemaCache.TryGetValue(cacheKey, out var cachedId))
            return cachedId;

        var request = CreateRegisterSchemaRequest(schema);

        using var response = await PostAsJsonWithFailoverAsync(
            WithNormalizeQuery($"subjects/{Uri.EscapeDataString(subject)}/versions", effectiveNormalize),
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<RegisterSchemaResponse>(
            SchemaRegistryJsonContext.Default.RegisterSchemaResponse, cancellationToken).ConfigureAwait(false);

        var id = result!.Id;

        CacheSchema(id, subject, schema, effectiveNormalize);

        return id;
    }

    public async Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_schemaByIdCache.TryGetValue(id, out var cached))
            return cached;

        using var response = await GetWithFailoverAsync(
            $"schemas/ids/{id}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSchemaResponse>(
            SchemaRegistryJsonContext.Default.GetSchemaResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var schema = CreateSchema(result);

        CacheSchema(id, subject: null, schema);
        return schema;
    }

    public bool TryGetCachedSchema(int id, out Schema schema)
        => _schemaByIdCache.TryGetValue(id, out schema!);

    public async Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var schema = CreateSchema(result);

        CacheSchema(result.Id, subject: null, schema);

        return new RegisteredSchema
        {
            Id = result.Id,
            Subject = result.Subject,
            Version = result.Version,
            Schema = schema
        };
    }

    public Task<int> GetOrRegisterSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken = default) =>
        GetOrRegisterSchemaAsync(subject, schema, normalize: false, cancellationToken);

    public async Task<int> GetOrRegisterSchemaAsync(
        string subject,
        Schema schema,
        bool normalize,
        CancellationToken cancellationToken = default)
    {
        var effectiveNormalize = normalize || _config.NormalizeSchemas;
        var cacheKey = (subject, schema, effectiveNormalize);
        if (_idBySchemaCache.TryGetValue(cacheKey, out var cachedId))
            return cachedId;

        // Try to get existing schema first
        var request = CreateRegisterSchemaRequest(schema);

        using var response = await PostAsJsonWithFailoverAsync(
            WithNormalizeQuery($"subjects/{Uri.EscapeDataString(subject)}", effectiveNormalize),
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Schema doesn't exist, register it
            return await RegisterSchemaAsync(
                subject,
                schema,
                effectiveNormalize,
                cancellationToken).ConfigureAwait(false);
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var id = result.Id;

        CacheSchema(id, subject, schema, effectiveNormalize);

        return id;
    }

    internal void CacheSchema(int id, string? subject, Schema schema, bool normalize = false)
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
                _idBySchemaCache.TryAdd((subject, schema, normalize), id);
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync("subjects", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<string>>(
            SchemaRegistryJsonContext.Default.ListString, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            $"subjects/{Uri.EscapeDataString(subject)}/versions",
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            SchemaRegistryJsonContext.Default.ListInt32, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public Task<bool> IsCompatibleAsync(
        string subject,
        Schema schema,
        string version = "latest",
        CancellationToken cancellationToken = default) =>
        IsCompatibleAsync(subject, schema, version, normalize: false, cancellationToken);

    public async Task<bool> IsCompatibleAsync(
        string subject,
        Schema schema,
        string version,
        bool normalize,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRegisterSchemaRequest(schema);

        using var response = await PostAsJsonWithFailoverAsync(
            WithNormalizeQuery(
                $"compatibility/subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
                normalize || _config.NormalizeSchemas),
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

        using var response = await DeleteWithFailoverAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            SchemaRegistryJsonContext.Default.ListInt32, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static RegisterSchemaRequest CreateRegisterSchemaRequest(Schema schema) => new()
    {
        Schema = schema.SchemaString,
        SchemaType = schema.SchemaType == SchemaType.Avro ? null : schema.SchemaType.ToString().ToUpperInvariant(),
        References = schema.References?.Select(ToReferenceDto).ToList(),
        Metadata = ToMetadataDto(schema.Metadata),
        RuleSet = ToRuleSetDto(schema.RuleSet)
    };

    private static Schema CreateSchema(GetSchemaResponse response) => new()
    {
        SchemaString = response.Schema,
        SchemaType = ParseSchemaType(response.SchemaType),
        References = response.References?.Select(ToReference).ToList(),
        Metadata = ToMetadata(response.Metadata),
        RuleSet = ToRuleSet(response.RuleSet)
    };

    private static Schema CreateSchema(GetSubjectVersionResponse response) => new()
    {
        SchemaString = response.Schema,
        SchemaType = ParseSchemaType(response.SchemaType),
        References = response.References?.Select(ToReference).ToList(),
        Metadata = ToMetadata(response.Metadata),
        RuleSet = ToRuleSet(response.RuleSet)
    };

    private static SchemaReferenceDto ToReferenceDto(SchemaReference reference) => new()
    {
        Name = reference.Name,
        Subject = reference.Subject,
        Version = reference.Version
    };

    private static SchemaReference ToReference(SchemaReferenceDto reference) => new()
    {
        Name = reference.Name,
        Subject = reference.Subject,
        Version = reference.Version
    };

    private static SchemaMetadataDto? ToMetadataDto(SchemaMetadata? metadata)
    {
        if (metadata is null)
            return null;

        return new SchemaMetadataDto
        {
            Tags = metadata.Tags?.ToDictionary(
                static kvp => kvp.Key,
                static kvp => kvp.Value.ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal),
            Properties = metadata.Properties?.ToDictionary(
                static kvp => kvp.Key,
                static kvp => kvp.Value,
                StringComparer.Ordinal),
            Sensitive = metadata.Sensitive?.ToHashSet(StringComparer.Ordinal)
        };
    }

    private static SchemaMetadata? ToMetadata(SchemaMetadataDto? metadata)
    {
        if (metadata is null)
            return null;

        return new SchemaMetadata
        {
            Tags = metadata.Tags?.ToDictionary(
                static kvp => kvp.Key,
                static kvp => (IReadOnlySet<string>)kvp.Value,
                StringComparer.Ordinal),
            Properties = metadata.Properties,
            Sensitive = metadata.Sensitive
        };
    }

    private static SchemaRuleSetDto? ToRuleSetDto(SchemaRuleSet? ruleSet)
    {
        if (ruleSet is null)
            return null;

        return new SchemaRuleSetDto
        {
            MigrationRules = ruleSet.MigrationRules?.Select(ToRuleDto).ToList(),
            DomainRules = ruleSet.DomainRules?.Select(ToRuleDto).ToList(),
            EncodingRules = ruleSet.EncodingRules?.Select(ToRuleDto).ToList(),
            EnableAt = ruleSet.EnableAt
        };
    }

    private static SchemaRuleSet? ToRuleSet(SchemaRuleSetDto? ruleSet)
    {
        if (ruleSet is null)
            return null;

        return new SchemaRuleSet
        {
            MigrationRules = ruleSet.MigrationRules?.Select(ToRule).ToList(),
            DomainRules = ruleSet.DomainRules?.Select(ToRule).ToList(),
            EncodingRules = ruleSet.EncodingRules?.Select(ToRule).ToList(),
            EnableAt = ruleSet.EnableAt
        };
    }

    private static SchemaRuleDto ToRuleDto(SchemaRule rule) => new()
    {
        Name = rule.Name,
        Doc = rule.Doc,
        Kind = FormatRuleKind(rule.Kind),
        Mode = FormatRuleMode(rule.Mode),
        Type = rule.Type,
        Tags = rule.Tags?.ToHashSet(StringComparer.Ordinal),
        Params = rule.Parameters?.ToDictionary(
            static kvp => kvp.Key,
            static kvp => kvp.Value,
            StringComparer.Ordinal),
        Expr = rule.Expr,
        OnSuccess = rule.OnSuccess,
        OnFailure = rule.OnFailure,
        Disabled = rule.Disabled
    };

    private static SchemaRule ToRule(SchemaRuleDto rule) => new()
    {
        Name = rule.Name ?? string.Empty,
        Doc = rule.Doc,
        Kind = ParseRuleKind(rule.Kind),
        Mode = ParseRuleMode(rule.Mode),
        Type = rule.Type ?? string.Empty,
        Tags = rule.Tags,
        Parameters = rule.Params,
        Expr = rule.Expr,
        OnSuccess = rule.OnSuccess,
        OnFailure = rule.OnFailure,
        Disabled = rule.Disabled
    };

    private static SchemaType ParseSchemaType(string? schemaType)
    {
        return schemaType?.ToUpperInvariant() switch
        {
            "JSON" => SchemaType.Json,
            "PROTOBUF" => SchemaType.Protobuf,
            _ => SchemaType.Avro
        };
    }

    private static string FormatRuleKind(SchemaRuleKind kind)
        => kind switch
        {
            SchemaRuleKind.Condition => "CONDITION",
            _ => "TRANSFORM"
        };

    private static SchemaRuleKind ParseRuleKind(string? kind)
        => string.Equals(kind, "CONDITION", StringComparison.OrdinalIgnoreCase)
            ? SchemaRuleKind.Condition
            : SchemaRuleKind.Transform;

    private static string FormatRuleMode(SchemaRuleMode mode)
        => mode switch
        {
            SchemaRuleMode.Upgrade => "UPGRADE",
            SchemaRuleMode.Downgrade => "DOWNGRADE",
            SchemaRuleMode.UpDown => "UPDOWN",
            SchemaRuleMode.Read => "READ",
            SchemaRuleMode.Write => "WRITE",
            SchemaRuleMode.WriteRead => "WRITEREAD",
            _ => "WRITE"
        };

    private static SchemaRuleMode ParseRuleMode(string? mode)
        => mode?.ToUpperInvariant() switch
        {
            "UPGRADE" => SchemaRuleMode.Upgrade,
            "DOWNGRADE" => SchemaRuleMode.Downgrade,
            "UPDOWN" => SchemaRuleMode.UpDown,
            "READ" => SchemaRuleMode.Read,
            "WRITEREAD" => SchemaRuleMode.WriteRead,
            _ => SchemaRuleMode.Write
        };

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
    /// Schema Registry URL. Multiple failover URLs may be provided as a
    /// comma-separated list.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Optional Schema Registry failover URLs. When set, this takes precedence
    /// over <see cref="Url"/>.
    /// </summary>
    public IReadOnlyList<string>? Urls { get; init; }

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

    /// <summary>
    /// Whether schema registration, lookup, and compatibility requests should
    /// include normalize=true.
    /// </summary>
    public bool NormalizeSchemas { get; init; }
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
