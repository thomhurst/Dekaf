using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Schema Registry CSFLE rule handler for ENCRYPT rules.
/// </summary>
/// <remarks>
/// The handler supports whole-payload encryption for every payload format and
/// tagged string-field encryption for JSON payloads when schema metadata tags
/// are present.
/// </remarks>
public sealed class SchemaRegistryCsfleRuleHandler : ISchemaRegistryRuleHandler
{
    /// <summary>
    /// Default Schema Registry CSFLE encrypt rule type.
    /// </summary>
    public const string EncryptRuleType = "ENCRYPT";

    private const string KekNameParameter = "encrypt.kek.name";
    private const string DekAlgorithmParameter = "encrypt.dek.algorithm";
    private const string DekExpiryDaysParameter = "encrypt.dek.expiry.days";
    private const string DekSubjectParameter = "encrypt.dek.subject";
    private const string DeterministicParameter = "encrypt.deterministic";
    private const string KmsTypeParameter = "encrypt.kms.type";
    private const string KmsKeyIdParameter = "encrypt.kms.key.id";
    private const string KmsPropsPrefix = "encrypt.kms.props.";
    private const int VersionPrefixLength = 5;
    private const int AesGcmNonceLength = 12;
    private const int AesGcmTagLength = 16;
    private const int AesSivLength = 16;
    private const long MillisInDay = 24L * 60 * 60 * 1000;
    private const byte VersionPrefixMagic = 0x00;

    private static readonly byte[] EmptyAssociatedData = [];
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(30);

    private readonly ISchemaRegistryClient _schemaRegistryClient;
    private readonly SchemaRegistryKmsProviderRegistry _kmsProviders;
    private readonly TimeSpan _operationTimeout;
    private readonly ConcurrentDictionary<WriteDekCacheKey, Lazy<ResolvedDek>> _writeDeks = new();
    private readonly ConcurrentDictionary<ReadDekCacheKey, Lazy<ResolvedDek>> _readDeks = new();

    /// <summary>
    /// Creates a CSFLE rule handler.
    /// </summary>
    public SchemaRegistryCsfleRuleHandler(
        ISchemaRegistryClient schemaRegistryClient,
        SchemaRegistryKmsProviderRegistry kmsProviders,
        TimeSpan? operationTimeout = null,
        string type = EncryptRuleType)
    {
        ArgumentNullException.ThrowIfNull(schemaRegistryClient);
        ArgumentNullException.ThrowIfNull(kmsProviders);
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Rule handler type cannot be null or whitespace.", nameof(type));

        _schemaRegistryClient = schemaRegistryClient;
        _kmsProviders = kmsProviders;
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        Type = type;
    }

    /// <summary>
    /// Creates a CSFLE rule handler.
    /// </summary>
    public SchemaRegistryCsfleRuleHandler(
        ISchemaRegistryClient schemaRegistryClient,
        IEnumerable<ISchemaRegistryKmsProvider> kmsProviders,
        TimeSpan? operationTimeout = null,
        string type = EncryptRuleType)
        : this(schemaRegistryClient, new SchemaRegistryKmsProviderRegistry(kmsProviders), operationTimeout, type)
    {
    }

    /// <inheritdoc />
    public string Type { get; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = ResolveSettings(context);
        return TransformJsonFields(payload, context, settings, encrypt: true) ??
            EncryptPayload(payload, context, settings);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = ResolveSettings(context);
        return TransformJsonFields(payload, context, settings, encrypt: false) ??
            DecryptPayload(payload, context, settings);
    }

    private ReadOnlyMemory<byte>? TransformJsonFields(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings,
        bool encrypt)
    {
        if (context.PayloadContext.PayloadFormat != SchemaRegistryPayloadFormat.Json ||
            context.Rule.Tags is null ||
            context.Rule.Tags.Count == 0)
        {
            return null;
        }

        var fieldPaths = ResolveTaggedJsonPaths(context);
        using var document = JsonDocument.Parse(payload);
        using var output = new MemoryStream(payload.Length + 128);
        using (var writer = new Utf8JsonWriter(output))
        {
            WriteJsonElement(
                writer,
                document.RootElement,
                "$",
                fieldPaths,
                context,
                settings,
                encrypt);
        }

        return output.ToArray();
    }

    private static HashSet<string> ResolveTaggedJsonPaths(SchemaRegistryRuleHandlerContext context)
    {
        var metadataTags = context.PayloadContext.Schema?.Metadata?.Tags;
        if (metadataTags is null || metadataTags.Count == 0)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' requires schema metadata tags for tagged field encryption.");
        }

        var ruleTags = context.Rule.Tags!;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, tags) in metadataTags)
        {
            if (tags.Any(tag => ruleTags.Contains(tag)))
                paths.Add(path);
        }

        if (paths.Count == 0)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' did not match any schema metadata tag paths.");
        }

        return paths;
    }

    private void WriteJsonElement(
        Utf8JsonWriter writer,
        JsonElement element,
        string path,
        HashSet<string> fieldPaths,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings,
        bool encrypt)
    {
        if (fieldPaths.Contains(path))
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                element.WriteTo(writer);
                return;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{context.Rule.Name}' can only encrypt JSON string fields; path '{path}' is {element.ValueKind}.");
            }

            var value = element.GetString() ?? string.Empty;
            var transformed = encrypt
                ? Convert.ToBase64String(EncryptPayload(Encoding.UTF8.GetBytes(value), context, settings).Span)
                : DecryptJsonField(value, context, settings, path);

            writer.WriteStringValue(transformed);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteJsonElement(
                        writer,
                        property.Value,
                        path == "$" ? "$." + property.Name : path + "." + property.Name,
                        fieldPaths,
                        context,
                        settings,
                        encrypt);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    WriteJsonElement(
                        writer,
                        item,
                        path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        fieldPaths,
                        context,
                        settings,
                        encrypt);
                    index++;
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private string DecryptJsonField(
        string value,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings,
        string path)
    {
        var encrypted = DecodeBase64(
            value,
            $"encrypted JSON field at path '{path}'",
            context.Rule.Name);
        var decrypted = DecryptPayload(encrypted, context, settings);
        return Encoding.UTF8.GetString(decrypted.Span);
    }

    private ReadOnlyMemory<byte> EncryptPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings)
    {
        var dek = GetWriteDek(settings);
        ValidateKeyMaterial(dek.KeyMaterial, settings.Algorithm, context.Rule.Name);
        var encrypted = EncryptBytes(payload.Span, dek.KeyMaterial, settings.Algorithm, EmptyAssociatedData);
        return WriteEncryptedPayload(encrypted, dek, settings.IsDekRotated);
    }

    private ReadOnlyMemory<byte> DecryptPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings)
    {
        var ciphertext = payload.Span;
        int? dekVersion = null;
        if (settings.IsDekRotated)
        {
            var versionedPayload = ExtractVersionPrefix(ciphertext, context.Rule.Name);
            dekVersion = versionedPayload.DekVersion;
            ciphertext = versionedPayload.Ciphertext;
        }

        var dek = GetReadDek(settings, dekVersion);

        ValidateKeyMaterial(dek.KeyMaterial, dek.Algorithm, context.Rule.Name);
        var envelope = ReadEncryptedPayload(ciphertext, dek.Algorithm, context.Rule.Name);
        return DecryptBytes(envelope, dek.KeyMaterial, EmptyAssociatedData, context.Rule.Name);
    }

    private ResolvedDek GetWriteDek(EncryptionSettings settings)
    {
        var key = new WriteDekCacheKey(settings.KekName, settings.Subject, settings.Algorithm);
        while (true)
        {
            var lazy = _writeDeks.GetOrAdd(
                key,
                _ => new Lazy<ResolvedDek>(() => ResolveWriteDek(settings), LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var dek = lazy.Value;
                if (!IsExpired(settings, dek))
                    return dek;
            }
            catch
            {
                RemoveCachedDek(_writeDeks, key, lazy);
                throw;
            }

            RemoveCachedDek(_writeDeks, key, lazy);
        }
    }

    private ResolvedDek GetReadDek(EncryptionSettings settings, int? version)
    {
        var key = new ReadDekCacheKey(settings.KekName, settings.Subject, settings.Algorithm, version);
        var lazy = _readDeks.GetOrAdd(
            key,
            _ => new Lazy<ResolvedDek>(() => ResolveReadDek(settings, version), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return lazy.Value;
        }
        catch
        {
            RemoveCachedDek(_readDeks, key, lazy);
            throw;
        }
    }

    private ResolvedDek ResolveWriteDek(EncryptionSettings settings)
    {
        var kek = ResolveKek(settings);
        Dek? existing = null;
        try
        {
            existing = WaitFor(
                _schemaRegistryClient.GetDekAsync(settings.KekName, settings.Subject, settings.Algorithm));
            if (!IsExpired(settings, existing))
                return ResolveDekMaterial(existing, kek, settings.Subject, settings.RuleName, settings.Algorithm);
        }
        catch (SchemaRegistryException ex) when (IsNotFound(ex))
        {
        }

        var rawKey = RandomNumberGenerator.GetBytes(GetKeyLength(settings.Algorithm));
        var keyMaterial = CreateTinkKeyMaterial(settings.Algorithm, rawKey);
        var encryptedKeyMaterial = WaitFor(
            _kmsProviders.WrapKeyAsync(keyMaterial, ToKeyReference(kek)));

        var registered = WaitFor(
            _schemaRegistryClient.RegisterDekAsync(
                settings.KekName,
                new RegisterDekRequest
                {
                    Subject = settings.Subject,
                    Version = existing is null ? null : existing.Version + 1,
                    Algorithm = settings.Algorithm,
                    EncryptedKeyMaterial = Convert.ToBase64String(encryptedKeyMaterial)
                }));

        return new ResolvedDek(settings.Algorithm, registered.Version, rawKey, registered.Timestamp);
    }

    private ResolvedDek ResolveReadDek(EncryptionSettings settings, int? version)
    {
        var kek = ResolveKek(settings);
        var dek = version is { } dekVersion
            ? WaitFor(_schemaRegistryClient.GetDekAsync(settings.KekName, settings.Subject, dekVersion))
            : WaitFor(_schemaRegistryClient.GetDekAsync(settings.KekName, settings.Subject, settings.Algorithm));

        return ResolveDekMaterial(dek, kek, settings.Subject, settings.RuleName, settings.Algorithm);
    }

    private ResolvedDek ResolveDekMaterial(
        Dek dek,
        Kek kek,
        string subject,
        string ruleName,
        DekAlgorithm fallbackAlgorithm)
    {
        var algorithm = dek.Algorithm == DekAlgorithm.Unknown ? fallbackAlgorithm : dek.Algorithm;
        if (!string.IsNullOrWhiteSpace(dek.KeyMaterial))
        {
            return new ResolvedDek(
                algorithm,
                dek.Version,
                NormalizeKeyMaterial(
                    DecodeBase64(dek.KeyMaterial, "DEK key material", ruleName),
                    algorithm,
                    ruleName),
                dek.Timestamp);
        }

        if (string.IsNullOrWhiteSpace(dek.EncryptedKeyMaterial))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry DEK for subject '{subject}' has no key material.");
        }

        var encryptedKeyMaterial = DecodeBase64(dek.EncryptedKeyMaterial, "encrypted DEK key material", ruleName);
        var keyMaterial = WaitFor(_kmsProviders.UnwrapKeyAsync(encryptedKeyMaterial, ToKeyReference(kek)));
        var rawKey = NormalizeKeyMaterial(keyMaterial, algorithm, ruleName);
        return new ResolvedDek(algorithm, dek.Version, rawKey, dek.Timestamp);
    }

    private Kek ResolveKek(EncryptionSettings settings)
    {
        try
        {
            return WaitFor(_schemaRegistryClient.GetKekAsync(settings.KekName));
        }
        catch (SchemaRegistryException ex) when (IsNotFound(ex))
        {
            if (settings.KmsType is null || settings.KmsKeyId is null)
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry KEK '{settings.KekName}' was not found. Configure '{KmsTypeParameter}' and '{KmsKeyIdParameter}' to auto-register it.",
                    ex);
            }

            return WaitFor(
                _schemaRegistryClient.RegisterKekAsync(
                    new RegisterKekRequest
                    {
                        Name = settings.KekName,
                        KmsType = settings.KmsType,
                        KmsKeyId = settings.KmsKeyId,
                        KmsProps = settings.KmsProps
                    }));
        }
    }

    private static EncryptionSettings ResolveSettings(SchemaRegistryRuleHandlerContext context)
    {
        var rule = context.Rule;
        if (!TryGetParameter(rule, KekNameParameter, out var kekName))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' is missing required parameter '{KekNameParameter}'.");
        }

        var subject = ResolveSubject(context, rule);
        var algorithm = ResolveAlgorithm(rule);
        var dekExpiryDays = ResolveDekExpiryDays(rule);
        TryGetParameter(rule, KmsTypeParameter, out var kmsType);
        TryGetParameter(rule, KmsKeyIdParameter, out var kmsKeyId);

        return new EncryptionSettings(
            kekName,
            subject,
            algorithm,
            rule.Name,
            dekExpiryDays,
            NullIfEmpty(kmsType),
            NullIfEmpty(kmsKeyId),
            ResolveKmsProps(rule));
    }

    private static string ResolveSubject(SchemaRegistryRuleHandlerContext context, SchemaRule rule)
    {
        if (TryGetParameter(rule, DekSubjectParameter, out var subject))
            return subject;

        if (!string.IsNullOrWhiteSpace(context.PayloadContext.Subject))
            return context.PayloadContext.Subject;

        if (string.IsNullOrWhiteSpace(context.PayloadContext.Topic))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' cannot resolve a DEK subject.");
        }

        return context.PayloadContext.Topic + (context.PayloadContext.Component == Serialization.SerializationComponent.Key ? "-key" : "-value");
    }

    private static DekAlgorithm ResolveAlgorithm(SchemaRule rule)
    {
        if (TryGetParameter(rule, DekAlgorithmParameter, out var algorithm))
            return ParseDekAlgorithmParameter(algorithm, rule.Name);

        if (TryGetParameter(rule, DeterministicParameter, out var deterministic) &&
            bool.TryParse(deterministic, out var deterministicValue) &&
            deterministicValue)
        {
            return DekAlgorithm.Aes256Siv;
        }

        return DekAlgorithm.Aes256Gcm;
    }

    private static int ResolveDekExpiryDays(SchemaRule rule)
    {
        if (!TryGetParameter(rule, DekExpiryDaysParameter, out var expiryDays))
            return 0;

        if (!int.TryParse(expiryDays, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' has invalid '{DekExpiryDaysParameter}' value '{expiryDays}'.");
        }

        return value;
    }

    private static IReadOnlyDictionary<string, string>? ResolveKmsProps(SchemaRule rule)
    {
        if (rule.Parameters is null)
            return null;

        Dictionary<string, string>? props = null;
        foreach (var (key, value) in rule.Parameters)
        {
            if (!key.StartsWith(KmsPropsPrefix, StringComparison.Ordinal) || string.IsNullOrEmpty(value))
                continue;

            props ??= new Dictionary<string, string>(StringComparer.Ordinal);
            props[key[KmsPropsPrefix.Length..]] = value;
        }

        return props;
    }

    private static bool TryGetParameter(SchemaRule rule, string name, out string value)
    {
        if (rule.Parameters is not null &&
            rule.Parameters.TryGetValue(name, out var parameterValue) &&
            !string.IsNullOrWhiteSpace(parameterValue))
        {
            value = parameterValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DekAlgorithm ParseDekAlgorithmParameter(string algorithm, string ruleName)
        => algorithm.ToUpperInvariant() switch
        {
            "AES128_GCM" => DekAlgorithm.Aes128Gcm,
            "AES256_GCM" => DekAlgorithm.Aes256Gcm,
            "AES256_SIV" => DekAlgorithm.Aes256Siv,
            _ => throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' has unsupported DEK algorithm '{algorithm}'.")
        };

    private static int GetKeyLength(DekAlgorithm algorithm)
        => algorithm switch
        {
            DekAlgorithm.Aes128Gcm => 16,
            DekAlgorithm.Aes256Gcm => 32,
            DekAlgorithm.Aes256Siv => 64,
            _ => throw new SchemaRegistryRuleException("Unsupported DEK algorithm.")
        };

    private static void ValidateKeyMaterial(byte[] keyMaterial, DekAlgorithm algorithm, string ruleName)
    {
        var expectedLength = GetKeyLength(algorithm);
        if (keyMaterial.Length != expectedLength)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' received invalid DEK material length for {algorithm}.");
        }
    }

    private static byte[] CreateTinkKeyMaterial(DekAlgorithm algorithm, ReadOnlySpan<byte> rawKey)
    {
        var expectedLength = GetKeyLength(algorithm);
        if (rawKey.Length != expectedLength)
            throw new SchemaRegistryRuleException("Unsupported DEK algorithm.");

        var keyMaterial = new byte[2 + rawKey.Length];
        keyMaterial[0] = 0x12;
        keyMaterial[1] = checked((byte)rawKey.Length);
        rawKey.CopyTo(keyMaterial.AsSpan(2));
        return keyMaterial;
    }

    private static byte[] NormalizeKeyMaterial(byte[] keyMaterial, DekAlgorithm algorithm, string ruleName)
    {
        var expectedLength = GetKeyLength(algorithm);
        if (keyMaterial.Length == expectedLength)
            return keyMaterial;

        if (TryReadTinkKeyValue(keyMaterial, expectedLength, out var rawKey))
            return rawKey;

        throw new SchemaRegistryRuleException(
            $"Schema Registry rule '{ruleName}' received invalid DEK material length for {algorithm}.");
    }

    private static bool TryReadTinkKeyValue(ReadOnlySpan<byte> keyMaterial, int expectedLength, out byte[] rawKey)
    {
        var offset = 0;
        while (offset < keyMaterial.Length)
        {
            if (!TryReadVarInt(keyMaterial, ref offset, out var tag))
                break;

            var fieldNumber = (int)(tag >> 3);
            var wireType = (int)(tag & 0x07);
            if (fieldNumber == 2 && wireType == 2)
            {
                if (!TryReadVarInt(keyMaterial, ref offset, out var length) ||
                    length != (ulong)expectedLength ||
                    keyMaterial.Length - offset < (int)length)
                {
                    break;
                }

                rawKey = keyMaterial.Slice(offset, (int)length).ToArray();
                return true;
            }

            if (!TrySkipProtobufValue(keyMaterial, ref offset, wireType))
                break;
        }

        rawKey = [];
        return false;
    }

    private static bool TrySkipProtobufValue(ReadOnlySpan<byte> data, ref int offset, int wireType)
    {
        switch (wireType)
        {
            case 0:
                return TryReadVarInt(data, ref offset, out _);
            case 1:
                offset += sizeof(ulong);
                return offset <= data.Length;
            case 2:
                if (!TryReadVarInt(data, ref offset, out var length) || length > int.MaxValue)
                    return false;
                offset += (int)length;
                return offset <= data.Length;
            case 5:
                offset += sizeof(uint);
                return offset <= data.Length;
            default:
                return false;
        }
    }

    private static bool TryReadVarInt(ReadOnlySpan<byte> data, ref int offset, out ulong value)
    {
        value = 0;
        var shift = 0;
        while (offset < data.Length && shift < 64)
        {
            var current = data[offset++];
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return true;

            shift += 7;
        }

        return false;
    }

    private static byte[] DecodeBase64(string value, string description, string ruleName)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' received invalid base64 {description}.",
                ex);
        }
    }

    private static SchemaRegistryKmsKeyReference ToKeyReference(Kek kek) => new()
    {
        KmsType = kek.KmsType,
        KmsKeyId = kek.KmsKeyId,
        KmsProps = kek.KmsProps
    };

    private static EncryptedPayload EncryptBytes(
        ReadOnlySpan<byte> plaintext,
        byte[] keyMaterial,
        DekAlgorithm algorithm,
        byte[] associatedData)
    {
        if (algorithm == DekAlgorithm.Aes256Siv)
            return EncryptAesSiv(plaintext, keyMaterial, associatedData);

        var nonce = RandomNumberGenerator.GetBytes(AesGcmNonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcmTagLength];
        using var aes = new AesGcm(keyMaterial, tagSizeInBytes: AesGcmTagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return new EncryptedPayload(algorithm, nonce, tag, ciphertext);
    }

    private static ReadOnlyMemory<byte> DecryptBytes(
        EncryptedEnvelope envelope,
        byte[] keyMaterial,
        byte[] associatedData,
        string ruleName)
    {
        try
        {
            if (envelope.Algorithm == DekAlgorithm.Aes256Siv)
                return DecryptAesSiv(envelope, keyMaterial, associatedData, ruleName);

            var plaintext = new byte[envelope.Ciphertext.Length];
            using var aes = new AesGcm(keyMaterial, tagSizeInBytes: AesGcmTagLength);
            aes.Decrypt(envelope.NonceOrIv, envelope.Ciphertext, envelope.Tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' failed to decrypt encrypted payload.",
                ex);
        }
    }

    private static EncryptedPayload EncryptAesSiv(
        ReadOnlySpan<byte> plaintext,
        byte[] keyMaterial,
        byte[] associatedData)
    {
        var macKey = keyMaterial.AsSpan(0, 32);
        var encryptionKey = keyMaterial.AsSpan(32, 32);
        var siv = ComputeSiv(macKey, plaintext, associatedData);
        var ciphertext = AesCtrCrypt(encryptionKey, siv, plaintext);
        return new EncryptedPayload(DekAlgorithm.Aes256Siv, siv, [], ciphertext);
    }

    private static ReadOnlyMemory<byte> DecryptAesSiv(
        EncryptedEnvelope envelope,
        byte[] keyMaterial,
        byte[] associatedData,
        string ruleName)
    {
        var macKey = keyMaterial.AsSpan(0, 32);
        var encryptionKey = keyMaterial.AsSpan(32, 32);
        var plaintext = AesCtrCrypt(encryptionKey, envelope.NonceOrIv, envelope.Ciphertext);
        var expectedSiv = ComputeSiv(macKey, plaintext, associatedData);
        if (!CryptographicOperations.FixedTimeEquals(expectedSiv, envelope.NonceOrIv))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' failed to authenticate encrypted payload.");
        }

        return plaintext;
    }

    private static byte[] WriteEncryptedPayload(EncryptedPayload payload, ResolvedDek dek, bool includeDekVersion)
    {
        var ciphertext = WriteTinkCiphertext(payload);
        if (!includeDekVersion)
            return ciphertext;

        var result = new byte[VersionPrefixLength + ciphertext.Length];
        var span = result.AsSpan();
        span[0] = VersionPrefixMagic;
        BinaryPrimitives.WriteInt32BigEndian(span[1..VersionPrefixLength], dek.Version);
        ciphertext.CopyTo(span[VersionPrefixLength..]);
        return result;
    }

    private static byte[] WriteTinkCiphertext(EncryptedPayload payload)
    {
        var length = payload.Algorithm == DekAlgorithm.Aes256Siv
            ? payload.NonceOrIv.Length + payload.Ciphertext.Length
            : payload.NonceOrIv.Length + payload.Ciphertext.Length + payload.Tag.Length;
        var result = new byte[length];
        var span = result.AsSpan();

        payload.NonceOrIv.CopyTo(span);
        var offset = payload.NonceOrIv.Length;
        payload.Ciphertext.CopyTo(span[offset..]);
        offset += payload.Ciphertext.Length;
        payload.Tag.CopyTo(span[offset..]);
        return result;
    }

    private static VersionedPayload ExtractVersionPrefix(ReadOnlySpan<byte> payload, string ruleName)
    {
        if (payload.Length < VersionPrefixLength || payload[0] != VersionPrefixMagic)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' expected a CSFLE DEK version prefix.");
        }

        var version = BinaryPrimitives.ReadInt32BigEndian(payload[1..VersionPrefixLength]);
        return new VersionedPayload(version, payload[VersionPrefixLength..]);
    }

    private static EncryptedEnvelope ReadEncryptedPayload(
        ReadOnlySpan<byte> payload,
        DekAlgorithm algorithm,
        string ruleName)
    {
        return algorithm switch
        {
            DekAlgorithm.Aes128Gcm or DekAlgorithm.Aes256Gcm => ReadAesGcmPayload(payload, algorithm, ruleName),
            DekAlgorithm.Aes256Siv => ReadAesSivPayload(payload, ruleName),
            _ => throw new SchemaRegistryRuleException("Unsupported DEK algorithm.")
        };
    }

    private static EncryptedEnvelope ReadAesGcmPayload(
        ReadOnlySpan<byte> payload,
        DekAlgorithm algorithm,
        string ruleName)
    {
        if (payload.Length < AesGcmNonceLength + AesGcmTagLength)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' received a truncated encrypted payload.");
        }

        return new EncryptedEnvelope(
            algorithm,
            payload[..AesGcmNonceLength].ToArray(),
            payload[^AesGcmTagLength..].ToArray(),
            payload[AesGcmNonceLength..^AesGcmTagLength].ToArray());
    }

    private static EncryptedEnvelope ReadAesSivPayload(ReadOnlySpan<byte> payload, string ruleName)
    {
        if (payload.Length < AesSivLength)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' received a truncated encrypted payload.");
        }

        return new EncryptedEnvelope(
            DekAlgorithm.Aes256Siv,
            payload[..AesSivLength].ToArray(),
            [],
            payload[AesSivLength..].ToArray());
    }

    private static byte[] ComputeSiv(
        ReadOnlySpan<byte> macKey,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        Span<byte> d = stackalloc byte[16];
        AesCmac(macKey, ReadOnlySpan<byte>.Empty).CopyTo(d);
        if (!associatedData.IsEmpty)
        {
            DoubleBlock(d);
            var adMac = AesCmac(macKey, associatedData);
            XorInPlace(d, adMac);
        }

        Span<byte> input = plaintext.Length >= 16
            ? plaintext.ToArray()
            : stackalloc byte[16];

        if (plaintext.Length >= 16)
        {
            XorInPlace(input[^16..], d);
            return AesCmac(macKey, input);
        }

        input.Clear();
        plaintext.CopyTo(input);
        input[plaintext.Length] = 0x80;
        DoubleBlock(d);
        XorInPlace(input, d);
        return AesCmac(macKey, input);
    }

    private static byte[] AesCmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message)
    {
        Span<byte> zero = stackalloc byte[16];
        Span<byte> l = stackalloc byte[16];
        using var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.EncryptEcb(zero, l, PaddingMode.None);

        Span<byte> k1 = stackalloc byte[16];
        l.CopyTo(k1);
        DoubleBlock(k1);

        Span<byte> k2 = stackalloc byte[16];
        k1.CopyTo(k2);
        DoubleBlock(k2);

        var blockCount = Math.Max(1, (message.Length + 15) / 16);
        Span<byte> state = stackalloc byte[16];
        Span<byte> block = stackalloc byte[16];

        for (var blockIndex = 0; blockIndex < blockCount - 1; blockIndex++)
        {
            message.Slice(blockIndex * 16, 16).CopyTo(block);
            XorInPlace(block, state);
            aes.EncryptEcb(block, state, PaddingMode.None);
        }

        block.Clear();
        var lastOffset = (blockCount - 1) * 16;
        var remaining = message.Length - lastOffset;
        if (remaining == 16 && message.Length != 0)
        {
            message.Slice(lastOffset, 16).CopyTo(block);
            XorInPlace(block, k1);
        }
        else
        {
            if (remaining > 0)
                message.Slice(lastOffset, remaining).CopyTo(block);
            block[remaining] = 0x80;
            XorInPlace(block, k2);
        }

        XorInPlace(block, state);
        var result = new byte[16];
        aes.EncryptEcb(block, result, PaddingMode.None);
        return result;
    }

    private static byte[] AesCtrCrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length];
        Span<byte> counter = stackalloc byte[16];
        iv.CopyTo(counter);
        counter[8] &= 0x7F;
        counter[12] &= 0x7F;

        Span<byte> keyStream = stackalloc byte[16];
        using var aes = Aes.Create();
        aes.Key = key.ToArray();

        var offset = 0;
        while (offset < input.Length)
        {
            aes.EncryptEcb(counter, keyStream, PaddingMode.None);
            var count = Math.Min(16, input.Length - offset);
            for (var i = 0; i < count; i++)
                output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);

            IncrementCounter(counter);
            offset += count;
        }

        return output;
    }

    private static void IncrementCounter(Span<byte> counter)
    {
        for (var i = counter.Length - 1; i >= 0; i--)
        {
            counter[i]++;
            if (counter[i] != 0)
                break;
        }
    }

    private static void DoubleBlock(Span<byte> block)
    {
        var carry = 0;
        for (var i = block.Length - 1; i >= 0; i--)
        {
            var nextCarry = (block[i] & 0x80) != 0 ? 1 : 0;
            block[i] = (byte)((block[i] << 1) | carry);
            carry = nextCarry;
        }

        if (carry != 0)
            block[^1] ^= 0x87;
    }

    private static void XorInPlace(Span<byte> target, ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < target.Length; i++)
            target[i] ^= value[i];
    }

    private static bool IsExpired(EncryptionSettings settings, Dek dek)
        => IsExpired(settings, dek.Timestamp);

    private static bool IsExpired(EncryptionSettings settings, ResolvedDek dek)
        => IsExpired(settings, dek.Timestamp);

    private static bool IsExpired(EncryptionSettings settings, long? timestamp)
    {
        if (settings.DekExpiryDays <= 0 || timestamp is null)
            return false;

        var ageMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp.Value;
        return ageMillis >= 0 && ageMillis / MillisInDay >= settings.DekExpiryDays;
    }

    private static void RemoveCachedDek<TKey>(
        ConcurrentDictionary<TKey, Lazy<ResolvedDek>> cache,
        TKey key,
        Lazy<ResolvedDek> value)
        where TKey : notnull
    {
        ((ICollection<KeyValuePair<TKey, Lazy<ResolvedDek>>>)cache).Remove(
            new KeyValuePair<TKey, Lazy<ResolvedDek>>(key, value));
    }

    private static bool IsNotFound(SchemaRegistryException exception)
    {
        // Confluent DEK Registry uses 40470 for missing KEKs and 40471 for missing DEKs.
        return exception.ErrorCode is 404 or 40470 or 40471;
    }

    private T WaitFor<T>(Task<T> task)
        => task.WaitAsync(_operationTimeout).ConfigureAwait(false).GetAwaiter().GetResult();

    private T WaitFor<T>(ValueTask<T> task)
        => task.IsCompletedSuccessfully
            ? task.Result
            : task.AsTask().WaitAsync(_operationTimeout).ConfigureAwait(false).GetAwaiter().GetResult();

    private sealed record EncryptionSettings(
        string KekName,
        string Subject,
        DekAlgorithm Algorithm,
        string RuleName,
        int DekExpiryDays,
        string? KmsType,
        string? KmsKeyId,
        IReadOnlyDictionary<string, string>? KmsProps)
    {
        public bool IsDekRotated => DekExpiryDays > 0;
    }

    private sealed record ResolvedDek(DekAlgorithm Algorithm, int Version, byte[] KeyMaterial, long? Timestamp);

    private readonly record struct WriteDekCacheKey(string KekName, string Subject, DekAlgorithm Algorithm);

    private readonly record struct ReadDekCacheKey(
        string KekName,
        string Subject,
        DekAlgorithm Algorithm,
        int? Version);

    private sealed record EncryptedPayload(
        DekAlgorithm Algorithm,
        byte[] NonceOrIv,
        byte[] Tag,
        byte[] Ciphertext);

    private sealed record EncryptedEnvelope(
        DekAlgorithm Algorithm,
        byte[] NonceOrIv,
        byte[] Tag,
        byte[] Ciphertext);

    private readonly ref struct VersionedPayload(int dekVersion, ReadOnlySpan<byte> ciphertext)
    {
        public int DekVersion { get; } = dekVersion;

        public ReadOnlySpan<byte> Ciphertext { get; } = ciphertext;
    }
}
