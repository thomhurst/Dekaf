using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    private const int DekCacheCapacity = 256;
    private const long MillisInDay = 24L * 60 * 60 * 1000;
    private const byte VersionPrefixMagic = 0x00;

    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(30);

    [ThreadStatic]
    private static ConditionalWeakTable<SchemaRegistryCsfleRuleHandler, CsfleWorkspace>? t_workspaces;

    private readonly ISchemaRegistryClient _schemaRegistryClient;
    private readonly SchemaRegistryKmsProviderRegistry _kmsProviders;
    private readonly TimeSpan _operationTimeout;
    private readonly ConcurrentDictionary<WriteDekCacheKey, DekCacheEntry> _writeDeks = new();
    private readonly Queue<(WriteDekCacheKey Key, DekCacheEntry Entry)> _writeDekOrder = new(DekCacheCapacity);
    private readonly object _writeDekCacheLock = new();
    private readonly ConcurrentDictionary<ReadDekCacheKey, DekCacheEntry> _readDeks = new();
    private readonly Queue<(ReadDekCacheKey Key, DekCacheEntry Entry)> _readDekOrder = new(DekCacheCapacity);
    private readonly object _readDekCacheLock = new();
    private readonly ConditionalWeakTable<SchemaRule, RuleSettingsCache> _settings = new();
    private readonly ConditionalWeakTable<SchemaMetadata, TaggedJsonPlanCache> _taggedJsonPlans = new();

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
        var settings = GetSettings(context);
        return TransformJsonFields(payload, context, settings, encrypt: true) ??
            EncryptPayload(payload, context, settings);
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = GetSettings(context);
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

        var schema = context.PayloadContext.Schema;
        var metadata = schema?.Metadata;
        if (metadata?.Tags is not { Count: > 0 })
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' requires schema metadata tags for tagged field encryption.");
        }

        var plan = _taggedJsonPlans
            .GetValue(metadata, static value => new TaggedJsonPlanCache(value))
            .Get(
                context.Rule,
                context.PayloadContext.Schema?.RuleSet?.HasFixedRuleCollections != true);
        return TransformTaggedJson(payload, context, settings, plan, encrypt);
    }

    private ReadOnlyMemory<byte> TransformTaggedJson(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings,
        TaggedJsonPlan plan,
        bool encrypt)
    {
        var workspace = GetWorkspace();
        using var workspaceOperation = new CsfleWorkspaceOperation(workspace);
        workspace.JsonNodes[0] = plan.Root;
        var output = workspace.CreateOutput(payload.Span, payload.Length + 128);
        var reader = new Utf8JsonReader(payload.Span);
        JsonPathNode? pendingNode = null;
        var pendingProperty = false;
        var copiedThrough = 0;
        var matchedTarget = false;

        while (reader.Read())
        {
            JsonPathNode? valueNode = null;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    pendingNode = workspace.JsonNodes[reader.CurrentDepth - 1]?.FindProperty(ref reader, workspace);
                    pendingProperty = true;
                    continue;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    if (reader.CurrentDepth == 0)
                    {
                        valueNode = plan.Root;
                    }
                    else
                    {
                        valueNode = ResolveJsonValueNode(
                            reader.CurrentDepth,
                            ref pendingNode,
                            ref pendingProperty,
                            workspace);
                    }

                    var isTarget = plan.IsTarget(valueNode, context.Rule);
                    matchedTarget |= isTarget;
                    if (isTarget)
                    {
                        throw new SchemaRegistryRuleException(
                            $"Schema Registry rule '{context.Rule.Name}' can only encrypt JSON string fields; the tagged path is {reader.TokenType}.");
                    }

                    workspace.JsonNodes[reader.CurrentDepth] = valueNode;
                    workspace.JsonArrayIndices[reader.CurrentDepth] = 0;
                    continue;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    workspace.JsonNodes[reader.CurrentDepth] = null;
                    continue;
                default:
                    valueNode = ResolveJsonValueNode(
                        reader.CurrentDepth,
                        ref pendingNode,
                        ref pendingProperty,
                        workspace);
                    break;
            }

            var transformsValue = plan.IsTarget(valueNode, context.Rule);
            matchedTarget |= transformsValue;
            if (!transformsValue || reader.TokenType == JsonTokenType.Null)
                continue;

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{context.Rule.Name}' can only encrypt JSON string fields; the tagged path is {reader.TokenType}.");
            }

            var tokenStart = checked((int)reader.TokenStartIndex);
            output.Append(payload.Span[copiedThrough..tokenStart]);
            WriteTransformedJsonString(ref output, ref reader, context, settings, workspace, encrypt);
            copiedThrough = checked((int)reader.BytesConsumed);
        }

        if (!matchedTarget && !plan.HasTarget(context.Rule))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' did not match any schema metadata tag paths.");
        }

        output.Append(payload.Span[copiedThrough..]);
        return output.WrittenMemory;
    }

    private ReadOnlyMemory<byte> EncryptPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings)
    {
        var dek = GetWriteDek(settings);
        ValidateKeyMaterial(dek.KeyMaterial, settings.Algorithm, context.Rule.Name);
        var workspace = GetWorkspace();
        var length = GetEncryptedPayloadLength(payload.Length, settings.Algorithm, settings.IsDekRotated);
        var output = workspace.CreateOutput(payload.Span, length);
        EncryptPayload(payload.Span, dek, settings, workspace, output.GetSpan(length)[..length]);
        GC.KeepAlive(dek);
        output.Advance(length);
        return output.WrittenMemory;
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
        var plaintextLength = GetPlaintextLength(ciphertext, dek.Algorithm, context.Rule.Name);
        var workspace = GetWorkspace();
        var output = workspace.CreateOutput(payload.Span, plaintextLength);
        DecryptPayload(
            ciphertext,
            dek,
            workspace,
            output.GetSpan(plaintextLength)[..plaintextLength],
            context.Rule.Name);
        GC.KeepAlive(dek);
        output.Advance(plaintextLength);
        return output.WrittenMemory;
    }

    private static JsonPathNode? ResolveJsonValueNode(
        int depth,
        ref JsonPathNode? pendingNode,
        ref bool pendingProperty,
        CsfleWorkspace workspace)
    {
        if (depth == 0)
            return workspace.JsonNodes[0];

        if (pendingProperty)
        {
            pendingProperty = false;
            var result = pendingNode;
            pendingNode = null;
            return result;
        }

        var parentDepth = depth - 1;
        var index = workspace.JsonArrayIndices[parentDepth]++;
        return workspace.JsonNodes[parentDepth]?.FindIndex(index);
    }

    private void WriteTransformedJsonString(
        ref PooledBufferBuilder output,
        ref Utf8JsonReader reader,
        SchemaRegistryRuleHandlerContext context,
        EncryptionSettings settings,
        CsfleWorkspace workspace,
        bool encrypt)
    {
        var encodedValue = workspace.GetTemporary(0, reader.ValueSpan.Length);
        var valueLength = reader.CopyString(encodedValue);
        if (encrypt)
        {
            var dek = GetWriteDek(settings);
            ValidateKeyMaterial(dek.KeyMaterial, settings.Algorithm, context.Rule.Name);
            var outputLength = GetEncryptedPayloadLength(valueLength, settings.Algorithm, settings.IsDekRotated);
            var encrypted = workspace.GetTemporary(1, outputLength);
            EncryptPayload(encodedValue[..valueLength], dek, settings, workspace, encrypted[..outputLength]);
            GC.KeepAlive(dek);

            output.Append((byte)'"');
            var encodedLength = Base64.GetMaxEncodedToUtf8Length(outputLength);
            var encodeStatus = Base64.EncodeToUtf8(
                encrypted[..outputLength],
                output.GetSpan(encodedLength),
                out _,
                out var written);
            if (encodeStatus != OperationStatus.Done)
                throw new SchemaRegistryRuleException($"Schema Registry rule '{context.Rule.Name}' failed to encode an encrypted JSON field.");
            output.Advance(written);
            output.Append((byte)'"');
            return;
        }

        var status = Base64.DecodeFromUtf8InPlace(encodedValue[..valueLength], out var encryptedLength);
        if (status != OperationStatus.Done)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' received invalid base64 encrypted JSON field data.");
        }

        ReadOnlySpan<byte> ciphertext = encodedValue[..encryptedLength];
        int? dekVersion = null;
        if (settings.IsDekRotated)
        {
            var versioned = ExtractVersionPrefix(ciphertext, context.Rule.Name);
            dekVersion = versioned.DekVersion;
            ciphertext = versioned.Ciphertext;
        }

        var readDek = GetReadDek(settings, dekVersion);
        ValidateKeyMaterial(readDek.KeyMaterial, readDek.Algorithm, context.Rule.Name);
        var plaintextLength = GetPlaintextLength(ciphertext, readDek.Algorithm, context.Rule.Name);
        var plaintext = workspace.GetTemporary(1, plaintextLength);
        DecryptPayload(ciphertext, readDek, workspace, plaintext[..plaintextLength], context.Rule.Name);
        GC.KeepAlive(readDek);
        WriteJsonString(ref output, plaintext[..plaintextLength]);
    }

    private static void WriteJsonString(ref PooledBufferBuilder output, ReadOnlySpan<byte> value)
    {
        output.Append((byte)'"');
        var copiedThrough = 0;
        Span<byte> unicodeEscape = stackalloc byte[6];
        "\\u00"u8.CopyTo(unicodeEscape);
        for (var i = 0; i < value.Length; i++)
        {
            ReadOnlySpan<byte> escape = value[i] switch
            {
                (byte)'"' => "\\\""u8,
                (byte)'\\' => "\\\\"u8,
                (byte)'\b' => "\\b"u8,
                (byte)'\f' => "\\f"u8,
                (byte)'\n' => "\\n"u8,
                (byte)'\r' => "\\r"u8,
                (byte)'\t' => "\\t"u8,
                < 0x20 => default,
                _ => ""u8
            };
            if (escape.Length == 0 && value[i] >= 0x20)
                continue;

            output.Append(value[copiedThrough..i]);
            if (value[i] < 0x20 && escape.IsEmpty)
            {
                unicodeEscape[4] = ToHex(value[i] >> 4);
                unicodeEscape[5] = ToHex(value[i] & 0x0F);
                output.Append(unicodeEscape);
            }
            else
            {
                output.Append(escape);
            }

            copiedThrough = i + 1;
        }

        output.Append(value[copiedThrough..]);
        output.Append((byte)'"');
    }

    private static byte ToHex(int value) => (byte)(value < 10 ? '0' + value : 'A' + value - 10);

    private CsfleWorkspace GetWorkspace() =>
        (t_workspaces ??= new ConditionalWeakTable<SchemaRegistryCsfleRuleHandler, CsfleWorkspace>())
        .GetValue(this, static _ => new CsfleWorkspace());

    private static TaggedJsonPlan CreateTaggedJsonPlan(
        SchemaMetadata metadata,
        SchemaRule rule,
        bool mutableTags)
    {
        var root = new JsonPathNode();
        List<JsonPathNode>? mutableTargets = mutableTags ? [] : null;
        var matched = false;
        foreach (var (path, tags) in metadata.Tags!)
        {
            var applies = false;
            foreach (var tag in tags)
            {
                if (rule.Tags!.Contains(tag))
                {
                    applies = true;
                    break;
                }
            }

            if (!applies && !mutableTags)
                continue;

            var target = AddJsonPath(root, path, rule.Name);
            if (mutableTags)
            {
                target.IsTarget = applies;
                target.MutablePath = path;
                target.MutableTags = tags;
                target.MutableTagsVersion = GetSetVersion(tags);
                target.MutableRuleTagsVersion = GetSetVersion(rule.Tags!);
                mutableTargets!.Add(target);
            }

            matched |= applies;
        }

        if (!matched)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' did not match any schema metadata tag paths.");
        }

        return new TaggedJsonPlan(
            root,
            mutableTargets?.ToArray(),
            mutableTags ? metadata.Tags : null);
    }

    private static JsonPathNode AddJsonPath(JsonPathNode root, string path, string ruleName)
    {
        if (path.Length == 0 || path[0] != '$')
            throw InvalidJsonPath(ruleName, path);

        var node = root;
        var offset = 1;
        while (offset < path.Length)
        {
            if (path[offset] == '.')
            {
                var start = ++offset;
                while (offset < path.Length && path[offset] is not ('.' or '['))
                    offset++;
                if (offset == start)
                    throw InvalidJsonPath(ruleName, path);
                node = node.GetOrAddProperty(path[start..offset]);
                continue;
            }

            if (path[offset] == '[')
            {
                var start = ++offset;
                while (offset < path.Length && char.IsAsciiDigit(path[offset]))
                    offset++;
                if (offset == start || offset >= path.Length || path[offset] != ']' ||
                    !int.TryParse(path.AsSpan(start, offset - start), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    throw InvalidJsonPath(ruleName, path);
                }

                offset++;
                node = node.GetOrAddIndex(index);
                continue;
            }

            throw InvalidJsonPath(ruleName, path);
        }

        node.IsTarget = true;
        return node;
    }

    private static int GetSetVersion(IReadOnlySet<string> tags) => tags switch
    {
        HashSet<string> hashSet => Volatile.Read(ref GetHashSetVersion(hashSet)),
        SortedSet<string> sortedSet => Volatile.Read(ref GetSortedSetVersion(sortedSet)),
        FrozenSet<string> or IImmutableSet<string> => 0,
        _ => throw new SchemaRegistryRuleException(
            $"Caller-owned CSFLE tag set type '{tags.GetType().FullName}' cannot be tracked for mutation without a per-message scan. " +
            "Use HashSet<string>, SortedSet<string>, FrozenSet<string>, or IImmutableSet<string>.")
    };

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref int GetMetadataVersion(Dictionary<string, IReadOnlySet<string>> dictionary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref int GetRuleParameterVersion(Dictionary<string, string> dictionary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref int GetHashSetVersion(HashSet<string> hashSet);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "version")]
    private static extern ref int GetSortedSetVersion(SortedSet<string> sortedSet);

    private static bool TagsOverlap(IReadOnlySet<string> tags, IReadOnlySet<string> ruleTags)
    {
        if (tags is HashSet<string> hashSet)
        {
            foreach (var tag in hashSet)
            {
                if (ruleTags.Contains(tag))
                    return true;
            }

            return false;
        }

        if (ruleTags is not HashSet<string> ruleHashSet)
        {
            foreach (var tag in tags)
            {
                if (ruleTags.Contains(tag))
                    return true;
            }

            return false;
        }

        foreach (var tag in ruleHashSet)
        {
            if (tags.Contains(tag))
                return true;
        }

        return false;
    }

    private static SchemaRegistryRuleException InvalidJsonPath(string ruleName, string path) =>
        new($"Schema Registry rule '{ruleName}' contains unsupported JSON metadata path '{path}'.");

    private ResolvedDek GetWriteDek(EncryptionSettings settings)
    {
        var key = new WriteDekCacheKey(settings.KekName, settings.Subject, settings.Algorithm);
        while (true)
        {
            if (!_writeDeks.TryGetValue(key, out var entry))
            {
                entry = _writeDeks.GetOrAdd(
                    key,
                    static (_, state) => new DekCacheEntry(
                        () => state.Handler.ResolveWriteDek(state.Settings)),
                    (Handler: this, Settings: settings));
                TrackCachedDek(
                    _writeDeks,
                    _writeDekOrder,
                    _writeDekCacheLock,
                    key,
                    entry);
            }

            try
            {
                if (entry.TryGetValue(out var dek))
                {
                    if (!IsExpired(settings, dek))
                        return dek;
                }
            }
            catch
            {
                RemoveCachedDek(_writeDeks, key, entry);
                throw;
            }

            RemoveCachedDek(_writeDeks, key, entry);
        }
    }

    private ResolvedDek GetReadDek(EncryptionSettings settings, int? version)
    {
        var key = new ReadDekCacheKey(settings.KekName, settings.Subject, settings.Algorithm, version);
        while (true)
        {
            if (!_readDeks.TryGetValue(key, out var entry))
            {
                entry = _readDeks.GetOrAdd(
                    key,
                    static (_, state) => new DekCacheEntry(
                        () => state.Handler.ResolveReadDek(state.Settings, state.Version)),
                    (Handler: this, Settings: settings, Version: version));
                TrackCachedDek(
                    _readDeks,
                    _readDekOrder,
                    _readDekCacheLock,
                    key,
                    entry);
            }

            try
            {
                if (entry.TryGetValue(out var dek))
                    return dek;
            }
            catch
            {
                RemoveCachedDek(_readDeks, key, entry);
                throw;
            }

            RemoveCachedDek(_readDeks, key, entry);
        }
    }

    private static void TrackCachedDek<TKey>(
        ConcurrentDictionary<TKey, DekCacheEntry> cache,
        Queue<(TKey Key, DekCacheEntry Entry)> order,
        object cacheLock,
        TKey key,
        DekCacheEntry entry)
        where TKey : notnull
    {
        if (!entry.TryTrack())
            return;

        lock (cacheLock)
        {
            order.Enqueue((key, entry));
            while (order.Count > DekCacheCapacity)
            {
                var evicted = order.Dequeue();
                RemoveCachedDek(cache, evicted.Key, evicted.Entry);
            }
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
            ? WaitFor(_schemaRegistryClient.GetDekAsync(
                settings.KekName,
                settings.Subject,
                dekVersion,
                settings.Algorithm))
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
                        KmsProps = settings.MutableRule is { } mutableRule
                            ? ResolveKmsProps(mutableRule)
                            : settings.KmsProps
                    }));
        }
    }

    private EncryptionSettings GetSettings(SchemaRegistryRuleHandlerContext context)
    {
        var rule = context.Rule;
        var subject = ResolveSubject(context, rule);
        if (context.PayloadContext.Schema?.RuleSet?.HasFixedRuleCollections != true)
        {
            return _settings
                .GetValue(rule, static value => new RuleSettingsCache(value))
                .GetMutable(subject);
        }

        return _settings.GetValue(rule, static value => new RuleSettingsCache(value)).Get(subject);
    }

    private static EncryptionSettings ResolveSettings(
        SchemaRule rule,
        string subject,
        bool cacheKmsProps = true)
    {
        if (!TryGetParameter(rule, KekNameParameter, out var kekName))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' is missing required parameter '{KekNameParameter}'.");
        }

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
            cacheKmsProps ? ResolveKmsProps(rule) : null,
            cacheKmsProps ? null : rule);
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

    private static int GetEncryptedPayloadLength(int plaintextLength, DekAlgorithm algorithm, bool includeDekVersion) =>
        plaintextLength +
        (algorithm == DekAlgorithm.Aes256Siv ? AesSivLength : AesGcmNonceLength + AesGcmTagLength) +
        (includeDekVersion ? VersionPrefixLength : 0);

    private static void EncryptPayload(
        ReadOnlySpan<byte> plaintext,
        ResolvedDek dek,
        EncryptionSettings settings,
        CsfleWorkspace workspace,
        Span<byte> destination)
    {
        var offset = 0;
        if (settings.IsDekRotated)
        {
            destination[0] = VersionPrefixMagic;
            BinaryPrimitives.WriteInt32BigEndian(destination[1..VersionPrefixLength], dek.Version);
            offset = VersionPrefixLength;
        }

        if (settings.Algorithm == DekAlgorithm.Aes256Siv)
        {
            var siv = destination.Slice(offset, AesSivLength);
            var ciphertext = destination[(offset + AesSivLength)..];
            var mac = workspace.GetBlockCipher(dek.KeyMaterial, 0);
            ComputeSiv(mac, plaintext, ReadOnlySpan<byte>.Empty, siv);
            AesCtrCrypt(workspace.GetBlockCipher(dek.KeyMaterial, 32), siv, plaintext, ciphertext);
            return;
        }

        var nonce = destination.Slice(offset, AesGcmNonceLength);
        var encrypted = destination.Slice(offset + AesGcmNonceLength, plaintext.Length);
        var tag = destination[^AesGcmTagLength..];
        RandomNumberGenerator.Fill(nonce);
        EncryptAesGcm(workspace.GetGcmCipher(dek.KeyMaterial), nonce, plaintext, encrypted, tag);
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

    private static int GetPlaintextLength(
        ReadOnlySpan<byte> payload,
        DekAlgorithm algorithm,
        string ruleName)
    {
        var overhead = algorithm switch
        {
            DekAlgorithm.Aes128Gcm or DekAlgorithm.Aes256Gcm => AesGcmNonceLength + AesGcmTagLength,
            DekAlgorithm.Aes256Siv => AesSivLength,
            _ => throw new SchemaRegistryRuleException("Unsupported DEK algorithm.")
        };
        if (payload.Length < overhead)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' received a truncated encrypted payload.");
        }

        return payload.Length - overhead;
    }

    private static void DecryptPayload(
        ReadOnlySpan<byte> payload,
        ResolvedDek dek,
        CsfleWorkspace workspace,
        Span<byte> destination,
        string ruleName)
    {
        try
        {
            if (dek.Algorithm == DekAlgorithm.Aes256Siv)
            {
                var siv = payload[..AesSivLength];
                AesCtrCrypt(workspace.GetBlockCipher(dek.KeyMaterial, 32), siv, payload[AesSivLength..], destination);
                Span<byte> expectedSiv = stackalloc byte[AesSivLength];
                ComputeSiv(workspace.GetBlockCipher(dek.KeyMaterial, 0), destination, ReadOnlySpan<byte>.Empty, expectedSiv);
                if (!CryptographicOperations.FixedTimeEquals(expectedSiv, siv))
                {
                    throw new SchemaRegistryRuleException(
                        $"Schema Registry rule '{ruleName}' failed to authenticate encrypted payload.");
                }

                return;
            }

            DecryptAesGcm(
                workspace.GetGcmCipher(dek.KeyMaterial),
                payload[..AesGcmNonceLength],
                payload[AesGcmNonceLength..^AesGcmTagLength],
                payload[^AesGcmTagLength..],
                destination);
        }
        catch (CryptographicException ex)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{ruleName}' failed to decrypt encrypted payload.",
                ex);
        }
    }

    private static void EncryptAesGcm(
        GcmCipher cipher,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag)
    {
        Span<byte> counter = stackalloc byte[16];
        nonce.CopyTo(counter);
        counter[15] = 1;
        AesGcmCtrCrypt(cipher.Cipher, counter, plaintext, ciphertext);

        Span<byte> authentication = stackalloc byte[16];
        ComputeGHash(cipher, ciphertext, authentication);
        counter[12..].Clear();
        counter[15] = 1;
        cipher.Cipher.EncryptBlock(counter, tag);
        XorInPlace(tag, authentication);
    }

    private static void DecryptAesGcm(
        GcmCipher cipher,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext)
    {
        Span<byte> counter = stackalloc byte[16];
        nonce.CopyTo(counter);
        counter[15] = 1;

        Span<byte> authentication = stackalloc byte[16];
        ComputeGHash(cipher, ciphertext, authentication);
        Span<byte> expectedTag = stackalloc byte[16];
        cipher.Cipher.EncryptBlock(counter, expectedTag);
        XorInPlace(expectedTag, authentication);
        if (!CryptographicOperations.FixedTimeEquals(expectedTag, tag))
            throw new CryptographicException("The computed authentication tag did not match the input authentication tag.");

        AesGcmCtrCrypt(cipher.Cipher, counter, ciphertext, plaintext);
    }

    private static void AesGcmCtrCrypt(
        BlockCipher cipher,
        Span<byte> counter,
        ReadOnlySpan<byte> input,
        Span<byte> output)
    {
        Span<byte> keyStream = stackalloc byte[16];
        var offset = 0;
        while (offset < input.Length)
        {
            IncrementCounter32(counter);
            cipher.EncryptBlock(counter, keyStream);
            var count = Math.Min(16, input.Length - offset);
            for (var i = 0; i < count; i++)
                output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);
            offset += count;
        }
    }

    private static void ComputeGHash(GcmCipher cipher, ReadOnlySpan<byte> ciphertext, Span<byte> destination)
    {
        UInt128 state = 0;
        Span<byte> block = stackalloc byte[16];
        var offset = 0;
        while (ciphertext.Length - offset >= 16)
        {
            state ^= BinaryPrimitives.ReadUInt128BigEndian(ciphertext.Slice(offset, 16));
            state = cipher.Multiply(state);
            offset += 16;
        }

        if (offset < ciphertext.Length)
        {
            block.Clear();
            ciphertext[offset..].CopyTo(block);
            state ^= BinaryPrimitives.ReadUInt128BigEndian(block);
            state = cipher.Multiply(state);
        }

        block.Clear();
        BinaryPrimitives.WriteUInt64BigEndian(block[8..], checked((ulong)ciphertext.Length * 8));
        state ^= BinaryPrimitives.ReadUInt128BigEndian(block);
        state = cipher.Multiply(state);
        BinaryPrimitives.WriteUInt128BigEndian(destination, state);
    }

    private static void IncrementCounter32(Span<byte> counter)
    {
        var value = BinaryPrimitives.ReadUInt32BigEndian(counter[12..]);
        BinaryPrimitives.WriteUInt32BigEndian(counter[12..], unchecked(value + 1));
    }

    private static void ComputeSiv(
        BlockCipher cipher,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        Span<byte> d = stackalloc byte[16];
        AesCmac(cipher, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, d);
        if (!associatedData.IsEmpty)
        {
            DoubleBlock(d);
            Span<byte> adMac = stackalloc byte[16];
            AesCmac(cipher, associatedData, ReadOnlySpan<byte>.Empty, adMac);
            XorInPlace(d, adMac);
        }

        if (plaintext.Length >= 16)
        {
            AesCmac(cipher, plaintext, d, destination);
            return;
        }

        Span<byte> input = stackalloc byte[16];
        input.Clear();
        plaintext.CopyTo(input);
        input[plaintext.Length] = 0x80;
        DoubleBlock(d);
        XorInPlace(input, d);
        AesCmac(cipher, input, ReadOnlySpan<byte>.Empty, destination);
    }

    private static void AesCmac(
        BlockCipher cipher,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> xorLast16,
        Span<byte> destination)
    {
        Span<byte> zero = stackalloc byte[16];
        Span<byte> l = stackalloc byte[16];
        cipher.EncryptBlock(zero, l);

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
            XorSivTail(block, blockIndex * 16, message.Length, xorLast16);
            XorInPlace(block, state);
            cipher.EncryptBlock(block, state);
        }

        block.Clear();
        var lastOffset = (blockCount - 1) * 16;
        var remaining = message.Length - lastOffset;
        if (remaining == 16 && message.Length != 0)
        {
            message.Slice(lastOffset, 16).CopyTo(block);
            XorSivTail(block, lastOffset, message.Length, xorLast16);
            XorInPlace(block, k1);
        }
        else
        {
            if (remaining > 0)
            {
                message.Slice(lastOffset, remaining).CopyTo(block);
                XorSivTail(block[..remaining], lastOffset, message.Length, xorLast16);
            }
            block[remaining] = 0x80;
            XorInPlace(block, k2);
        }

        XorInPlace(block, state);
        cipher.EncryptBlock(block, destination);
    }

    private static void XorSivTail(
        Span<byte> block,
        int blockOffset,
        int messageLength,
        ReadOnlySpan<byte> xorLast16)
    {
        if (xorLast16.IsEmpty)
            return;

        var xorStart = messageLength - AesSivLength;
        var start = Math.Max(blockOffset, xorStart);
        var end = Math.Min(blockOffset + block.Length, messageLength);
        for (var position = start; position < end; position++)
            block[position - blockOffset] ^= xorLast16[position - xorStart];
    }

    private static void AesCtrCrypt(
        BlockCipher cipher,
        ReadOnlySpan<byte> iv,
        ReadOnlySpan<byte> input,
        Span<byte> output)
    {
        Span<byte> counter = stackalloc byte[16];
        iv.CopyTo(counter);
        counter[8] &= 0x7F;
        counter[12] &= 0x7F;

        Span<byte> keyStream = stackalloc byte[16];

        var offset = 0;
        while (offset < input.Length)
        {
            cipher.EncryptBlock(counter, keyStream);
            var count = Math.Min(16, input.Length - offset);
            for (var i = 0; i < count; i++)
                output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);

            IncrementCounter(counter);
            offset += count;
        }
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
        ConcurrentDictionary<TKey, DekCacheEntry> cache,
        TKey key,
        DekCacheEntry value)
        where TKey : notnull
    {
        if (((ICollection<KeyValuePair<TKey, DekCacheEntry>>)cache).Remove(
                new KeyValuePair<TKey, DekCacheEntry>(key, value)))
        {
            value.Retire();
        }
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
        IReadOnlyDictionary<string, string>? KmsProps,
        SchemaRule? MutableRule)
    {
        public bool IsDekRotated => DekExpiryDays > 0;
    }

    private sealed class ResolvedDek(
        DekAlgorithm algorithm,
        int version,
        byte[] keyMaterial,
        long? timestamp)
    {
        public DekAlgorithm Algorithm { get; } = algorithm;

        public int Version { get; } = version;

        public byte[] KeyMaterial { get; } = keyMaterial;

        public long? Timestamp { get; } = timestamp;

        ~ResolvedDek() => CryptographicOperations.ZeroMemory(KeyMaterial);
    }

    private sealed class DekCacheEntry(Func<ResolvedDek> valueFactory)
    {
        private Lazy<ResolvedDek>? _value = new(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        private int _tracked;

        public bool TryGetValue(out ResolvedDek value)
        {
            var lazy = _value;
            if (lazy is null)
            {
                value = null!;
                return false;
            }

            value = lazy.Value;
            return true;
        }

        public bool TryTrack() => Interlocked.Exchange(ref _tracked, 1) == 0;

        public void Retire() => _value = null;
    }

    private readonly record struct WriteDekCacheKey(string KekName, string Subject, DekAlgorithm Algorithm);

    private readonly record struct ReadDekCacheKey(
        string KekName,
        string Subject,
        DekAlgorithm Algorithm,
        int? Version);

    private readonly ref struct VersionedPayload(int dekVersion, ReadOnlySpan<byte> ciphertext)
    {
        public int DekVersion { get; } = dekVersion;

        public ReadOnlySpan<byte> Ciphertext { get; } = ciphertext;
    }

    private sealed class TaggedJsonPlan(
        JsonPathNode root,
        JsonPathNode[]? mutableTargets,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? mutableMetadata)
    {
        public JsonPathNode Root { get; } = root;

        public bool IsTarget(JsonPathNode? node, SchemaRule rule) =>
            node is not null
            && (mutableTargets is null
                ? node.IsTarget
                : node.MutableTags is not null && node.RefreshMutableTarget(rule, mutableMetadata!));

        public bool HasTarget(SchemaRule rule)
        {
            if (mutableTargets is null)
                return true;

            for (var i = 0; i < mutableTargets.Length; i++)
            {
                if (mutableTargets[i].RefreshMutableTarget(rule, mutableMetadata!))
                    return true;
            }

            return false;
        }
    }

    private sealed class RuleSettingsCache
    {
        private readonly SchemaRule _rule;
        private readonly ConcurrentDictionary<string, EncryptionSettings> _settings = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, MutableRuleSettings> _mutableSettings = new(StringComparer.Ordinal);
        private readonly Func<string, EncryptionSettings> _createSettings;

        public RuleSettingsCache(SchemaRule rule)
        {
            _rule = rule;
            _createSettings = CreateSettings;
        }

        public EncryptionSettings Get(string subject) => _settings.GetOrAdd(subject, _createSettings);

        public EncryptionSettings GetMutable(string subject)
        {
            if (_rule.Parameters is Dictionary<string, string> parameters)
            {
                var version = Volatile.Read(ref GetRuleParameterVersion(parameters));
                if (_mutableSettings.TryGetValue(subject, out var versionedCached)
                    && versionedCached.Version == version
                    && versionedCached.Snapshot.Matches(parameters))
                {
                    return versionedCached.Settings;
                }

                var versionedSnapshot = RuleParameterSnapshot.Create(_rule);
                var versionedSettings = ResolveSettings(_rule, subject, cacheKmsProps: false);
                _mutableSettings[subject] = new MutableRuleSettings(version, versionedSnapshot, versionedSettings);
                return versionedSettings;
            }

            var snapshot = RuleParameterSnapshot.Create(_rule);
            if (_mutableSettings.TryGetValue(subject, out var cached)
                && cached.Snapshot == snapshot)
            {
                return cached.Settings;
            }

            var settings = ResolveSettings(_rule, subject, cacheKmsProps: false);
            _mutableSettings[subject] = new MutableRuleSettings(-1, snapshot, settings);
            return settings;
        }

        private EncryptionSettings CreateSettings(string subject) => ResolveSettings(_rule, subject);
    }

    private readonly record struct MutableRuleSettings(
        int Version,
        RuleParameterSnapshot Snapshot,
        EncryptionSettings Settings);

    private readonly record struct RuleParameterSnapshot(
        string? KekName,
        string? Algorithm,
        string? ExpiryDays,
        string? Deterministic,
        string? KmsType,
        string? KmsKeyId)
    {
        public static RuleParameterSnapshot Create(SchemaRule rule) => new(
            GetParameter(rule, KekNameParameter),
            GetParameter(rule, DekAlgorithmParameter),
            GetParameter(rule, DekExpiryDaysParameter),
            GetParameter(rule, DeterministicParameter),
            GetParameter(rule, KmsTypeParameter),
            GetParameter(rule, KmsKeyIdParameter));

        public bool Matches(Dictionary<string, string> parameters) =>
            Matches(parameters, KekNameParameter, KekName)
            && Matches(parameters, DekAlgorithmParameter, Algorithm)
            && Matches(parameters, DekExpiryDaysParameter, ExpiryDays)
            && Matches(parameters, DeterministicParameter, Deterministic)
            && Matches(parameters, KmsTypeParameter, KmsType)
            && Matches(parameters, KmsKeyIdParameter, KmsKeyId);

        private static string? GetParameter(SchemaRule rule, string name) =>
            rule.Parameters is { } parameters && parameters.TryGetValue(name, out var value)
                ? value
                : null;

        private static bool Matches(
            Dictionary<string, string> parameters,
            string name,
            string? expected)
        {
            if (!parameters.TryGetValue(name, out var current))
                return expected is null;

            return ReferenceEquals(current, expected);
        }
    }

    private sealed class TaggedJsonPlanCache
    {
        private readonly SchemaMetadata _metadata;
        private readonly ConditionalWeakTable<SchemaRule, TaggedJsonPlan> _fixedPlans = new();
        private readonly ConditionalWeakTable<SchemaRule, MutableTaggedJsonPlan> _mutablePlans = new();
        private readonly ConditionalWeakTable<SchemaRule, TaggedJsonPlan>.CreateValueCallback _createFixedPlan;
        private readonly ConditionalWeakTable<SchemaRule, MutableTaggedJsonPlan>.CreateValueCallback _createMutablePlan;

        public TaggedJsonPlanCache(SchemaMetadata metadata)
        {
            _metadata = metadata;
            _createFixedPlan = CreateFixedPlan;
            _createMutablePlan = CreateMutablePlan;
        }

        public TaggedJsonPlan Get(SchemaRule rule, bool mutableTags) => mutableTags
            ? _mutablePlans.GetValue(rule, _createMutablePlan).Get()
            : _fixedPlans.GetValue(rule, _createFixedPlan);

        private TaggedJsonPlan CreateFixedPlan(SchemaRule rule) =>
            CreateTaggedJsonPlan(_metadata, rule, mutableTags: false);

        private MutableTaggedJsonPlan CreateMutablePlan(SchemaRule rule) => new(_metadata, rule);
    }

    private sealed class MutableTaggedJsonPlan
    {
        private readonly object _gate = new();
        private readonly SchemaMetadata _metadata;
        private readonly SchemaRule _rule;
        private TaggedJsonPlan _plan;
        private int _metadataCount;
        private int _metadataVersion;
        private SortedDictionary<string, IReadOnlySet<string>>.Enumerator _sortedMetadataVersion;
        private MetadataTagsKind _metadataKind;

        public MutableTaggedJsonPlan(SchemaMetadata metadata, SchemaRule rule)
        {
            _metadata = metadata;
            _rule = rule;
            _plan = CreateTaggedJsonPlan(metadata, rule, mutableTags: true);
            CaptureVersions();
        }

        public TaggedJsonPlan Get()
        {
            if (IsCurrent())
                return _plan;

            lock (_gate)
            {
                if (!IsCurrent())
                {
                    _plan = CreateTaggedJsonPlan(_metadata, _rule, mutableTags: true);
                    CaptureVersions();
                }

                return _plan;
            }
        }

        private bool IsCurrent()
        {
            var tags = _metadata.Tags!;
            if (tags.Count != _metadataCount)
                return false;

            return _metadataKind switch
            {
                MetadataTagsKind.Dictionary =>
                    tags is Dictionary<string, IReadOnlySet<string>> dictionary
                    && Volatile.Read(ref GetMetadataVersion(dictionary)) == _metadataVersion,
                MetadataTagsKind.SortedDictionary =>
                    tags is SortedDictionary<string, IReadOnlySet<string>> && SortedMetadataIsCurrent(),
                MetadataTagsKind.Immutable =>
                    tags is FrozenDictionary<string, IReadOnlySet<string>>
                        or IImmutableDictionary<string, IReadOnlySet<string>>,
                _ => false
            };
        }

        private void CaptureVersions()
        {
            var tags = _metadata.Tags!;
            _metadataCount = tags.Count;
            switch (tags)
            {
                case Dictionary<string, IReadOnlySet<string>> dictionary:
                    _metadataKind = MetadataTagsKind.Dictionary;
                    _metadataVersion = Volatile.Read(ref GetMetadataVersion(dictionary));
                    break;
                case SortedDictionary<string, IReadOnlySet<string>> sortedDictionary:
                    _metadataKind = MetadataTagsKind.SortedDictionary;
                    _sortedMetadataVersion = sortedDictionary.GetEnumerator();
                    while (_sortedMetadataVersion.MoveNext())
                        _ = _sortedMetadataVersion.Current;
                    break;
                case FrozenDictionary<string, IReadOnlySet<string>> or
                    IImmutableDictionary<string, IReadOnlySet<string>>:
                    _metadataKind = MetadataTagsKind.Immutable;
                    break;
                default:
                    throw new SchemaRegistryRuleException(
                        $"Caller-owned CSFLE metadata tag map type '{tags.GetType().FullName}' cannot be tracked for mutation without a per-message scan. " +
                        "Use Dictionary<string, IReadOnlySet<string>>, SortedDictionary<string, IReadOnlySet<string>>, " +
                        "FrozenDictionary<string, IReadOnlySet<string>>, or IImmutableDictionary<string, IReadOnlySet<string>>.");
            }
        }

        private bool SortedMetadataIsCurrent()
        {
            try
            {
                var version = _sortedMetadataVersion;
                return !version.MoveNext();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    private enum MetadataTagsKind : byte
    {
        Dictionary,
        SortedDictionary,
        Immutable
    }

    private sealed class JsonPathNode
    {
        private Dictionary<PathSegmentHash, JsonPathNode>? _properties;
        private Dictionary<int, JsonPathNode>? _indices;

        public bool IsTarget { get; set; }

        public string? MutablePath { get; set; }

        public IReadOnlySet<string>? MutableTags { get; set; }

        public int MutableTagsVersion;

        public int MutableRuleTagsVersion;

        public byte[]? PropertyName { get; private init; }

        public JsonPathNode? HashCollision { get; private set; }

        public bool RefreshMutableTarget(
            SchemaRule rule,
            IReadOnlyDictionary<string, IReadOnlySet<string>> metadata)
        {
            if (!metadata.TryGetValue(MutablePath!, out var tags))
            {
                IsTarget = false;
                return false;
            }

            var tagsChanged = !ReferenceEquals(tags, MutableTags);
            if (tagsChanged)
                MutableTags = tags;

            var tagsVersion = GetSetVersion(tags);
            var ruleTagsVersion = GetSetVersion(rule.Tags!);
            if (!tagsChanged
                && tagsVersion == Volatile.Read(ref MutableTagsVersion)
                && ruleTagsVersion == Volatile.Read(ref MutableRuleTagsVersion))
            {
                return IsTarget;
            }

            IsTarget = TagsOverlap(tags, rule.Tags!);
            Volatile.Write(ref MutableTagsVersion, tagsVersion);
            Volatile.Write(ref MutableRuleTagsVersion, ruleTagsVersion);
            return IsTarget;
        }

        public JsonPathNode GetOrAddProperty(string property)
        {
            var utf8Name = Encoding.UTF8.GetBytes(property);
            var hash = PathSegmentHash.Create(utf8Name);
            _properties ??= new Dictionary<PathSegmentHash, JsonPathNode>();
            if (!_properties.TryGetValue(hash, out var node))
            {
                node = new JsonPathNode { PropertyName = utf8Name };
                _properties.Add(hash, node);
                return node;
            }

            while (!utf8Name.AsSpan().SequenceEqual(node.PropertyName))
            {
                if (node.HashCollision is null)
                {
                    node.HashCollision = new JsonPathNode { PropertyName = utf8Name };
                    return node.HashCollision;
                }

                node = node.HashCollision;
            }

            return node;
        }

        public JsonPathNode GetOrAddIndex(int index)
        {
            _indices ??= new Dictionary<int, JsonPathNode>();
            if (!_indices.TryGetValue(index, out var node))
            {
                node = new JsonPathNode();
                _indices.Add(index, node);
            }

            return node;
        }

        public JsonPathNode? FindProperty(ref Utf8JsonReader reader, CsfleWorkspace workspace)
        {
            if (_properties is null)
                return null;

            ReadOnlySpan<byte> propertyName;
            if (reader.ValueIsEscaped)
            {
                var scratch = workspace.GetTemporary(0, reader.ValueSpan.Length);
                propertyName = scratch[..reader.CopyString(scratch)];
            }
            else
            {
                propertyName = reader.ValueSpan;
            }

            if (!_properties.TryGetValue(PathSegmentHash.Create(propertyName), out var node))
                return null;

            while (!propertyName.SequenceEqual(node.PropertyName))
            {
                node = node.HashCollision;
                if (node is null)
                    return null;
            }

            return node;
        }

        public JsonPathNode? FindIndex(int index) =>
            _indices is not null && _indices.TryGetValue(index, out var node) ? node : null;
    }

    private readonly record struct PathSegmentHash(ulong First, ulong Second, int Length)
    {
        public static PathSegmentHash Create(ReadOnlySpan<byte> value)
        {
            const ulong firstOffset = 14695981039346656037UL;
            const ulong secondOffset = 7809847782465536322UL;
            const ulong firstPrime = 1099511628211UL;
            const ulong secondPrime = 14029467366897019727UL;
            var first = firstOffset;
            var second = secondOffset;
            foreach (var current in value)
            {
                first = (first ^ current) * firstPrime;
                second = (second ^ current) * secondPrime;
            }

            return new PathSegmentHash(first, second, value.Length);
        }
    }

    private sealed class GcmCipher : IDisposable
    {
        private const int NibbleCount = 32;
        private const int NibbleValues = 16;
        private readonly UInt128[] _multiplicationTable = new UInt128[NibbleCount * NibbleValues];
        private int _disposed;

        public GcmCipher(byte[] key)
        {
            Cipher = new BlockCipher(key);

            Span<byte> zero = stackalloc byte[16];
            Span<byte> hashBytes = stackalloc byte[16];
            Cipher.EncryptBlock(zero, hashBytes);
            var hash = BinaryPrimitives.ReadUInt128BigEndian(hashBytes);
            for (var position = 0; position < NibbleCount; position++)
            {
                var shift = (NibbleCount - 1 - position) * 4;
                var tableOffset = position * NibbleValues;
                for (var value = 1; value < NibbleValues; value++)
                    _multiplicationTable[tableOffset + value] = MultiplySlow((UInt128)value << shift, hash);
            }
        }

        public BlockCipher Cipher { get; }

        ~GcmCipher() => Dispose();

        public UInt128 Multiply(UInt128 value)
        {
            UInt128 result = 0;
            for (var position = 0; position < NibbleCount; position++)
            {
                var shift = (NibbleCount - 1 - position) * 4;
                var nibble = (int)((value >> shift) & 0x0F);
                result ^= _multiplicationTable[(position * NibbleValues) + nibble];
            }

            return result;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Cipher.Dispose();
            GC.SuppressFinalize(this);
        }

        private static UInt128 MultiplySlow(UInt128 value, UInt128 hash)
        {
            var reduction = (UInt128)0xE1 << 120;
            UInt128 result = 0;
            var current = hash;
            for (var bit = 127; bit >= 0; bit--)
            {
                if (((value >> bit) & 1) != 0)
                    result ^= current;
                current = (current & 1) == 0 ? current >> 1 : (current >> 1) ^ reduction;
            }

            return result;
        }
    }

    private sealed class BlockCipher : IDisposable
    {
        private readonly Aes _aes;
        private readonly ICryptoTransform _encryptor;
        private readonly byte[] _input = new byte[16];
        private readonly byte[] _output = new byte[16];

        public BlockCipher(ReadOnlySpan<byte> key)
        {
            _aes = Aes.Create();
            _aes.Mode = CipherMode.ECB;
            _aes.Padding = PaddingMode.None;
            _aes.Key = key.ToArray();
            _encryptor = _aes.CreateEncryptor();
        }

        public void EncryptBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            input.CopyTo(_input);
            _encryptor.TransformBlock(_input, 0, 16, _output, 0);
            _output.CopyTo(output);
        }

        public void Dispose()
        {
            _encryptor.Dispose();
            _aes.Dispose();
        }
    }

    private sealed class CsfleWorkspace
    {
        private const int MaxRetainedBufferSize = 1024 * 1024;
        private const int OutputBufferCount = 4;
        private const int CryptoFastCacheCapacity = 64;
        private readonly byte[]?[] _outputs = new byte[OutputBufferCount][];
        private readonly byte[]?[] _temporaries = new byte[2][];
        private readonly Dictionary<byte[], GcmCipher> _gcmCiphers = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<byte[], SivCiphers> _sivCiphers = new(ReferenceEqualityComparer.Instance);
        private readonly ConditionalWeakTable<byte[], GcmCipher> _overflowGcmCiphers = new();
        private readonly ConditionalWeakTable<byte[], SivCiphers> _overflowSivCiphers = new();
        private readonly JsonPathNode?[] _jsonNodes = new JsonPathNode?[65];
        private readonly int[] _jsonArrayIndices = new int[65];
        private int _nextOutput;

        public JsonPathNode?[] JsonNodes => _jsonNodes;

        public int[] JsonArrayIndices => _jsonArrayIndices;

        public PooledBufferBuilder CreateOutput(ReadOnlySpan<byte> input, int capacity)
        {
            for (var attempt = 0; attempt < OutputBufferCount; attempt++)
            {
                var slot = (_nextOutput + attempt) % OutputBufferCount;
                var buffer = _outputs[slot];
                if (buffer is not null && input.Overlaps(buffer))
                    continue;

                EnsureOutputCapacity(slot, capacity, written: 0);
                _nextOutput = (slot + 1) % OutputBufferCount;
                return new PooledBufferBuilder(this, slot);
            }

            throw new InvalidOperationException("No CSFLE output buffer is available.");
        }

        public Span<byte> GetTemporary(int slot, int length)
        {
            var buffer = _temporaries[slot];
            if (buffer is null || buffer.Length < length)
            {
                var replacement = ArrayPool<byte>.Shared.Rent(Math.Max(1, length));
                if (buffer is not null)
                    ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                _temporaries[slot] = replacement;
                buffer = replacement;
            }

            return buffer.AsSpan(0, length);
        }

        public GcmCipher GetGcmCipher(byte[] key)
        {
            if (_gcmCiphers.TryGetValue(key, out var cipher))
                return cipher;

            if (_gcmCiphers.Count < CryptoFastCacheCapacity)
            {
                cipher = new GcmCipher(key);
                _gcmCiphers.Add(key, cipher);
                return cipher;
            }

            return _overflowGcmCiphers.GetValue(key, static value => new GcmCipher(value));
        }

        public BlockCipher GetBlockCipher(byte[] key, int offset)
        {
            if (!_sivCiphers.TryGetValue(key, out var ciphers))
            {
                if (_sivCiphers.Count < CryptoFastCacheCapacity)
                {
                    ciphers = new SivCiphers(key);
                    _sivCiphers.Add(key, ciphers);
                    return ciphers.Get(offset);
                }

                ciphers = _overflowSivCiphers.GetValue(key, static value => new SivCiphers(value));
            }

            return ciphers.Get(offset);
        }

        public Span<byte> GetOutputSpan(int slot, int written, int sizeHint)
        {
            EnsureOutputCapacity(slot, checked(written + Math.Max(1, sizeHint)), written);
            return _outputs[slot]!.AsSpan(written);
        }

        public ReadOnlyMemory<byte> GetOutputMemory(int slot, int written)
        {
            var buffer = _outputs[slot]!;
            var memory = buffer.AsMemory(0, written);
            if (buffer.Length > MaxRetainedBufferSize)
                _outputs[slot] = null;
            return memory;
        }

        public void ReleaseOversizedTemporaries()
        {
            ReleaseOversizedTemporary(0);
            ReleaseOversizedTemporary(1);
        }

        private void EnsureOutputCapacity(int slot, int capacity, int written)
        {
            var buffer = _outputs[slot];
            if (buffer is not null && buffer.Length >= capacity)
                return;

            var replacement = ArrayPool<byte>.Shared.Rent(Math.Max(1, capacity));
            if (buffer is not null)
            {
                buffer.AsSpan(0, written).CopyTo(replacement);
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            _outputs[slot] = replacement;
        }

        private void ReleaseOversizedTemporary(int slot)
        {
            var buffer = _temporaries[slot];
            if (buffer is null || buffer.Length <= MaxRetainedBufferSize)
                return;

            _temporaries[slot] = null;
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private readonly ref struct CsfleWorkspaceOperation(CsfleWorkspace workspace)
    {
        public void Dispose() => workspace.ReleaseOversizedTemporaries();
    }

    private ref struct PooledBufferBuilder(CsfleWorkspace workspace, int slot)
    {
        private int _written;

        public readonly ReadOnlyMemory<byte> WrittenMemory => workspace.GetOutputMemory(slot, _written);

        public readonly Span<byte> GetSpan(int sizeHint) => workspace.GetOutputSpan(slot, _written, sizeHint);

        public void Advance(int count) => _written = checked(_written + count);

        public void Append(byte value)
        {
            GetSpan(1)[0] = value;
            Advance(1);
        }

        public void Append(scoped ReadOnlySpan<byte> value)
        {
            value.CopyTo(GetSpan(value.Length));
            Advance(value.Length);
        }
    }

    private sealed class SivCiphers : IDisposable
    {
        private int _disposed;

        public SivCiphers(byte[] key)
        {
            Mac = new BlockCipher(key.AsSpan(0, 32));
            Encryption = new BlockCipher(key.AsSpan(32, 32));
        }

        private BlockCipher Mac { get; }

        private BlockCipher Encryption { get; }

        ~SivCiphers() => Dispose();

        public BlockCipher Get(int offset) => offset == 0 ? Mac : Encryption;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Mac.Dispose();
                Encryption.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
