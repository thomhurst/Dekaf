using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dekaf.Security.Sasl;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// HTTP client for Confluent Schema Registry.
/// </summary>
public sealed class SchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
{
    private const string AcceptUnknownPropertiesHeader = "Confluent-Accept-Unknown-Properties";
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    private readonly HttpClient _httpClient;
    private readonly SchemaRegistryConfig _config;
    private readonly ConcurrentDictionary<int, Schema> _schemaByIdCache = new();
    private readonly ConcurrentDictionary<(Guid Guid, string? Format), Schema> _schemaByGuidCache = new();
    private readonly ConcurrentDictionary<(int Id, string Subject, string? Format), Schema> _schemaBySubjectAndIdCache = new();
    private readonly ConcurrentDictionary<(string Subject, Schema Schema, bool Normalize), int> _idBySchemaCache = new();
    private readonly object _cacheLock = new();
    private readonly int _maxCachedSchemas;
    private readonly Uri[] _baseUris;
    private int _activeBaseUriIndex;
    private bool _disposed;

    public SchemaRegistryClient(SchemaRegistryConfig config)
        : this(config, CreateConfiguredHttpHandler(config), oauthBearerTokenProviderFactory: null)
    {
    }

    /// <summary>
    /// Creates a Schema Registry client over a caller-owned <see cref="HttpMessageHandler"/>.
    /// Disposing this instance does not dispose <paramref name="handler"/>.
    /// </summary>
    public SchemaRegistryClient(SchemaRegistryConfig config, HttpMessageHandler handler)
        : this(config, CreateCallerOwnedHttpMessageHandler(config, handler), oauthBearerTokenProviderFactory: null)
    {
    }

    /// <summary>
    /// Creates a Schema Registry client using a handler produced by <paramref name="handlerFactory"/>.
    /// The produced handler is owned and disposed by this instance.
    /// </summary>
    public SchemaRegistryClient(SchemaRegistryConfig config, Func<HttpMessageHandler> handlerFactory)
        : this(config, CreateOwnedFactoryHttpMessageHandler(config, handlerFactory), oauthBearerTokenProviderFactory: null)
    {
    }

    internal SchemaRegistryClient(
        SchemaRegistryConfig config,
        HttpMessageHandler handler,
        Func<OAuthBearerConfig, Func<CancellationToken, ValueTask<OAuthBearerToken>>>? oauthBearerTokenProviderFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handler);

        HttpClient? httpClient = null;
        SchemaRegistryAuthenticationHandler? authHandler = null;
        try
        {
            ValidateConfig(config);
            _config = config;
            _maxCachedSchemas = Math.Max(0, config.MaxCachedSchemas);
            _baseUris = ResolveBaseUris(config);

            authHandler = new SchemaRegistryAuthenticationHandler(
                handler,
                config,
                oauthBearerTokenProviderFactory);

            httpClient = new HttpClient(authHandler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromMilliseconds(config.RequestTimeoutMs)
            };

            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.schemaregistry.v1+json"));
            ConfigureDefaultHeaders(httpClient.DefaultRequestHeaders, config);
            _httpClient = httpClient;
        }
        catch
        {
            if (httpClient is not null)
                httpClient.Dispose();
            else if (authHandler is not null)
                authHandler.Dispose();
            else
                handler.Dispose();
            throw;
        }

    }

    internal int CachedSchemaByIdCount => _schemaByIdCache.Count;
    internal int CachedSchemaByGuidCount => _schemaByGuidCache.Count;
    internal int CachedSchemaBySubjectAndIdCount => _schemaBySubjectAndIdCache.Count;
    internal int CachedSchemaIdCount => _idBySchemaCache.Count;

    /// <inheritdoc />
    public int LatestCacheTtlSecs => _config.LatestCacheTtlSecs;

    internal static HttpMessageHandler CreateConfiguredHttpHandler(SchemaRegistryConfig? config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfig(config);

        var tlsConfig = ResolveTlsConfig(config);
        SchemaRegistryCertificateMaterial? certificateMaterial = null;
        SocketsHttpHandler? handler = null;
        try
        {
            certificateMaterial = tlsConfig is null
                ? null
                : SchemaRegistryCertificateMaterial.Load(tlsConfig);
            handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = PooledConnectionLifetime,
                UseProxy = config.UseProxy,
                Proxy = config.Proxy
            };

            if (tlsConfig is not null)
                ConfigureTls(handler, tlsConfig, certificateMaterial!);

            if (certificateMaterial is null)
                return handler;

            return new CertificateOwningHttpMessageHandler(handler, certificateMaterial);
        }
        catch
        {
            handler?.Dispose();
            certificateMaterial?.Dispose();
            throw;
        }
    }

    private static HttpMessageHandler CreateCallerOwnedHttpMessageHandler(
        SchemaRegistryConfig? config,
        HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handler);
        ValidateConfig(config);
        ValidateCustomPipelineConfig(config);
        return new NonDisposingHttpMessageHandler(handler);
    }

    private static HttpMessageHandler CreateOwnedFactoryHttpMessageHandler(
        SchemaRegistryConfig? config,
        Func<HttpMessageHandler>? handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(handlerFactory);
        ValidateConfig(config);
        ValidateCustomPipelineConfig(config);
        return handlerFactory() ?? throw new InvalidOperationException("The HTTP message handler factory returned null.");
    }

    private static void ValidateConfig(SchemaRegistryConfig config)
    {
        if (config.RequestTimeoutMs <= 0 && config.RequestTimeoutMs != Timeout.Infinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "RequestTimeoutMs must be positive or -1 for an infinite timeout.");
        }

        if (config.LatestCacheTtlSecs < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "LatestCacheTtlSecs must be non-negative or -1 for no expiry.");
        }

        if (!config.UseProxy && config.Proxy is not null)
            throw new ArgumentException("Proxy cannot be supplied when UseProxy is false.", nameof(config));

        if (config.Tls is not null && config.ClientCertificate is not null)
        {
            throw new ArgumentException(
                "ClientCertificate cannot be combined with Tls. Configure the certificate through Tls instead.",
                nameof(config));
        }
    }

    private static void ValidateCustomPipelineConfig(SchemaRegistryConfig config)
    {
        if (config.Tls is not null ||
            config.ClientCertificate is not null ||
            config.Proxy is not null ||
            !config.UseProxy)
        {
            throw new ArgumentException(
                "TLS and proxy settings cannot be combined with a caller-supplied HTTP pipeline.",
                nameof(config));
        }
    }

    private static SchemaRegistryTlsConfig? ResolveTlsConfig(SchemaRegistryConfig config) =>
        config.Tls ?? (config.ClientCertificate is null
            ? null
            : new SchemaRegistryTlsConfig { ClientCertificate = config.ClientCertificate });

    private static void ConfigureTls(
        SocketsHttpHandler handler,
        SchemaRegistryTlsConfig config,
        SchemaRegistryCertificateMaterial material)
    {
        var sslOptions = new SslClientAuthenticationOptions
        {
            CertificateRevocationCheckMode = config.CheckCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck
        };

        if (config.EnabledSslProtocols is not null)
            sslOptions.EnabledSslProtocols = config.EnabledSslProtocols.Value;

        if (material.ClientCertificate is not null)
        {
            if (material.ClientIntermediateCertificates is { Count: > 0 } intermediateCertificates)
            {
                sslOptions.ClientCertificateContext = SslStreamCertificateContext.Create(
                    material.ClientCertificate,
                    intermediateCertificates,
                    offline: true);
            }
            else
            {
                sslOptions.ClientCertificates = [material.ClientCertificate];
            }
        }

        if (config.RemoteCertificateValidationCallback is not null)
        {
            sslOptions.RemoteCertificateValidationCallback = config.RemoteCertificateValidationCallback;
        }
        else if (!config.ValidateServerCertificate)
        {
#pragma warning disable CA5359 // Explicit opt-out requested by the caller.
            sslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore CA5359
        }
        else if (material.CaCertificates is not null || !config.ValidateServerCertificateHostName)
        {
            sslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            {
                if (material.CaCertificates is null)
                {
                    return (errors & ~SslPolicyErrors.RemoteCertificateNameMismatch) ==
                           SslPolicyErrors.None;
                }

                return SchemaRegistryCertificateValidator.Validate(
                    certificate,
                    chain,
                    errors,
                    material.CaCertificates,
                    config.ValidateServerCertificateHostName,
                    config.CheckCertificateRevocation);
            };
        }

        handler.SslOptions = sslOptions;
    }

    private static void ConfigureDefaultHeaders(HttpRequestHeaders headers, SchemaRegistryConfig config)
    {
        var userAgent = config.UserAgent ?? GetDefaultUserAgent();
        try
        {
            headers.UserAgent.ParseAdd(userAgent);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("UserAgent is not a valid HTTP User-Agent value.", nameof(config), ex);
        }

        headers.Add(AcceptUnknownPropertiesHeader, "true");

        if (config.DefaultHeaders is null)
            return;

        foreach (var (name, value) in config.DefaultHeaders)
        {
            if (string.Equals(name, "User-Agent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Accept", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, AcceptUnknownPropertiesHeader, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"The {name} header is managed by SchemaRegistryClient and cannot be overridden.",
                    nameof(config));
            }

            try
            {
                headers.Add(name, value);
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                throw new ArgumentException(
                    $"Default header '{name}' is invalid or is a content header.",
                    nameof(config),
                    ex);
            }
        }
    }

    private static string GetDefaultUserAgent()
    {
        var version = typeof(SchemaRegistryClient).Assembly.GetName().Version;
        return $"Dekaf.SchemaRegistry/{version?.ToString(3) ?? "unknown"}";
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

    private static string WithQuery(string path, params (string Name, string? Value)[] parameters)
    {
        StringBuilder? builder = null;
        foreach (var (name, value) in parameters)
            AppendQueryParameter(ref builder, path, name, value);

        return builder?.ToString() ?? path;
    }

    private static string WithAssociationQuery(
        string path,
        string? resourceType,
        IReadOnlyList<string>? associationTypes,
        string? lifecycle,
        int? offset,
        int? limit,
        bool? cascadeLifecycle)
    {
        StringBuilder? builder = null;
        AppendQueryParameter(ref builder, path, "resourceType", resourceType);

        if (associationTypes is not null)
        {
            for (var index = 0; index < associationTypes.Count; index++)
                AppendQueryParameter(ref builder, path, "associationType", associationTypes[index]);
        }

        AppendQueryParameter(ref builder, path, "lifecycle", lifecycle);
        AppendQueryParameter(ref builder, path, "offset", IntQuery(offset));
        AppendQueryParameter(ref builder, path, "limit", IntQuery(limit));
        AppendQueryParameter(
            ref builder,
            path,
            "cascadeLifecycle",
            cascadeLifecycle.HasValue ? cascadeLifecycle.Value ? "true" : "false" : null);
        return builder?.ToString() ?? path;
    }

    private static void AppendQueryParameter(
        ref StringBuilder? builder,
        string path,
        string name,
        string? value)
    {
        if (value is null)
            return;

        builder ??= new StringBuilder(path);
        builder.Append(builder.Length == path.Length ? '?' : '&');
        builder.Append(Uri.EscapeDataString(name));
        builder.Append('=');
        builder.Append(Uri.EscapeDataString(value));
    }

    private static string? BoolQuery(bool value) => value ? "true" : null;

    private static string? IntQuery(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;

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

    private Task<HttpResponseMessage> PutAsJsonWithFailoverAsync<T>(
        string path,
        T value,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken) =>
        SendWithFailoverAsync(
            baseUri => _httpClient.PutAsJsonAsync(new Uri(baseUri, path), value, jsonTypeInfo, cancellationToken),
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
        var schemaGuid = ParseSchemaGuid(result.Guid);

        CacheSchema(
            id,
            subject,
            schema,
            effectiveNormalize,
            schemaGuid: effectiveNormalize ? null : schemaGuid);

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

    public async Task<Schema> GetSchemaByGuidAsync(
        string guid,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guid);
        if (!Guid.TryParse(guid, out var parsedGuid))
            throw new ArgumentException("The schema GUID is not valid.", nameof(guid));

        format = NormalizeFormat(format);
        var cacheKey = (parsedGuid, format);
        if (_schemaByGuidCache.TryGetValue(cacheKey, out var cached))
            return cached;

        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"schemas/guids/{parsedGuid:D}",
                ("format", format)),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSchemaResponse>(
            SchemaRegistryJsonContext.Default.GetSchemaResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var schema = CreateSchema(result);
        CacheGuidSchema(parsedGuid, format, schema);
        return schema;
    }

    public bool TryGetCachedSchema(int id, out Schema schema)
        => _schemaByIdCache.TryGetValue(id, out schema!);

    public bool TryGetCachedSchema(Guid guid, string? format, out Schema schema)
        => _schemaByGuidCache.TryGetValue((guid, NormalizeFormat(format)), out schema!);

    public Task<Schema> GetSchemaAsync(
        int id,
        string subject,
        CancellationToken cancellationToken = default)
        => GetSchemaAsync(id, subject, format: null, cancellationToken);

    internal async Task<Schema> GetSchemaAsync(
        int id,
        string subject,
        string? format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        format = NormalizeFormat(format);
        var key = (id, subject, format);
        if (_schemaBySubjectAndIdCache.TryGetValue(key, out var cached))
            return cached;

        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"schemas/ids/{id.ToString(CultureInfo.InvariantCulture)}",
                ("subject", subject),
                ("format", format)),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSchemaResponse>(
            SchemaRegistryJsonContext.Default.GetSchemaResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var schema = CreateSchema(result);
        CacheSubjectSchema(id, subject, format, schema);
        return schema;
    }

    public bool TryGetCachedSchema(int id, string subject, out Schema schema)
        => _schemaBySubjectAndIdCache.TryGetValue((id, subject, null), out schema!);

    public Task<RegisteredSchema> GetSchemaBySubjectAsync(
        string subject,
        string version = "latest",
        CancellationToken cancellationToken = default) =>
        GetSchemaBySubjectAsync(subject, version, ignoreDeletedSchemas: true, cancellationToken);

    public Task<RegisteredSchema> GetSchemaBySubjectAsync(
        string subject,
        string version,
        bool ignoreDeletedSchemas,
        CancellationToken cancellationToken = default)
        => GetSchemaBySubjectAsync(
            subject,
            version,
            ignoreDeletedSchemas,
            format: null,
            cancellationToken);

    internal async Task<RegisteredSchema> GetSchemaBySubjectAsync(
        string subject,
        string version,
        bool ignoreDeletedSchemas,
        string? format,
        CancellationToken cancellationToken = default)
    {
        format = NormalizeFormat(format);
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"subjects/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}",
                ("deleted", ignoreDeletedSchemas ? null : "true"),
                ("format", format)),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var schema = CreateSchema(result);
        var schemaGuid = ParseSchemaGuid(result.Guid);

        if (format is null)
        {
            CacheSchema(result.Id, subject: null, schema, schemaGuid: schemaGuid);
        }
        else
        {
            CacheSubjectSchema(result.Id, subject, format, schema);
            if (schemaGuid is { } guid)
                CacheGuidSchema(guid, format, schema);
        }

        return new RegisteredSchema
        {
            Id = result.Id,
            Guid = schemaGuid?.ToString("D"),
            Subject = result.Subject,
            Version = result.Version,
            Schema = schema
        };
    }

    public async Task<RegisteredSchema> LookupSchemaAsync(
        string subject,
        Schema schema,
        bool ignoreDeletedSchemas = true,
        bool normalize = false,
        CancellationToken cancellationToken = default)
    {
        var effectiveNormalize = normalize || _config.NormalizeSchemas;
        var request = CreateRegisterSchemaRequest(schema);
        var path = WithQuery(
            $"subjects/{Uri.EscapeDataString(subject)}",
            ("normalize", effectiveNormalize ? "true" : "false"),
            ("deleted", ignoreDeletedSchemas ? "false" : "true"));

        using var response = await PostAsJsonWithFailoverAsync(
            path,
            request,
            SchemaRegistryJsonContext.Default.RegisterSchemaRequest,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetSubjectVersionResponse>(
            SchemaRegistryJsonContext.Default.GetSubjectVersionResponse,
            cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty schema response");

        var registeredSchema = CreateSchema(result);
        var schemaGuid = ParseSchemaGuid(result.Guid);
        CacheSchema(
            result.Id,
            ignoreDeletedSchemas ? subject : null,
            schema,
            effectiveNormalize,
            schemaById: registeredSchema,
            schemaGuid: schemaGuid);

        return new RegisteredSchema
        {
            Id = result.Id,
            Guid = schemaGuid?.ToString("D"),
            Subject = result.Subject,
            Version = result.Version,
            Schema = registeredSchema
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

        var registeredSchema = CreateSchema(result);
        var schemaGuid = ParseSchemaGuid(result.Guid);
        CacheSchema(
            result.Id,
            subject,
            schema,
            effectiveNormalize,
            schemaById: registeredSchema,
            schemaGuid: schemaGuid);

        return result.Id;
    }

    internal void CacheSchema(
        int id,
        string? subject,
        Schema schema,
        bool normalize = false,
        Schema? schemaById = null,
        Guid? schemaGuid = null)
    {
        if (_maxCachedSchemas == 0)
            return;

        lock (_cacheLock)
        {
            ClearCachesIfFull();

            if (schemaById is not null)
                _schemaByIdCache[id] = schemaById;
            else
                _schemaByIdCache.TryAdd(id, schema);
            if (subject is not null)
            {
                _idBySchemaCache.TryAdd((subject, schema, normalize), id);
            }
            if (schemaGuid is { } guid)
                _schemaByGuidCache.TryAdd((guid, null), schemaById ?? schema);
        }
    }

    private void CacheSubjectSchema(int id, string subject, string? format, Schema schema)
    {
        if (_maxCachedSchemas == 0)
            return;

        lock (_cacheLock)
        {
            ClearCachesIfFull();

            _schemaBySubjectAndIdCache[(id, subject, NormalizeFormat(format))] = schema;
        }
    }

    internal void CacheGuidSchema(Guid guid, string? format, Schema schema)
    {
        if (_maxCachedSchemas == 0)
            return;

        var cacheKey = (guid, NormalizeFormat(format));
        lock (_cacheLock)
        {
            if (_schemaByGuidCache.ContainsKey(cacheKey))
                return;

            ClearCachesIfFull();

            _schemaByGuidCache.TryAdd(cacheKey, schema);
        }
    }

    private static string? NormalizeFormat(string? format) =>
        string.IsNullOrEmpty(format) ? null : format;

    private void ClearCachesIfFull()
    {
        if (_schemaByIdCache.Count < _maxCachedSchemas &&
            _schemaBySubjectAndIdCache.Count < _maxCachedSchemas &&
            _idBySchemaCache.Count < _maxCachedSchemas &&
            _schemaByGuidCache.Count < _maxCachedSchemas)
            return;

        _schemaByIdCache.Clear();
        _schemaBySubjectAndIdCache.Clear();
        _idBySchemaCache.Clear();
        _schemaByGuidCache.Clear();
    }

    private static Guid? ParseSchemaGuid(string? guid)
    {
        if (guid is null)
            return null;

        if (Guid.TryParse(guid, out var parsedGuid))
            return parsedGuid;

        throw new SchemaRegistryException(0, "Schema Registry returned an invalid schema GUID.");
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

    public async Task<SchemaCompatibilityLevel> GetCompatibilityAsync(
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var path = GetCompatibilityPath(subject);
        using var response = await GetWithFailoverAsync(path, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<GetCompatibilityResponse>(
            SchemaRegistryJsonContext.Default.GetCompatibilityResponse,
            cancellationToken).ConfigureAwait(false);
        return ParseCompatibilityLevel(result?.CompatibilityLevel);
    }

    public async Task<SchemaCompatibilityLevel> UpdateCompatibilityAsync(
        SchemaCompatibilityLevel level,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var path = GetCompatibilityPath(subject);
        var request = new UpdateCompatibilityRequest
        {
            Compatibility = GetCompatibilityWireValue(level)
        };
        using var response = await PutAsJsonWithFailoverAsync(
            path,
            request,
            SchemaRegistryJsonContext.Default.UpdateCompatibilityRequest,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<UpdateCompatibilityResponse>(
            SchemaRegistryJsonContext.Default.UpdateCompatibilityResponse,
            cancellationToken).ConfigureAwait(false);
        return ParseCompatibilityLevel(result?.Compatibility);
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

    public async Task<IReadOnlyList<Association>> GetAssociationsByResourceNameAsync(
        string resourceName,
        string resourceNamespace = "-",
        string? resourceType = null,
        IReadOnlyList<string>? associationTypes = null,
        string? lifecycle = null,
        int offset = 0,
        int limit = -1,
        CancellationToken cancellationToken = default)
    {
        AssociationValidation.ValidateGet(
            resourceName,
            resourceNamespace,
            resourceType,
            associationTypes,
            lifecycle,
            offset,
            limit);

        var path = WithAssociationQuery(
            $"associations/resources/{Uri.EscapeDataString(resourceNamespace)}/{Uri.EscapeDataString(resourceName)}",
            resourceType,
            associationTypes,
            lifecycle,
            offset == 0 ? null : offset,
            limit == -1 ? null : limit,
            cascadeLifecycle: null);
        using var response = await GetWithFailoverAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<List<AssociationDto>>(
            SchemaRegistryJsonContext.Default.ListAssociationDto,
            cancellationToken).ConfigureAwait(false);
        if (result is null or { Count: 0 })
            return [];

        var associations = new Association[result.Count];
        for (var index = 0; index < result.Count; index++)
            associations[index] = ToAssociation(result[index]);
        return associations;
    }

    public async Task<AssociationResponse> CreateAssociationAsync(
        AssociationCreateOrUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        AssociationValidation.ValidateCreate(request);

        using var response = await PostAsJsonWithFailoverAsync(
            "associations",
            ToAssociationRequestDto(request, _config.NormalizeSchemas),
            SchemaRegistryJsonContext.Default.AssociationCreateOrUpdateRequestDto,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<AssociationResponseDto>(
            SchemaRegistryJsonContext.Default.AssociationResponseDto,
            cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            throw new SchemaRegistryException(
                (int)response.StatusCode,
                "Schema Registry returned an empty association response");
        }

        return ToAssociationResponse(result);
    }

    public async Task DeleteAssociationsAsync(
        string resourceId,
        string? resourceType = null,
        IReadOnlyList<string>? associationTypes = null,
        bool cascadeLifecycle = false,
        CancellationToken cancellationToken = default)
    {
        AssociationValidation.ValidateDelete(resourceId, resourceType, associationTypes);

        var path = WithAssociationQuery(
            $"associations/resources/{Uri.EscapeDataString(resourceId)}",
            resourceType,
            associationTypes,
            lifecycle: null,
            offset: null,
            limit: null,
            cascadeLifecycle);
        using var response = await DeleteWithFailoverAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static string GetCompatibilityPath(string? subject)
    {
        if (subject is null)
            return "config";

        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty or whitespace. Use null for global configuration.", nameof(subject));

        return $"config/{Uri.EscapeDataString(subject)}";
    }

    private static string GetCompatibilityWireValue(SchemaCompatibilityLevel level) => level switch
    {
        SchemaCompatibilityLevel.None => "NONE",
        SchemaCompatibilityLevel.Backward => "BACKWARD",
        SchemaCompatibilityLevel.BackwardTransitive => "BACKWARD_TRANSITIVE",
        SchemaCompatibilityLevel.Forward => "FORWARD",
        SchemaCompatibilityLevel.ForwardTransitive => "FORWARD_TRANSITIVE",
        SchemaCompatibilityLevel.Full => "FULL",
        SchemaCompatibilityLevel.FullTransitive => "FULL_TRANSITIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown compatibility level.")
    };

    private static SchemaCompatibilityLevel ParseCompatibilityLevel(string? compatibility) => compatibility switch
    {
        "NONE" => SchemaCompatibilityLevel.None,
        "BACKWARD" => SchemaCompatibilityLevel.Backward,
        "BACKWARD_TRANSITIVE" => SchemaCompatibilityLevel.BackwardTransitive,
        "FORWARD" => SchemaCompatibilityLevel.Forward,
        "FORWARD_TRANSITIVE" => SchemaCompatibilityLevel.ForwardTransitive,
        "FULL" => SchemaCompatibilityLevel.Full,
        "FULL_TRANSITIVE" => SchemaCompatibilityLevel.FullTransitive,
        _ => throw new SchemaRegistryException(
            0,
            $"Schema Registry returned unknown compatibility level '{compatibility ?? "<null>"}'.")
    };

    public async Task<IReadOnlyList<string>> GetKekNamesAsync(
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                "dek-registry/v1/keks",
                ("deleted", BoolQuery(deleted)),
                ("offset", IntQuery(offset)),
                ("limit", IntQuery(limit))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<string>>(
            SchemaRegistryJsonContext.Default.ListString, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<Kek> RegisterKekAsync(
        RegisterKekRequest request,
        bool testSharing = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await PostAsJsonWithFailoverAsync(
            WithQuery("dek-registry/v1/keks", ("testSharing", BoolQuery(testSharing))),
            ToRegisterKekRequestDto(request),
            SchemaRegistryJsonContext.Default.RegisterKekRequestDto,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<KekDto>(
            SchemaRegistryJsonContext.Default.KekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty KEK response");

        return ToKek(result);
    }

    public async Task<Kek> GetKekAsync(
        string name,
        bool deleted = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(name)}",
                ("deleted", BoolQuery(deleted))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<KekDto>(
            SchemaRegistryJsonContext.Default.KekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty KEK response");

        return ToKek(result);
    }

    public async Task DeleteKekAsync(
        string name,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await DeleteWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(name)}",
                ("permanent", BoolQuery(permanent))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetDekSubjectsAsync(
        string kekName,
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks",
                ("deleted", BoolQuery(deleted)),
                ("offset", IntQuery(offset)),
                ("limit", IntQuery(limit))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<string>>(
            SchemaRegistryJsonContext.Default.ListString, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<Dek> RegisterDekAsync(
        string kekName,
        RegisterDekRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await PostAsJsonWithFailoverAsync(
            $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks",
            ToRegisterDekRequestDto(request),
            SchemaRegistryJsonContext.Default.RegisterDekRequestDto,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<DekDto>(
            SchemaRegistryJsonContext.Default.DekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty DEK response");

        return ToDek(result);
    }

    public async Task<Dek> GetDekAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool deleted = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}",
                ("algorithm", FormatDekAlgorithm(algorithm)),
                ("deleted", BoolQuery(deleted))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<DekDto>(
            SchemaRegistryJsonContext.Default.DekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty DEK response");

        return ToDek(result);
    }

    public async Task<Dek> GetDekAsync(
        string kekName,
        string subject,
        int version,
        bool deleted = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}/versions/{version.ToString(CultureInfo.InvariantCulture)}",
                ("deleted", BoolQuery(deleted))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<DekDto>(
            SchemaRegistryJsonContext.Default.DekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty DEK response");

        return ToDek(result);
    }

    public async Task<Dek> GetDekAsync(
        string kekName,
        string subject,
        int version,
        DekAlgorithm algorithm,
        bool deleted = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}/versions/{version.ToString(CultureInfo.InvariantCulture)}",
                ("algorithm", FormatDekAlgorithm(algorithm)),
                ("deleted", BoolQuery(deleted))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<DekDto>(
            SchemaRegistryJsonContext.Default.DekDto, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new SchemaRegistryException((int)response.StatusCode, "Schema Registry returned an empty DEK response");

        return ToDek(result);
    }

    public async Task<IReadOnlyList<int>> GetDekVersionsAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}/versions",
                ("algorithm", FormatDekAlgorithm(algorithm)),
                ("deleted", BoolQuery(deleted)),
                ("offset", IntQuery(offset)),
                ("limit", IntQuery(limit))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<List<int>>(
            SchemaRegistryJsonContext.Default.ListInt32, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task DeleteDekAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await DeleteWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}",
                ("algorithm", FormatDekAlgorithm(algorithm)),
                ("permanent", BoolQuery(permanent))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteDekVersionAsync(
        string kekName,
        string subject,
        int version,
        DekAlgorithm? algorithm = null,
        bool permanent = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await DeleteWithFailoverAsync(
            WithQuery(
                $"dek-registry/v1/keks/{Uri.EscapeDataString(kekName)}/deks/{Uri.EscapeDataString(subject)}/versions/{version.ToString(CultureInfo.InvariantCulture)}",
                ("algorithm", FormatDekAlgorithm(algorithm)),
                ("permanent", BoolQuery(permanent))),
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static RegisterSchemaRequest CreateRegisterSchemaRequest(Schema schema) => new()
    {
        Schema = schema.SchemaString,
        SchemaType = schema.SchemaType == SchemaType.Avro ? null : schema.SchemaType.ToString().ToUpperInvariant(),
        References = schema.References?.Select(ToReferenceDto).ToList(),
        Metadata = ToMetadataDto(schema.Metadata),
        RuleSet = ToRuleSetDto(schema.RuleSet)
    };

    private static AssociationCreateOrUpdateRequestDto ToAssociationRequestDto(
        AssociationCreateOrUpdateRequest request,
        bool normalizeSchemas)
    {
        var associations = new List<AssociationCreateOrUpdateInfoDto>(request.Associations.Count);
        for (var index = 0; index < request.Associations.Count; index++)
        {
            var association = request.Associations[index];
            associations.Add(new AssociationCreateOrUpdateInfoDto
            {
                Subject = association.Subject,
                AssociationType = association.AssociationType,
                Lifecycle = association.Lifecycle,
                Frozen = association.Frozen,
                Schema = association.Schema is null ? null : CreateRegisterSchemaRequest(association.Schema),
                Normalize = association.Schema is null
                    ? association.Normalize
                    : association.Normalize ?? normalizeSchemas
            });
        }

        return new AssociationCreateOrUpdateRequestDto
        {
            ResourceName = request.ResourceName,
            ResourceNamespace = request.ResourceNamespace,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            Associations = associations
        };
    }

    private static Association ToAssociation(AssociationDto association) => new()
    {
        Subject = association.Subject,
        Guid = association.Guid,
        ResourceName = association.ResourceName,
        ResourceNamespace = association.ResourceNamespace,
        ResourceId = association.ResourceId,
        ResourceType = association.ResourceType,
        AssociationType = association.AssociationType,
        Lifecycle = association.Lifecycle,
        Frozen = association.Frozen
    };

    private static AssociationResponse ToAssociationResponse(AssociationResponseDto response)
    {
        var associations = new AssociationInfo[response.Associations.Count];
        for (var index = 0; index < response.Associations.Count; index++)
        {
            var association = response.Associations[index];
            associations[index] = new AssociationInfo
            {
                Subject = association.Subject,
                AssociationType = association.AssociationType,
                Lifecycle = association.Lifecycle,
                Frozen = association.Frozen,
                Schema = association.Schema is null ? null : CreateSchema(association.Schema)
            };
        }

        return new AssociationResponse
        {
            ResourceName = response.ResourceName,
            ResourceNamespace = response.ResourceNamespace,
            ResourceId = response.ResourceId,
            ResourceType = response.ResourceType,
            Associations = associations
        };
    }

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
            MigrationRules = ToReadOnlyRules(ruleSet.MigrationRules),
            DomainRules = ToReadOnlyRules(ruleSet.DomainRules),
            EncodingRules = ToReadOnlyRules(ruleSet.EncodingRules),
            EnableAt = ruleSet.EnableAt,
            HasFixedRuleCollections = true
        };
    }

    private static IReadOnlyList<SchemaRule>? ToReadOnlyRules(IReadOnlyList<SchemaRuleDto>? rules) =>
        rules is null
            ? null
            : Array.AsReadOnly(rules.Select(ToRule).ToArray());

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

    private static RegisterKekRequestDto ToRegisterKekRequestDto(RegisterKekRequest request) => new()
    {
        Name = request.Name,
        KmsType = request.KmsType,
        KmsKeyId = request.KmsKeyId,
        KmsProps = request.KmsProps?.ToDictionary(
            static kvp => kvp.Key,
            static kvp => kvp.Value,
            StringComparer.Ordinal),
        Doc = request.Doc,
        Shared = request.Shared,
        Deleted = request.Deleted
    };

    private static Kek ToKek(KekDto dto) => new()
    {
        Name = dto.Name ?? string.Empty,
        KmsType = dto.KmsType ?? string.Empty,
        KmsKeyId = dto.KmsKeyId ?? string.Empty,
        KmsProps = dto.KmsProps,
        Doc = dto.Doc,
        Shared = dto.Shared,
        Deleted = dto.Deleted,
        Timestamp = ReadTimestamp(dto.Ts)
    };

    private static RegisterDekRequestDto ToRegisterDekRequestDto(RegisterDekRequest request) => new()
    {
        Subject = request.Subject,
        Version = request.Version,
        Algorithm = FormatDekAlgorithm(request.Algorithm),
        EncryptedKeyMaterial = request.EncryptedKeyMaterial,
        Deleted = request.Deleted
    };

    private static Dek ToDek(DekDto dto) => new()
    {
        KekName = dto.KekName ?? string.Empty,
        Subject = dto.Subject ?? string.Empty,
        Version = dto.Version,
        Algorithm = ParseDekAlgorithm(dto.Algorithm),
        EncryptedKeyMaterial = dto.EncryptedKeyMaterial,
        KeyMaterial = dto.KeyMaterial,
        Deleted = dto.Deleted,
        Timestamp = ReadTimestamp(dto.Ts)
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

    private static string? FormatDekAlgorithm(DekAlgorithm? algorithm)
        => algorithm.HasValue ? FormatDekAlgorithm(algorithm.Value) : null;

    private static string FormatDekAlgorithm(DekAlgorithm algorithm)
        => algorithm switch
        {
            DekAlgorithm.Aes128Gcm => "AES128_GCM",
            DekAlgorithm.Aes256Gcm => "AES256_GCM",
            DekAlgorithm.Aes256Siv => "AES256_SIV",
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported DEK algorithm.")
        };

    private static DekAlgorithm ParseDekAlgorithm(string? algorithm)
        => algorithm?.ToUpperInvariant() switch
        {
            "AES128_GCM" => DekAlgorithm.Aes128Gcm,
            "AES256_GCM" => DekAlgorithm.Aes256Gcm,
            "AES256_SIV" => DekAlgorithm.Aes256Siv,
            _ => DekAlgorithm.Unknown
        };

    private static long? ReadTimestamp(JsonElement? value)
    {
        if (!value.HasValue)
            return null;

        var element = value.Value;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var timestamp))
            return timestamp;

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("timestamp", out var timestampElement) &&
            timestampElement.ValueKind == JsonValueKind.Number &&
            timestampElement.TryGetInt64(out timestamp))
        {
            return timestamp;
        }

        return null;
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
    /// TLS certificate, trust, validation, and protocol settings for the default HTTP pipeline.
    /// Cannot be combined with a caller-supplied HTTP handler.
    /// </summary>
    public SchemaRegistryTlsConfig? Tls { get; init; }

    /// <summary>
    /// Whether the default pipeline uses a proxy. Default is true, which uses the platform proxy.
    /// Cannot be configured when a caller supplies the HTTP pipeline.
    /// </summary>
    public bool UseProxy { get; init; } = true;

    /// <summary>
    /// Proxy used by the default HTTP pipeline.
    /// </summary>
    public IWebProxy? Proxy { get; init; }

    /// <summary>
    /// Default request headers applied to every Schema Registry request.
    /// Content headers, Accept, and User-Agent are rejected.
    /// </summary>
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }

    /// <summary>
    /// User-Agent header value. Defaults to a versioned Dekaf.SchemaRegistry product value.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Request timeout in milliseconds. Use -1 for an infinite timeout.
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Maximum number of schemas to cache.
    /// </summary>
    public int MaxCachedSchemas { get; init; } = 1000;

    /// <summary>
    /// TTL in seconds for caches holding latest schemas. Use -1 for no expiry.
    /// Default is -1.
    /// </summary>
    public int LatestCacheTtlSecs { get; init; } = -1;

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
