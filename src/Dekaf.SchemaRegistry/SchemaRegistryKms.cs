using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Identifies a KMS key used to wrap or unwrap Schema Registry data encryption keys.
/// </summary>
public sealed class SchemaRegistryKmsKeyReference
{
    /// <summary>
    /// KMS provider type, for example aws-kms, azure-kv, gcp-kms, or local-kms.
    /// </summary>
    public required string KmsType { get; init; }

    /// <summary>
    /// Provider-specific key identifier or URL.
    /// </summary>
    public required string KmsKeyId { get; init; }

    /// <summary>
    /// Provider-specific KMS properties from Schema Registry.
    /// </summary>
    public IReadOnlyDictionary<string, string>? KmsProps { get; init; }
}

/// <summary>
/// Wraps and unwraps Schema Registry data encryption keys with a KMS-managed key encryption key.
/// </summary>
public interface ISchemaRegistryKmsProvider
{
    /// <summary>
    /// KMS provider type handled by this provider.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Wraps raw data encryption key material.
    /// </summary>
    /// <param name="keyMaterial">Raw data encryption key material.</param>
    /// <param name="keyReference">KMS key reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Wrapped key material.</returns>
    ValueTask<byte[]> WrapKeyAsync(
        ReadOnlyMemory<byte> keyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unwraps encrypted data encryption key material.
    /// </summary>
    /// <param name="encryptedKeyMaterial">Wrapped key material.</param>
    /// <param name="keyReference">KMS key reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw data encryption key material.</returns>
    ValueTask<byte[]> UnwrapKeyAsync(
        ReadOnlyMemory<byte> encryptedKeyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for configuring Schema Registry KMS providers.
/// </summary>
public sealed class SchemaRegistryKmsOptions
{
    /// <summary>
    /// Providers available to Schema Registry rule handlers.
    /// </summary>
    public IList<ISchemaRegistryKmsProvider> Providers { get; } = [];

    /// <summary>
    /// Creates a provider registry from the configured providers.
    /// </summary>
    public SchemaRegistryKmsProviderRegistry CreateProviderRegistry() => new(Providers);
}

/// <summary>
/// Resolves Schema Registry KMS providers by KMS type.
/// </summary>
public sealed class SchemaRegistryKmsProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ISchemaRegistryKmsProvider> _providers;

    /// <summary>
    /// Creates a KMS provider registry.
    /// </summary>
    /// <param name="providers">KMS providers keyed by <see cref="ISchemaRegistryKmsProvider.Type" />.</param>
    public SchemaRegistryKmsProviderRegistry(IEnumerable<ISchemaRegistryKmsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var dictionary = new Dictionary<string, ISchemaRegistryKmsProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.Type))
                throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(providers));

            if (!dictionary.TryAdd(provider.Type, provider))
                throw new ArgumentException($"A KMS provider for type '{provider.Type}' is already registered.", nameof(providers));
        }

        _providers = dictionary;
    }

    /// <summary>
    /// Attempts to resolve a KMS provider by type.
    /// </summary>
    public bool TryGetProvider(
        string kmsType,
        [NotNullWhen(true)] out ISchemaRegistryKmsProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(kmsType))
        {
            provider = null;
            return false;
        }

        return _providers.TryGetValue(kmsType, out provider);
    }

    /// <summary>
    /// Resolves a KMS provider by type.
    /// </summary>
    public ISchemaRegistryKmsProvider GetProvider(string kmsType)
    {
        if (TryGetProvider(kmsType, out var provider))
            return provider;

        throw new SchemaRegistryKmsException(
            $"No Schema Registry KMS provider is registered for KMS type '{kmsType}'.");
    }

    /// <summary>
    /// Wraps raw data encryption key material with the provider selected by <paramref name="keyReference" />.
    /// </summary>
    public ValueTask<byte[]> WrapKeyAsync(
        ReadOnlyMemory<byte> keyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        return GetProvider(keyReference.KmsType).WrapKeyAsync(keyMaterial, keyReference, cancellationToken);
    }

    /// <summary>
    /// Unwraps encrypted data encryption key material with the provider selected by <paramref name="keyReference" />.
    /// </summary>
    public ValueTask<byte[]> UnwrapKeyAsync(
        ReadOnlyMemory<byte> encryptedKeyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyReference);
        return GetProvider(keyReference.KmsType).UnwrapKeyAsync(encryptedKeyMaterial, keyReference, cancellationToken);
    }
}

/// <summary>
/// Deterministic local KMS provider for tests and offline deployments.
/// </summary>
/// <remarks>
/// The provider uses AES Key Wrap with Padding (AES-KWP) and never contacts an external KMS.
/// </remarks>
public sealed class LocalKmsProvider : ISchemaRegistryKmsProvider
{
    /// <summary>
    /// Default local provider type.
    /// </summary>
    public const string DefaultType = "local-kms";

    private readonly IReadOnlyDictionary<string, byte[]> _keysById;

    /// <summary>
    /// Creates a local KMS provider.
    /// </summary>
    /// <param name="keysById">AES KEK material keyed by KMS key identifier.</param>
    /// <param name="type">KMS provider type.</param>
    public LocalKmsProvider(
        IReadOnlyDictionary<string, byte[]> keysById,
        string type = DefaultType)
    {
        ArgumentNullException.ThrowIfNull(keysById);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("KMS provider type cannot be null or whitespace.", nameof(type));

        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in keysById)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                throw new ArgumentException("KMS key identifiers cannot be null or whitespace.", nameof(keysById));

            if (entry.Value is null)
                throw new ArgumentException("KMS key material cannot be null.", nameof(keysById));

            if (!IsValidAesKeyLength(entry.Value.Length))
                throw new ArgumentException("Local KMS KEK material must be 128, 192, or 256 bits.", nameof(keysById));

            keys.Add(entry.Key, entry.Value.ToArray());
        }

        Type = type;
        _keysById = keys;
    }

    /// <inheritdoc />
    public string Type { get; }

    /// <inheritdoc />
    public ValueTask<byte[]> WrapKeyAsync(
        ReadOnlyMemory<byte> keyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(keyReference);
        if (keyMaterial.IsEmpty)
            throw new SchemaRegistryKmsException("Local KMS wrap failed. Key material cannot be empty.");

        var kek = ResolveKey(keyReference);

        try
        {
            using var aes = Aes.Create();
            aes.Key = kek;
            return ValueTask.FromResult(aes.EncryptKeyWrapPadded(keyMaterial.Span));
        }
        catch (CryptographicException ex)
        {
            throw new SchemaRegistryKmsException("Local KMS wrap failed for the configured KEK.", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask<byte[]> UnwrapKeyAsync(
        ReadOnlyMemory<byte> encryptedKeyMaterial,
        SchemaRegistryKmsKeyReference keyReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(keyReference);
        if (encryptedKeyMaterial.IsEmpty)
            throw new SchemaRegistryKmsException("Local KMS unwrap failed. Encrypted key material cannot be empty.");

        var kek = ResolveKey(keyReference);

        try
        {
            using var aes = Aes.Create();
            aes.Key = kek;
            return ValueTask.FromResult(aes.DecryptKeyWrapPadded(encryptedKeyMaterial.Span));
        }
        catch (CryptographicException ex)
        {
            throw new SchemaRegistryKmsException(
                "Local KMS unwrap failed. The encrypted key material is invalid for the configured KEK.",
                ex);
        }
    }

    private byte[] ResolveKey(SchemaRegistryKmsKeyReference keyReference)
    {
        if (!string.Equals(Type, keyReference.KmsType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaRegistryKmsException(
                $"Local KMS provider '{Type}' cannot resolve KMS type '{keyReference.KmsType}'.");
        }

        if (_keysById.TryGetValue(keyReference.KmsKeyId, out var kek))
            return kek;

        throw new SchemaRegistryKmsException(
            $"Local KMS key '{keyReference.KmsKeyId}' is not registered.");
    }

    private static bool IsValidAesKeyLength(int length) => length is 16 or 24 or 32;
}

/// <summary>
/// Exception thrown by Schema Registry KMS operations.
/// </summary>
public sealed class SchemaRegistryKmsException : Exception
{
    /// <summary>
    /// Creates a KMS exception.
    /// </summary>
    public SchemaRegistryKmsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a KMS exception with an inner exception.
    /// </summary>
    public SchemaRegistryKmsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
