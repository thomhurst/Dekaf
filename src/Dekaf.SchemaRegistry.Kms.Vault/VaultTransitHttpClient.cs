using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dekaf.SchemaRegistry.Kms.Vault;

/// <summary>
/// Supplies Vault tokens for Transit requests.
/// </summary>
public interface IVaultTokenProvider
{
    /// <summary>
    /// Gets a token for a Vault address and namespace.
    /// </summary>
    ValueTask<string> GetTokenAsync(
        Uri vaultAddress,
        string? vaultNamespace,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Supplies a fixed Vault token.
/// </summary>
public sealed class VaultStaticTokenProvider : IVaultTokenProvider
{
    private readonly string _token;

    /// <summary>
    /// Creates a fixed-token provider.
    /// </summary>
    /// <param name="token">Vault token.</param>
    public VaultStaticTokenProvider(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || ContainsNewLine(token))
            throw new ArgumentException("Vault token is invalid.", nameof(token));

        _token = token;
    }

    /// <inheritdoc />
    public ValueTask<string> GetTokenAsync(
        Uri vaultAddress,
        string? vaultNamespace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vaultAddress);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<string>(_token);
    }

    private static bool ContainsNewLine(string value) => value.Contains('\r') || value.Contains('\n');
}

/// <summary>
/// Logs in with Vault AppRole and caches tokens until shortly before their lease expires.
/// </summary>
public sealed class VaultAppRoleTokenProvider : IVaultTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _roleId;
    private readonly string _secretId;
    private readonly string _authMountPoint;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<AuthScope, TokenState> _tokens = [];

    /// <summary>
    /// Creates an AppRole token provider.
    /// </summary>
    /// <param name="httpClient">Caller-owned HTTP client.</param>
    /// <param name="roleId">AppRole role ID.</param>
    /// <param name="secretId">AppRole secret ID.</param>
    /// <param name="authMountPoint">AppRole authentication mount point.</param>
    public VaultAppRoleTokenProvider(
        HttpClient httpClient,
        string roleId,
        string secretId,
        string authMountPoint = "approle")
        : this(httpClient, roleId, secretId, authMountPoint, TimeProvider.System)
    {
    }

    internal VaultAppRoleTokenProvider(
        HttpClient httpClient,
        string roleId,
        string secretId,
        string authMountPoint,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (string.IsNullOrWhiteSpace(roleId))
            throw new ArgumentException("Vault AppRole role ID cannot be null or whitespace.", nameof(roleId));
        if (string.IsNullOrWhiteSpace(secretId))
            throw new ArgumentException("Vault AppRole secret ID cannot be null or whitespace.", nameof(secretId));

        _httpClient = httpClient;
        _roleId = roleId;
        _secretId = secretId;
        _authMountPoint = VaultTransitHttpClient.NormalizeMountPoint(authMountPoint);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ValueTask<string> GetTokenAsync(
        Uri vaultAddress,
        string? vaultNamespace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vaultAddress);
        cancellationToken.ThrowIfCancellationRequested();

        var scope = new AuthScope(
            VaultTransitHttpClient.NormalizeAddress(vaultAddress),
            VaultTransitHttpClient.NormalizeNamespace(vaultNamespace));
        var state = _tokens.GetOrAdd(scope, static _ => new TokenState());
        return state.TryGetToken(_timeProvider.GetUtcNow(), out var token)
            ? new ValueTask<string>(token)
            : RefreshTokenAsync(scope, state, cancellationToken);
    }

    private async ValueTask<string> RefreshTokenAsync(
        AuthScope scope,
        TokenState state,
        CancellationToken cancellationToken)
    {
        await state.RefreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.TryGetToken(_timeProvider.GetUtcNow(), out var cachedToken))
                return cachedToken;

            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(
                new AppRoleLoginRequest(_roleId, _secretId),
                VaultJsonContext.Default.AppRoleLoginRequest);
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    VaultTransitHttpClient.BuildUri(
                        scope.VaultAddress,
                        $"v1/auth/{VaultTransitHttpClient.EncodeMountPoint(_authMountPoint)}/login"));
                request.Content = new ByteArrayContent(requestBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                VaultTransitHttpClient.AddNamespaceHeader(request, scope.VaultNamespace);

                var loginStartedAt = _timeProvider.GetUtcNow();
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                VaultTransitHttpClient.EnsureSuccess(response);
                var responseBytes = await VaultTransitHttpClient
                    .ReadResponseBytesAsync(response.Content, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    var (token, leaseDurationSeconds) = ParseLoginResponse(responseBytes);
                    var refreshAfterSeconds = leaseDurationSeconds > 60
                        ? leaseDurationSeconds - 30
                        : Math.Max(1, leaseDurationSeconds / 2);
                    state.SetToken(token, loginStartedAt.AddSeconds(refreshAfterSeconds));
                    return token;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(responseBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(requestBytes);
            }
        }
        finally
        {
            state.RefreshGate.Release();
        }
    }

    private static (string Token, int LeaseDurationSeconds) ParseLoginResponse(ReadOnlyMemory<byte> responseBytes)
    {
        using var document = JsonDocument.Parse(responseBytes);
        if (!document.RootElement.TryGetProperty("auth", out var auth)
            || !auth.TryGetProperty("client_token", out var tokenElement)
            || tokenElement.GetString() is not { Length: > 0 } token
            || token.Contains('\r')
            || token.Contains('\n')
            || !auth.TryGetProperty("lease_duration", out var leaseElement)
            || !leaseElement.TryGetInt32(out var leaseDurationSeconds)
            || leaseDurationSeconds <= 0)
        {
            throw new InvalidOperationException("Vault AppRole login returned invalid authentication data.");
        }

        return (token, leaseDurationSeconds);
    }

    private readonly record struct AuthScope(Uri VaultAddress, string? VaultNamespace);

    private sealed class TokenState
    {
        private string? _token;
        private long _expiresAtUtcTicks;

        internal SemaphoreSlim RefreshGate { get; } = new(1, 1);

        internal bool TryGetToken(DateTimeOffset now, out string token)
        {
            var expiresAtUtcTicks = Volatile.Read(ref _expiresAtUtcTicks);
            var cachedToken = Volatile.Read(ref _token);
            if (expiresAtUtcTicks > now.UtcTicks && cachedToken is { Length: > 0 })
            {
                token = cachedToken;
                return true;
            }

            token = string.Empty;
            return false;
        }

        internal void SetToken(string token, DateTimeOffset expiresAt)
        {
            Volatile.Write(ref _token, token);
            Volatile.Write(ref _expiresAtUtcTicks, expiresAt.UtcTicks);
        }
    }
}

/// <summary>
/// Calls the HashiCorp Vault Transit HTTP API.
/// </summary>
public sealed class VaultTransitHttpClient : IVaultTransitClient
{
    private const int MaximumResponseBytes = 1024 * 1024;

    private static ReadOnlySpan<byte> PlaintextPrefix => "{\"plaintext\":\""u8;
    private static ReadOnlySpan<byte> CiphertextPrefix => "{\"ciphertext\":\""u8;
    private static ReadOnlySpan<byte> JsonStringSuffix => "\"}"u8;

    private readonly HttpClient _httpClient;
    private readonly IVaultTokenProvider _tokenProvider;

    /// <summary>
    /// Creates a Vault Transit HTTP client.
    /// </summary>
    /// <param name="httpClient">Caller-owned HTTP client.</param>
    /// <param name="tokenProvider">Vault token provider.</param>
    public VaultTransitHttpClient(HttpClient httpClient, IVaultTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> EncryptAsync(
        Uri vaultAddress,
        string mountPoint,
        string keyName,
        string? vaultNamespace,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default)
    {
        if (plaintext.IsEmpty)
            throw new ArgumentException("Vault plaintext cannot be empty.", nameof(plaintext));

        var payload = RentBase64Payload(plaintext.Span, out var payloadLength);
        try
        {
            return await SendTransitRequestAsync(
                    vaultAddress,
                    mountPoint,
                    keyName,
                    vaultNamespace,
                    "encrypt",
                    payload,
                    payloadLength,
                    "ciphertext",
                    responseIsBase64: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ReturnSensitiveBuffer(payload);
        }
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> DecryptAsync(
        Uri vaultAddress,
        string mountPoint,
        string keyName,
        string? vaultNamespace,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default)
    {
        if (ciphertext.IsEmpty)
            throw new ArgumentException("Vault ciphertext cannot be empty.", nameof(ciphertext));

        var payload = RentCiphertextPayload(ciphertext.Span, out var payloadLength);
        try
        {
            return await SendTransitRequestAsync(
                    vaultAddress,
                    mountPoint,
                    keyName,
                    vaultNamespace,
                    "decrypt",
                    payload,
                    payloadLength,
                    "plaintext",
                    responseIsBase64: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ReturnSensitiveBuffer(payload);
        }
    }

    internal static string NormalizeMountPoint(string mountPoint)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
            throw new ArgumentException("Vault mount point cannot be null or whitespace.", nameof(mountPoint));

        var normalized = mountPoint.Trim('/');
        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new ArgumentException("Vault mount point is invalid.", nameof(mountPoint));
        }

        return normalized;
    }

    internal static string? NormalizeNamespace(string? vaultNamespace)
    {
        if (vaultNamespace is null)
            return null;

        var normalized = vaultNamespace.Trim('/');
        if (normalized.Length == 0
            || normalized.Contains('\r')
            || normalized.Contains('\n'))
        {
            throw new ArgumentException("Vault namespace is invalid.", nameof(vaultNamespace));
        }

        return normalized;
    }

    internal static Uri NormalizeAddress(Uri vaultAddress)
    {
        ArgumentNullException.ThrowIfNull(vaultAddress);
        if (!vaultAddress.IsAbsoluteUri
            || vaultAddress.Scheme is not ("http" or "https")
            || vaultAddress.UserInfo.Length != 0
            || vaultAddress.Query.Length != 0
            || vaultAddress.Fragment.Length != 0
            || vaultAddress.AbsolutePath is not ("" or "/"))
        {
            throw new ArgumentException(
                "Vault address must be an absolute HTTP or HTTPS URI without a path, credentials, query, or fragment.",
                nameof(vaultAddress));
        }

        return new Uri(vaultAddress.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
    }

    internal static string EncodeMountPoint(string mountPoint)
    {
        var segments = mountPoint.Split('/');
        var builder = new StringBuilder(mountPoint.Length);
        for (var index = 0; index < segments.Length; index++)
        {
            if (index != 0)
                builder.Append('/');
            builder.Append(Uri.EscapeDataString(segments[index]));
        }

        return builder.ToString();
    }

    internal static Uri BuildUri(Uri vaultAddress, string relativePath)
    {
        return new Uri(NormalizeAddress(vaultAddress), relativePath);
    }

    internal static void AddNamespaceHeader(HttpRequestMessage request, string? vaultNamespace)
    {
        if (NormalizeNamespace(vaultNamespace) is { } normalizedNamespace)
            request.Headers.TryAddWithoutValidation("X-Vault-Namespace", normalizedNamespace);
    }

    internal static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Vault request failed with HTTP status {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
    }

    internal static ValueTask<byte[]> ReadResponseBytesAsync(
        HttpContent content,
        CancellationToken cancellationToken) =>
        ReadResponseBytesAsync(content, ArrayPool<byte>.Shared, cancellationToken);

    internal static async ValueTask<byte[]> ReadResponseBytesAsync(
        HttpContent content,
        ArrayPool<byte> bufferPool,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes)
            throw new InvalidOperationException("Vault response exceeded the maximum allowed size.");

        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var configuredStream = stream.ConfigureAwait(false);
        var initialCapacity = content.Headers.ContentLength is > 0 and var contentLength
            ? (int)contentLength
            : 4096;
        var responseBuffer = bufferPool.Rent(initialCapacity);
        var written = 0;
        try
        {
            while (true)
            {
                if (written == MaximumResponseBytes)
                {
                    var probeBuffer = bufferPool.Rent(1);
                    try
                    {
                        if (await stream.ReadAsync(probeBuffer.AsMemory(0, 1), cancellationToken)
                                .ConfigureAwait(false) != 0)
                        {
                            throw new InvalidOperationException("Vault response exceeded the maximum allowed size.");
                        }
                    }
                    finally
                    {
                        ReturnSensitiveBuffer(bufferPool, probeBuffer);
                    }

                    return responseBuffer.AsSpan(0, written).ToArray();
                }

                if (written == responseBuffer.Length)
                {
                    var nextCapacity = Math.Min(MaximumResponseBytes, responseBuffer.Length * 2);
                    var retiredBuffer = responseBuffer;
                    responseBuffer = bufferPool.Rent(nextCapacity);
                    try
                    {
                        retiredBuffer.AsSpan(0, written).CopyTo(responseBuffer);
                    }
                    finally
                    {
                        ReturnSensitiveBuffer(bufferPool, retiredBuffer);
                    }
                }

                var available = Math.Min(responseBuffer.Length - written, MaximumResponseBytes - written);
                var read = await stream
                    .ReadAsync(responseBuffer.AsMemory(written, available), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    return responseBuffer.AsSpan(0, written).ToArray();

                written += read;
            }
        }
        finally
        {
            ReturnSensitiveBuffer(bufferPool, responseBuffer);
        }
    }

    private async ValueTask<byte[]> SendTransitRequestAsync(
        Uri vaultAddress,
        string mountPoint,
        string keyName,
        string? vaultNamespace,
        string operation,
        byte[] payload,
        int payloadLength,
        string responseProperty,
        bool responseIsBase64,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        vaultAddress = NormalizeAddress(vaultAddress);
        var normalizedMountPoint = NormalizeMountPoint(mountPoint);
        vaultNamespace = NormalizeNamespace(vaultNamespace);
        if (string.IsNullOrWhiteSpace(keyName)
            || keyName.Contains('/')
            || keyName is "." or "..")
            throw new ArgumentException("Vault Transit key name is invalid.", nameof(keyName));

        var token = await _tokenProvider
            .GetTokenAsync(vaultAddress, vaultNamespace, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token) || token.Contains('\r') || token.Contains('\n'))
            throw new InvalidOperationException("Vault token provider returned an invalid token.");

        var path = $"v1/{EncodeMountPoint(normalizedMountPoint)}/{operation}/{Uri.EscapeDataString(keyName)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(vaultAddress, path));
        request.Headers.TryAddWithoutValidation("X-Vault-Token", token);
        AddNamespaceHeader(request, vaultNamespace);
        request.Content = new ByteArrayContent(payload, 0, payloadLength);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response);
        var responseBytes = await ReadResponseBytesAsync(response.Content, cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(responseBytes);
            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty(responseProperty, out var material))
            {
                throw new InvalidOperationException("Vault Transit response did not contain key material.");
            }

            if (responseIsBase64)
                return material.GetBytesFromBase64();

            return material.GetString() is { Length: > 0 } ciphertext
                ? Encoding.UTF8.GetBytes(ciphertext)
                : throw new InvalidOperationException("Vault Transit response did not contain key material.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(responseBytes);
        }
    }

    private static byte[] RentBase64Payload(ReadOnlySpan<byte> plaintext, out int payloadLength)
    {
        var maximumEncodedLength = Base64.GetMaxEncodedToUtf8Length(plaintext.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(
            checked(PlaintextPrefix.Length + maximumEncodedLength + JsonStringSuffix.Length));
        PlaintextPrefix.CopyTo(buffer);
        var destination = buffer.AsSpan(PlaintextPrefix.Length, maximumEncodedLength);
        var status = Base64.EncodeToUtf8(plaintext, destination, out var consumed, out var written);
        if (status != OperationStatus.Done || consumed != plaintext.Length)
        {
            ReturnSensitiveBuffer(buffer);
            throw new InvalidOperationException("Vault plaintext could not be encoded.");
        }

        JsonStringSuffix.CopyTo(buffer.AsSpan(PlaintextPrefix.Length + written));
        payloadLength = PlaintextPrefix.Length + written + JsonStringSuffix.Length;
        return buffer;
    }

    private static byte[] RentCiphertextPayload(ReadOnlySpan<byte> ciphertext, out int payloadLength)
    {
        foreach (var value in ciphertext)
        {
            if (value is < 0x20 or > 0x7e or (byte)'"' or (byte)'\\')
                throw new FormatException("Vault ciphertext is not valid ASCII Transit ciphertext.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(
            checked(CiphertextPrefix.Length + ciphertext.Length + JsonStringSuffix.Length));
        CiphertextPrefix.CopyTo(buffer);
        ciphertext.CopyTo(buffer.AsSpan(CiphertextPrefix.Length));
        JsonStringSuffix.CopyTo(buffer.AsSpan(CiphertextPrefix.Length + ciphertext.Length));
        payloadLength = CiphertextPrefix.Length + ciphertext.Length + JsonStringSuffix.Length;
        return buffer;
    }

    private static void ReturnSensitiveBuffer(byte[] buffer)
        => ReturnSensitiveBuffer(ArrayPool<byte>.Shared, buffer);

    private static void ReturnSensitiveBuffer(ArrayPool<byte> bufferPool, byte[] buffer)
    {
        CryptographicOperations.ZeroMemory(buffer);
        bufferPool.Return(buffer);
    }
}

internal sealed record AppRoleLoginRequest(
    [property: JsonPropertyName("role_id")] string RoleId,
    [property: JsonPropertyName("secret_id")] string SecretId);

[JsonSerializable(typeof(AppRoleLoginRequest))]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
