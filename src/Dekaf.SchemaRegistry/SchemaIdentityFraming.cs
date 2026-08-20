using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Selects how a serializer carries the Schema Registry identity.
/// </summary>
public enum SchemaIdSerializerStrategy
{
    /// <summary>Prefix the payload with the magic byte and global schema ID.</summary>
    Prefix = 0,

    /// <summary>Write the schema GUID to the Confluent Kafka header.</summary>
    Header = 1
}

/// <summary>
/// Selects where a deserializer reads the Schema Registry identity.
/// </summary>
public enum SchemaIdDeserializerStrategy
{
    /// <summary>Use the Confluent header when present; otherwise use the payload prefix.</summary>
    Dual,

    /// <summary>Read the identity only from the payload prefix.</summary>
    Prefix,

    /// <summary>Read the identity only from the Confluent Kafka header.</summary>
    Header
}

/// <summary>
/// Confluent-compatible Kafka header names for Schema Registry identities.
/// </summary>
public static class SchemaIdentityHeaderNames
{
    /// <summary>Header carrying a record-key schema GUID.</summary>
    public const string Key = "__key_schema_id";

    /// <summary>Header carrying a record-value schema GUID.</summary>
    public const string Value = "__value_schema_id";
}

/// <summary>
/// A Schema Registry global ID, GUID, or paired ID and GUID.
/// </summary>
public readonly record struct SchemaIdentity
{
    /// <summary>Creates an integer schema identity.</summary>
    public SchemaIdentity(int schemaId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(schemaId);
        SchemaId = schemaId;
        SchemaGuid = null;
    }

    /// <summary>Creates a GUID schema identity.</summary>
    public SchemaIdentity(Guid schemaGuid)
    {
        if (schemaGuid == Guid.Empty)
            throw new ArgumentException("The schema GUID cannot be empty.", nameof(schemaGuid));

        SchemaId = null;
        SchemaGuid = schemaGuid;
    }

    /// <summary>Creates a paired integer and GUID schema identity.</summary>
    public SchemaIdentity(int schemaId, Guid schemaGuid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(schemaId);
        if (schemaGuid == Guid.Empty)
            throw new ArgumentException("The schema GUID cannot be empty.", nameof(schemaGuid));

        SchemaId = schemaId;
        SchemaGuid = schemaGuid;
    }

    /// <summary>Global integer schema ID, when available.</summary>
    public int? SchemaId { get; }

    /// <summary>Schema GUID, when available.</summary>
    public Guid? SchemaGuid { get; }
}

internal static class SchemaIdentityFraming
{
    internal const byte SchemaIdMagicByte = 0;
    internal const byte SchemaGuidMagicByte = 1;
    internal const int SchemaIdFrameSize = 5;
    internal const int SchemaGuidFrameSize = 17;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteSchemaId(Span<byte> destination, int schemaId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(schemaId);
        if (destination.Length < SchemaIdFrameSize)
            throw new ArgumentException("The destination is too short for a schema ID frame.", nameof(destination));

        destination[0] = SchemaIdMagicByte;
        BinaryPrimitives.WriteInt32BigEndian(destination[1..SchemaIdFrameSize], schemaId);
        return SchemaIdFrameSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteSchemaGuid(Span<byte> destination, Guid schemaGuid)
    {
        if (schemaGuid == Guid.Empty)
            throw new ArgumentException("The schema GUID cannot be empty.", nameof(schemaGuid));
        if (destination.Length < SchemaGuidFrameSize)
            throw new ArgumentException("The destination is too short for a schema GUID frame.", nameof(destination));

        destination[0] = SchemaGuidMagicByte;
        _ = schemaGuid.TryWriteBytes(destination[1..SchemaGuidFrameSize], bigEndian: true, out _);
        return SchemaGuidFrameSize;
    }

    internal static byte[] CreateSchemaGuidFrame(Guid schemaGuid)
    {
        var frame = GC.AllocateUninitializedArray<byte>(SchemaGuidFrameSize);
        WriteSchemaGuid(frame, schemaGuid);
        return frame;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Header CreateSchemaGuidHeader(
        SerializationComponent component,
        ReadOnlyMemory<byte> encodedSchemaGuid)
        => new(GetHeaderName(component), encodedSchemaGuid);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SchemaIdentity ReadPrefix(ReadOnlySpan<byte> payload, out int payloadOffset)
    {
        if (payload.Length < SchemaIdFrameSize)
            ThrowTruncatedSchemaIdPrefix();
        if (payload[0] != SchemaIdMagicByte)
            ThrowUnknownPrefixMagicByte(payload[0]);

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(payload[1..]);
        if (schemaId < 0)
            ThrowNegativeSchemaId(schemaId);

        payloadOffset = SchemaIdFrameSize;
        return new SchemaIdentity(schemaId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SchemaIdentity ReadHeader(
        in Header header,
        out ReadOnlyMemory<byte> trailingHeaderData)
    {
        if (header.IsValueNull)
            throw new InvalidDataException("The Schema Registry identity header cannot be null.");

        return ReadHeaderValue(header.Value, out trailingHeaderData);
    }

    // The record parser retains the last matching reserved header so framing never rescans the collection.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SchemaIdentity Read(
        ReadOnlySpan<byte> payload,
        Header? identityHeader,
        SchemaIdDeserializerStrategy strategy,
        out int payloadOffset,
        out ReadOnlyMemory<byte> trailingHeaderData)
    {
        switch (strategy)
        {
            case SchemaIdDeserializerStrategy.Prefix:
                trailingHeaderData = default;
                return ReadPrefix(payload, out payloadOffset);
            case SchemaIdDeserializerStrategy.Header:
                payloadOffset = 0;
                if (identityHeader is not { } requiredHeader)
                    throw new InvalidDataException("The required Schema Registry identity header is missing.");
                return ReadHeader(in requiredHeader, out trailingHeaderData);
            case SchemaIdDeserializerStrategy.Dual:
                if (identityHeader is { } optionalHeader)
                {
                    payloadOffset = 0;
                    return ReadHeader(in optionalHeader, out trailingHeaderData);
                }

                trailingHeaderData = default;
                return ReadPrefix(payload, out payloadOffset);
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown schema identity strategy.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SchemaIdentity ReadHeaderValue(
        ReadOnlyMemory<byte> value,
        out ReadOnlyMemory<byte> trailingHeaderData)
    {
        var span = value.Span;
        if (span.Length >= SchemaGuidFrameSize && span[0] == SchemaGuidMagicByte)
        {
            var schemaGuid = new Guid(span[1..SchemaGuidFrameSize], bigEndian: true);
            if (schemaGuid != Guid.Empty)
            {
                trailingHeaderData = value[SchemaGuidFrameSize..];
                return new SchemaIdentity(schemaGuid);
            }
        }

        return ReadHeaderValueSlow(value, out trailingHeaderData);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowTruncatedSchemaIdPrefix() =>
        throw new InvalidOperationException("Message too short to contain Schema Registry wire format.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnknownPrefixMagicByte(byte magicByte) =>
        throw new InvalidOperationException(
            $"Unknown magic byte: {magicByte}. Expected Schema Registry format (0x00).");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNegativeSchemaId(int schemaId) =>
        throw new InvalidDataException($"The Schema Registry ID cannot be negative: {schemaId}.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SchemaIdentity ReadHeaderValueSlow(
        ReadOnlyMemory<byte> value,
        out ReadOnlyMemory<byte> trailingHeaderData)
    {
        var source = value.Span;
        if (source.IsEmpty)
            throw new InvalidDataException("The Schema Registry identity is empty.");
        if (source[0] != SchemaGuidMagicByte)
            throw new InvalidDataException($"Unknown Schema Registry header magic byte: {source[0]}.");

        var parsed = ReadSchemaGuid(source);
        trailingHeaderData = value[parsed.BytesConsumed..];
        return parsed.Identity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IdentityFrame ReadSchemaGuid(ReadOnlySpan<byte> source)
    {
        if (source.Length < SchemaGuidFrameSize)
            throw new InvalidDataException("The Schema Registry GUID frame is truncated.");

        var schemaGuid = new Guid(source[1..SchemaGuidFrameSize], bigEndian: true);
        if (schemaGuid == Guid.Empty)
            throw new InvalidDataException("The Schema Registry GUID cannot be empty.");

        return new IdentityFrame(new SchemaIdentity(schemaGuid), SchemaGuidFrameSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetHeaderName(SerializationComponent component) => component switch
    {
        SerializationComponent.Key => SchemaIdentityHeaderNames.Key,
        SerializationComponent.Value => SchemaIdentityHeaderNames.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown serialization component.")
    };

    private readonly record struct IdentityFrame(SchemaIdentity Identity, int BytesConsumed);
}

internal enum SchemaSelectionMode
{
    AutoRegister,
    Lookup,
    Latest,
    ExplicitId
}

internal static class SchemaRegistrySerializerConfigValidator
{
    internal static SchemaSelectionMode ValidateAndResolve(
        int? useSchemaId,
        bool useLatestVersion,
        bool autoRegisterSchemas)
    {
        if (useSchemaId < 0)
            throw new ArgumentOutOfRangeException(nameof(useSchemaId), "The explicit schema ID cannot be negative.");
        if (useSchemaId.HasValue)
            return SchemaSelectionMode.ExplicitId;
        if (useLatestVersion)
            return SchemaSelectionMode.Latest;

        return autoRegisterSchemas ? SchemaSelectionMode.AutoRegister : SchemaSelectionMode.Lookup;
    }
}

internal static class SchemaIdentityResolution
{
    internal static async Task<SubjectSchemaIdCache.SubjectSchemaIdCacheValue> CreateSerializerValueAsync(
        ISchemaRegistryClient schemaRegistry,
        string subject,
        int schemaId,
        Schema schema,
        SchemaIdSerializerStrategy strategy,
        bool normalizeSchemas,
        RegisteredSchema? registeredSchema,
        CancellationToken cancellationToken)
    {
        if (strategy == SchemaIdSerializerStrategy.Prefix)
            return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(schemaId, schema);

        registeredSchema ??= await schemaRegistry.LookupSchemaAsync(
                subject,
                schema,
                ignoreDeletedSchemas: true,
                normalize: normalizeSchemas,
                cancellationToken)
            .ConfigureAwait(false);
        if (registeredSchema.Id != schemaId)
        {
            throw new InvalidDataException(
                $"Schema Registry resolved ID {registeredSchema.Id}; expected schema ID {schemaId}.");
        }
        if (!Guid.TryParse(registeredSchema.Guid, out var schemaGuid) || schemaGuid == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Schema Registry did not return a valid GUID for schema ID {schemaId}.");
        }

        return new SubjectSchemaIdCache.SubjectSchemaIdCacheValue(
            schemaId,
            schema,
            SchemaIdentityFraming.CreateSchemaGuidFrame(schemaGuid));
    }
}

internal static class BoundedSchemaIdentityCache
{
    internal static void RecordSuccessfulResolution<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> cache,
        ConcurrentQueue<KeyValuePair<TKey, TValue>> evictionQueue,
        TKey key,
        ref int cachedCount,
        int maxCachedEntries)
        where TKey : notnull
    {
        if (!cache.TryGetValue(key, out var entry))
            return;

        Interlocked.Increment(ref cachedCount);
        evictionQueue.Enqueue(new KeyValuePair<TKey, TValue>(key, entry));
        while (Volatile.Read(ref cachedCount) > maxCachedEntries
               && evictionQueue.TryDequeue(out var oldest))
        {
            if (((ICollection<KeyValuePair<TKey, TValue>>)cache).Remove(oldest))
                Interlocked.Decrement(ref cachedCount);
        }
    }
}

internal static class SchemaIdentitySerialization
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetPayloadOffset(SchemaIdSerializerStrategy strategy) => strategy switch
    {
        SchemaIdSerializerStrategy.Prefix => SchemaIdentityFraming.SchemaIdFrameSize,
        SchemaIdSerializerStrategy.Header => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown schema identity strategy.")
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteIdentity(
        Span<byte> destination,
        SerializationContext context,
        in SubjectSchemaIdCache.SubjectSchemaIdCacheEntry schemaEntry,
        SchemaIdSerializerStrategy strategy)
    {
        if (strategy == SchemaIdSerializerStrategy.Prefix)
        {
            SchemaIdentityFraming.WriteSchemaId(destination, schemaEntry.SchemaId);
            return;
        }

        if (context.Headers is not { } headers)
        {
            throw new InvalidOperationException(
                "Header schema identity framing requires a record Headers collection.");
        }
        var encodedSchemaGuid = schemaEntry.SchemaGuidFrame;
        if (encodedSchemaGuid is null)
        {
            throw new InvalidDataException(
                $"Schema Registry GUID framing is unavailable for schema ID {schemaEntry.SchemaId}.");
        }

        headers.Add(SchemaIdentityFraming.CreateSchemaGuidHeader(
            context.Component,
            encodedSchemaGuid));
    }
}
