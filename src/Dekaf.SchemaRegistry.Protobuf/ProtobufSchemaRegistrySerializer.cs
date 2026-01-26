using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Protobuf serializer that integrates with Confluent Schema Registry.
/// Wire format: [magic byte (0x00)] [schema ID (4 bytes)] [varint array indexes] [protobuf binary]
/// </summary>
/// <typeparam name="T">The Protobuf message type to serialize.</typeparam>
public sealed class ProtobufSchemaRegistrySerializer<T> : ISerializer<T>, IAsyncDisposable
    where T : IMessage<T>
{
    private const byte MagicByte = 0x00;

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtobufSerializerConfig _config;
    private readonly bool _ownsClient;
    private readonly MessageDescriptor _descriptor;
    private readonly ConcurrentDictionary<string, int> _schemaIdCache = new();
    private readonly Schema _schema;
    private readonly int[] _messageIndexes;

    /// <summary>
    /// Creates a new Protobuf Schema Registry serializer.
    /// </summary>
    /// <param name="schemaRegistry">The Schema Registry client.</param>
    /// <param name="config">Optional serializer configuration.</param>
    /// <param name="ownsClient">Whether this serializer owns the client and should dispose it.</param>
    public ProtobufSchemaRegistrySerializer(
        ISchemaRegistryClient schemaRegistry,
        ProtobufSerializerConfig? config = null,
        bool ownsClient = false)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _config = config ?? new ProtobufSerializerConfig();
        _ownsClient = ownsClient;

        // Get the message descriptor from the type
        _descriptor = GetMessageDescriptor();

        // Generate the schema string from the descriptor
        _schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = GenerateSchemaString(_descriptor),
            References = _config.UseSchemaReferences ? GetSchemaReferences(_descriptor) : null
        };

        // Calculate the message index path
        _messageIndexes = CalculateMessageIndexes(_descriptor);
    }

    /// <inheritdoc />
    public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        ArgumentNullException.ThrowIfNull(value);

        var subject = GetSubjectName(context.Topic, context.Component == SerializationComponent.Key);
        var schemaId = GetSchemaIdSync(subject);

        // Serialize the protobuf message to bytes using ToByteArray() for compatibility
        // with both generated and hand-coded messages
        var protoBytes = value.ToByteArray();

        // Calculate the varint-encoded message indexes size
        var indexesSize = CalculateVarintArraySize(_messageIndexes);

        // Total size: magic byte + schema ID + indexes + message
        var totalSize = 1 + 4 + indexesSize + protoBytes.Length;
        var span = destination.GetSpan(totalSize);

        // Write magic byte
        span[0] = MagicByte;

        // Write schema ID (big-endian)
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), schemaId);

        // Write message indexes as varints
        var offset = 5;
        offset += WriteVarintArray(span.Slice(offset), _messageIndexes);

        // Write the protobuf message
        protoBytes.AsSpan().CopyTo(span.Slice(offset));

        destination.Advance(totalSize);
    }

    private int GetSchemaIdSync(string subject)
    {
        if (_schemaIdCache.TryGetValue(subject, out var cachedId))
            return cachedId;

        var task = _config.AutoRegisterSchemas
            ? _schemaRegistry.GetOrRegisterSchemaAsync(subject, _schema)
            : _schemaRegistry.GetSchemaBySubjectAsync(subject).ContinueWith(t => t.Result.Id);

        var id = task.GetAwaiter().GetResult();
        _schemaIdCache.TryAdd(subject, id);
        return id;
    }

    private string GetSubjectName(string topic, bool isKey)
    {
        var suffix = isKey ? "-key" : "-value";

        return _config.SubjectNameStrategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => _config.UseDeprecatedFormat
                ? _descriptor.FullName
                : _descriptor.FullName + suffix,
            SubjectNameStrategy.TopicRecordName => $"{topic}-{_descriptor.FullName}{suffix}",
            _ => topic + suffix
        };
    }

    private static MessageDescriptor GetMessageDescriptor()
    {
        // Get the Descriptor property from the message type
        var descriptorProperty = typeof(T).GetProperty("Descriptor",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (descriptorProperty == null)
            throw new InvalidOperationException($"Type {typeof(T).Name} does not have a static Descriptor property");

        var descriptor = descriptorProperty.GetValue(null) as MessageDescriptor;

        if (descriptor == null)
            throw new InvalidOperationException($"Could not get MessageDescriptor for type {typeof(T).Name}");

        return descriptor;
    }

    private static string GenerateSchemaString(MessageDescriptor descriptor)
    {
        // Generate proto schema from the file descriptor
        var fileDescriptor = descriptor.File;
        return GenerateProtoFromFileDescriptor(fileDescriptor);
    }

    private static string GenerateProtoFromFileDescriptor(FileDescriptor fileDescriptor)
    {
        var builder = new System.Text.StringBuilder();

        // Syntax
        builder.AppendLine("syntax = \"proto3\";");
        builder.AppendLine();

        // Package
        if (!string.IsNullOrEmpty(fileDescriptor.Package))
        {
            builder.AppendLine($"package {fileDescriptor.Package};");
            builder.AppendLine();
        }

        // Dependencies
        foreach (var dependency in fileDescriptor.Dependencies)
        {
            builder.AppendLine($"import \"{dependency.Name}\";");
        }

        if (fileDescriptor.Dependencies.Count > 0)
            builder.AppendLine();

        // Messages
        foreach (var messageType in fileDescriptor.MessageTypes)
        {
            GenerateMessageProto(builder, messageType, 0);
        }

        // Enums at file level
        foreach (var enumType in fileDescriptor.EnumTypes)
        {
            GenerateEnumProto(builder, enumType, 0);
        }

        return builder.ToString();
    }

    private static void GenerateMessageProto(System.Text.StringBuilder builder, MessageDescriptor message, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        builder.AppendLine($"{indentStr}message {message.Name} {{");

        // Nested enums
        foreach (var enumType in message.EnumTypes)
        {
            GenerateEnumProto(builder, enumType, indent + 1);
        }

        // Nested messages
        foreach (var nestedMessage in message.NestedTypes)
        {
            // Skip map entry types
            if (nestedMessage.GetOptions()?.MapEntry == true)
                continue;

            GenerateMessageProto(builder, nestedMessage, indent + 1);
        }

        // Fields
        foreach (var field in message.Fields.InFieldNumberOrder())
        {
            var fieldIndent = new string(' ', (indent + 1) * 2);
            var fieldType = GetProtoFieldType(field);
            var repeated = field.IsRepeated && !field.IsMap ? "repeated " : "";
            builder.AppendLine($"{fieldIndent}{repeated}{fieldType} {field.Name} = {field.FieldNumber};");
        }

        // Oneofs
        foreach (var oneof in message.Oneofs)
        {
            if (oneof.IsSynthetic) continue; // Skip synthetic oneofs (for proto3 optional)

            var oneofIndent = new string(' ', (indent + 1) * 2);
            builder.AppendLine($"{oneofIndent}oneof {oneof.Name} {{");
            foreach (var field in oneof.Fields)
            {
                var fieldIndent = new string(' ', (indent + 2) * 2);
                var fieldType = GetProtoFieldType(field);
                builder.AppendLine($"{fieldIndent}{fieldType} {field.Name} = {field.FieldNumber};");
            }
            builder.AppendLine($"{oneofIndent}}}");
        }

        builder.AppendLine($"{indentStr}}}");
        builder.AppendLine();
    }

    private static void GenerateEnumProto(System.Text.StringBuilder builder, EnumDescriptor enumType, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        builder.AppendLine($"{indentStr}enum {enumType.Name} {{");

        foreach (var value in enumType.Values)
        {
            var valueIndent = new string(' ', (indent + 1) * 2);
            builder.AppendLine($"{valueIndent}{value.Name} = {value.Number};");
        }

        builder.AppendLine($"{indentStr}}}");
        builder.AppendLine();
    }

    private static string GetProtoFieldType(FieldDescriptor field)
    {
        if (field.IsMap)
        {
            var keyType = GetProtoFieldType(field.MessageType.FindFieldByName("key"));
            var valueType = GetProtoFieldType(field.MessageType.FindFieldByName("value"));
            return $"map<{keyType}, {valueType}>";
        }

        return field.FieldType switch
        {
            FieldType.Double => "double",
            FieldType.Float => "float",
            FieldType.Int64 => "int64",
            FieldType.UInt64 => "uint64",
            FieldType.Int32 => "int32",
            FieldType.Fixed64 => "fixed64",
            FieldType.Fixed32 => "fixed32",
            FieldType.Bool => "bool",
            FieldType.String => "string",
            FieldType.Group => field.MessageType.FullName,
            FieldType.Message => field.MessageType.FullName,
            FieldType.Bytes => "bytes",
            FieldType.UInt32 => "uint32",
            FieldType.SFixed32 => "sfixed32",
            FieldType.SFixed64 => "sfixed64",
            FieldType.SInt32 => "sint32",
            FieldType.SInt64 => "sint64",
            FieldType.Enum => field.EnumType.FullName,
            _ => "bytes"
        };
    }

    private IReadOnlyList<SchemaReference>? GetSchemaReferences(MessageDescriptor descriptor)
    {
        var references = new List<SchemaReference>();

        foreach (var dependency in descriptor.File.Dependencies)
        {
            // Skip well-known types if configured
            if (_config.SkipKnownTypes && IsWellKnownType(dependency))
                continue;

            var refName = _config.ReferenceSubjectNameStrategy == ReferenceSubjectNameStrategy.ReferenceName
                ? dependency.Name
                : dependency.Package;

            references.Add(new SchemaReference
            {
                Name = dependency.Name,
                Subject = refName,
                Version = 1 // Assuming version 1 for dependencies
            });
        }

        return references.Count > 0 ? references : null;
    }

    private static bool IsWellKnownType(FileDescriptor fileDescriptor)
    {
        return fileDescriptor.Name.StartsWith("google/protobuf/", StringComparison.Ordinal);
    }

    private static int[] CalculateMessageIndexes(MessageDescriptor descriptor)
    {
        var indexes = new List<int>();
        CalculateMessageIndexesRecursive(descriptor, indexes);
        return [.. indexes];
    }

    private static void CalculateMessageIndexesRecursive(MessageDescriptor descriptor, List<int> indexes)
    {
        // Check if this is a nested message
        if (descriptor.ContainingType != null)
        {
            CalculateMessageIndexesRecursive(descriptor.ContainingType, indexes);
            var index = 0;
            foreach (var nested in descriptor.ContainingType.NestedTypes)
            {
                if (nested == descriptor)
                {
                    indexes.Add(index);
                    return;
                }
                index++;
            }
        }
        else
        {
            // Top-level message - find index in file
            var index = 0;
            foreach (var message in descriptor.File.MessageTypes)
            {
                if (message == descriptor)
                {
                    indexes.Add(index);
                    return;
                }
                index++;
            }
        }
    }

    private static int CalculateVarintArraySize(int[] values)
    {
        // First, write the count of elements as varint
        var size = CalculateVarintSize(values.Length);

        // Then write each value as varint
        foreach (var value in values)
        {
            size += CalculateVarintSize(value);
        }

        return size;
    }

    private static int CalculateVarintSize(int value)
    {
        var size = 1;
        var v = (uint)value;
        while (v >= 0x80)
        {
            size++;
            v >>= 7;
        }
        return size;
    }

    private static int WriteVarintArray(Span<byte> span, int[] values)
    {
        var written = 0;

        // Write the count first
        written += WriteVarint(span.Slice(written), values.Length);

        // Write each value
        foreach (var value in values)
        {
            written += WriteVarint(span.Slice(written), value);
        }

        return written;
    }

    private static int WriteVarint(Span<byte> span, int value)
    {
        var written = 0;
        var v = (uint)value;
        while (v >= 0x80)
        {
            span[written++] = (byte)(v | 0x80);
            v >>= 7;
        }
        span[written++] = (byte)v;
        return written;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
            _schemaRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
