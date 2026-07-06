using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Avro.Specific;
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Security.Sasl;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using AvroSchema = Avro.Schema;
using DekafJsonSerializer = Dekaf.Serialization.Json.JsonSerializer<AotPayload>;

await AotSmoke.RunAsync();

internal static class AotSmoke
{
    private static readonly SerializationContext ValueContext = new()
    {
        Topic = "aot-topic",
        Component = SerializationComponent.Value
    };

    public static async Task RunAsync()
    {
        RunCompressionSmoke();
        RunJsonSmoke();
        await RunSchemaRegistrySmokeAsync();
        await RunSchemaRegistryPackageSmokeAsync();
        await RunCoreSmokeAsync();
    }

    private static async Task RunCoreSmokeAsync()
    {
        using var rsa = RSA.Create(2048);
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), CreateHttpClient());

        var token = await provider.GetTokenAsync(CancellationToken.None);
        Require(token.TokenValue == "access-token", "OAuth token value mismatch.");
        Require(token.PrincipalName == "aot-principal", "OAuth principal mismatch.");

        var headers = Headers.Create("aot", "ok");
        Require(headers.Count == 1, "Header smoke failed.");
        Require(headers[0].Key == "aot", "Header key mismatch.");
    }

    private static void RunCompressionSmoke()
    {
        ReadOnlyMemory<byte> payload = Encoding.UTF8.GetBytes("NativeAOT compression smoke payload");
        ICompressionCodec[] codecs =
        [
            new BrotliCompressionCodec(),
            new Lz4CompressionCodec(),
            new SnappyCompressionCodec(),
            new ZstdCompressionCodec()
        ];

        foreach (var codec in codecs)
        {
            var compressed = new ArrayBufferWriter<byte>();
            codec.Compress(new ReadOnlySequence<byte>(payload), compressed);

            var decompressed = new ArrayBufferWriter<byte>();
            codec.Decompress(new ReadOnlySequence<byte>(compressed.WrittenMemory), decompressed);

            Require(decompressed.WrittenSpan.SequenceEqual(payload.Span),
                $"{codec.Type} compression round-trip failed.");
        }
    }

    private static void RunJsonSmoke()
    {
        var serializer = new DekafJsonSerializer(AotJsonContext.Default.AotPayload);
        var payload = new AotPayload(7, "json");
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(payload, ref buffer, ValueContext);

        var roundTrip = serializer.Deserialize(buffer.WrittenMemory, ValueContext);
        Require(roundTrip == payload, "JSON serializer round-trip failed.");
    }

    private static async Task RunSchemaRegistrySmokeAsync()
    {
        using var registry = new InMemorySchemaRegistry();
        var payload = new AotPayload(8, "schema-registry");
        var buffer = new ArrayBufferWriter<byte>();

        await using var serializer = new JsonSchemaRegistrySerializer<AotPayload>(
            registry,
            AotPayloadJsonSchema,
            AotJsonContext.Default.AotPayload);
        await using var deserializer = new JsonSchemaRegistryDeserializer<AotPayload>(
            registry,
            AotJsonContext.Default.AotPayload);

        serializer.Serialize(payload, ref buffer, ValueContext);

        var roundTrip = deserializer.Deserialize(buffer.WrittenMemory, ValueContext);
        Require(roundTrip == payload, "Schema Registry JSON round-trip failed.");
    }

    private static async Task RunSchemaRegistryPackageSmokeAsync()
    {
        var avroSerializerConfig = new AvroSerializerConfig { AutoRegisterSchemas = false };
        var avroDeserializerConfig = new AvroDeserializerConfig();
        var protobufSerializerConfig = new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseSchemaReferences = false
        };
        var protobufDeserializerConfig = new ProtobufDeserializerConfig
        {
            SkipSchemaValidation = true
        };

        Require(!avroSerializerConfig.AutoRegisterSchemas, "Avro config smoke failed.");
        Require(avroDeserializerConfig.ReaderSchema is null, "Avro deserializer config smoke failed.");
        Require(!protobufSerializerConfig.AutoRegisterSchemas, "Protobuf config smoke failed.");
        Require(protobufDeserializerConfig.SkipSchemaValidation, "Protobuf deserializer config smoke failed.");

        using var avroRegistry = new InMemorySchemaRegistry();
        using var protobufRegistry = new InMemorySchemaRegistry();
        using var builderRegistry = new InMemorySchemaRegistry();

        await RunAvroSchemaRegistryPackageSmokeAsync(avroRegistry);
        await RunProtobufSchemaRegistryPackageSmokeAsync(protobufRegistry);
        RunSchemaRegistryBuilderExtensionSmoke(builderRegistry);
    }

    private static async Task RunAvroSchemaRegistryPackageSmokeAsync(InMemorySchemaRegistry registry)
    {
        await using var serializer = new AvroSchemaRegistrySerializer<AotAvroRecord>(registry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<AotAvroRecord>(registry);

        var payload = new AotAvroRecord { Id = 9, Name = "avro" };
        var buffer = new ArrayBufferWriter<byte>();

        await serializer.WarmupAsync(ValueContext.Topic, payload);
        serializer.Serialize(payload, ref buffer, ValueContext);

        var roundTrip = deserializer.Deserialize(buffer.WrittenMemory, ValueContext);
        Require(roundTrip.Id == payload.Id, "Avro Schema Registry ID mismatch.");
        Require(roundTrip.Name == payload.Name, "Avro Schema Registry name mismatch.");
    }

    private static async Task RunProtobufSchemaRegistryPackageSmokeAsync(InMemorySchemaRegistry registry)
    {
        await using var serializer = new ProtobufSchemaRegistrySerializer<AotProtobufMessage>(registry);
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<AotProtobufMessage>(registry);

        var payload = new AotProtobufMessage { Id = 10, Name = "protobuf", Value = 11.5 };
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(payload, ref buffer, ValueContext);

        var roundTrip = deserializer.Deserialize(buffer.WrittenMemory, ValueContext);
        Require(roundTrip.Id == payload.Id, "Protobuf Schema Registry ID mismatch.");
        Require(roundTrip.Name == payload.Name, "Protobuf Schema Registry name mismatch.");
        Require(Math.Abs(roundTrip.Value - payload.Value) < 0.0001, "Protobuf Schema Registry value mismatch.");
    }

    private static void RunSchemaRegistryBuilderExtensionSmoke(InMemorySchemaRegistry registry)
    {
        var avroProducer = Kafka.CreateProducer<string, AotAvroRecord>()
            .UseAvroSchemaRegistry(registry);
        var avroKeyProducer = Kafka.CreateProducer<AotAvroRecord, string>()
            .UseAvroSchemaRegistryKey(registry);
        var avroConsumer = Kafka.CreateConsumer<string, AotAvroRecord>()
            .UseAvroSchemaRegistry(registry);
        var avroKeyConsumer = Kafka.CreateConsumer<AotAvroRecord, string>()
            .UseAvroSchemaRegistryKey(registry);

        var protobufProducer = Kafka.CreateProducer<string, AotProtobufMessage>()
            .UseProtobufSchemaRegistry(registry);
        var protobufKeyValueProducer = Kafka.CreateProducer<AotProtobufMessage, AotProtobufMessage>()
            .UseProtobufSchemaRegistryForKeyAndValue(registry);
        var protobufConsumer = Kafka.CreateConsumer<string, AotProtobufMessage>()
            .UseProtobufSchemaRegistry(registry);
        var protobufKeyValueConsumer = Kafka.CreateConsumer<AotProtobufMessage, AotProtobufMessage>()
            .UseProtobufSchemaRegistryForKeyAndValue(registry);

        GC.KeepAlive(avroProducer);
        GC.KeepAlive(avroKeyProducer);
        GC.KeepAlive(avroConsumer);
        GC.KeepAlive(avroKeyConsumer);
        GC.KeepAlive(protobufProducer);
        GC.KeepAlive(protobufKeyValueProducer);
        GC.KeepAlive(protobufConsumer);
        GC.KeepAlive(protobufKeyValueConsumer);
    }

    private static OAuthBearerConfig CreateJwtBearerConfig(RSA rsa) =>
        new()
        {
            GrantType = OAuthBearerGrantType.JwtBearer,
            TokenEndpointUrl = "https://auth.example.test/token",
            ClientId = "aot-client",
            Scope = "kafka:produce",
            JwtBearer = new OAuthBearerJwtBearerOptions
            {
                TokenEndpoint = "https://auth.example.test/token",
                ClientId = "aot-client",
                PrivateKey = rsa,
                Audience = "kafka",
                Scopes = new List<string> { "kafka:produce" },
                AdditionalClaims = new Dictionary<string, object?>
                {
                    ["tenant"] = "aot",
                    ["enabled"] = true,
                    ["metadata"] = new Dictionary<string, object?>
                    {
                        ["region"] = "test"
                    }
                }
            }
        };

    private static HttpClient CreateHttpClient() => new(new TokenEndpointHandler())
    {
        BaseAddress = new Uri("https://auth.example.test/")
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Require(body.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", StringComparison.Ordinal),
                "JWT bearer grant type missing.");
            Require(body.Contains("assertion=", StringComparison.Ordinal), "JWT assertion missing.");

            const string json = """{"access_token":"access-token","expires_in":3600,"sub":"aot-principal"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private const string AotPayloadJsonSchema = """
        {
          "type": "object",
          "properties": {
            "id": { "type": "integer" },
            "name": { "type": "string" }
          },
          "required": [ "id", "name" ]
        }
        """;

    private sealed class InMemorySchemaRegistry : ISchemaRegistryClient, ISchemaRegistryCache
    {
        private readonly Dictionary<int, Schema> _schemasById = [];
        private readonly Dictionary<string, RegisteredSchema> _schemasBySubject = new(StringComparer.Ordinal);
        private int _nextId = 1;

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_schemasBySubject.TryGetValue(subject, out var existing))
                return Task.FromResult(existing.Id);

            var id = _nextId++;
            var registered = new RegisteredSchema
            {
                Id = id,
                Subject = subject,
                Version = 1,
                Schema = schema
            };
            _schemasBySubject[subject] = registered;
            _schemasById[id] = schema;

            return Task.FromResult(id);
        }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_schemasById[id]);
        }

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_schemasBySubject[subject]);
        }

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default)
            => RegisterSchemaAsync(subject, schema, cancellationToken);

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>(_schemasBySubject.Keys.ToArray());
        }

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<int>>([_schemasBySubject[subject].Version]);
        }

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<int>>(
                _schemasBySubject.Remove(subject, out var registered) ? [registered.Version] : []);
        }

        public bool TryGetCachedSchema(int id, out Schema schema)
        {
            if (_schemasById.TryGetValue(id, out var cached))
            {
                schema = cached;
                return true;
            }

            schema = null!;
            return false;
        }

        public void Dispose()
        {
        }
    }
}

internal sealed record AotPayload(int Id, string Name);

internal sealed class AotAvroRecord : ISpecificRecord
{
    public static readonly AvroSchema _SCHEMA = AvroSchema.Parse("""
        {
          "type": "record",
          "name": "AotAvroRecord",
          "fields": [
            { "name": "id", "type": "int" },
            { "name": "name", "type": "string" }
          ]
        }
        """);

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AvroSchema Schema => _SCHEMA;

    public AotAvroRecord()
    {
    }

    public object Get(int fieldPos) => fieldPos switch
    {
        0 => Id,
        1 => Name,
        _ => throw new ArgumentOutOfRangeException(nameof(fieldPos), fieldPos, "Invalid field position.")
    };

    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:
                Id = (int)fieldValue;
                break;
            case 1:
                Name = fieldValue.ToString() ?? string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fieldPos), fieldPos, "Invalid field position.");
        }
    }
}

internal sealed class AotProtobufMessage : IMessage<AotProtobufMessage>, IBufferMessage
{
    private static readonly MessageParser<AotProtobufMessage> ParserInstance = new(() => new AotProtobufMessage());
    private static readonly MessageDescriptor DescriptorInstance;
    private UnknownFieldSet? _unknownFields;

    static AotProtobufMessage()
    {
        var fileDescriptorProto = new FileDescriptorProto
        {
            Name = "aot_message.proto",
            Package = "dekaf.tests.aot",
            Syntax = "proto3"
        };

        fileDescriptorProto.MessageType.Add(new DescriptorProto
        {
            Name = "AotProtobufMessage",
            Field =
            {
                new FieldDescriptorProto
                {
                    Name = "id",
                    Number = 1,
                    Type = FieldDescriptorProto.Types.Type.Int32,
                    Label = FieldDescriptorProto.Types.Label.Optional
                },
                new FieldDescriptorProto
                {
                    Name = "name",
                    Number = 2,
                    Type = FieldDescriptorProto.Types.Type.String,
                    Label = FieldDescriptorProto.Types.Label.Optional
                },
                new FieldDescriptorProto
                {
                    Name = "value",
                    Number = 3,
                    Type = FieldDescriptorProto.Types.Type.Double,
                    Label = FieldDescriptorProto.Types.Label.Optional
                }
            }
        });

        DescriptorInstance = FileDescriptor.BuildFromByteStrings([fileDescriptorProto.ToByteString()])
            .Single()
            .MessageTypes[0];
    }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public static MessageParser<AotProtobufMessage> Parser => ParserInstance;
    public static MessageDescriptor Descriptor => DescriptorInstance;
    MessageDescriptor IMessage.Descriptor => DescriptorInstance;

    public int CalculateSize()
    {
        var size = 0;
        if (Id != 0)
            size += 1 + CodedOutputStream.ComputeInt32Size(Id);
        if (!string.IsNullOrEmpty(Name))
            size += 1 + CodedOutputStream.ComputeStringSize(Name);
        if (Value != 0)
            size += 1 + 8;
        return size;
    }

    public AotProtobufMessage Clone() => new()
    {
        Id = Id,
        Name = Name,
        Value = Value
    };

    public bool Equals(AotProtobufMessage? other)
        => other is not null && Id == other.Id && Name == other.Name && Value.Equals(other.Value);

    public override bool Equals(object? obj) => Equals(obj as AotProtobufMessage);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Value);

    public void MergeFrom(AotProtobufMessage message)
    {
        if (message.Id != 0)
            Id = message.Id;
        if (!string.IsNullOrEmpty(message.Name))
            Name = message.Name;
        if (message.Value != 0)
            Value = message.Value;
    }

    public void MergeFrom(CodedInputStream input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 8:
                    Id = input.ReadInt32();
                    break;
                case 18:
                    Name = input.ReadString();
                    break;
                case 25:
                    Value = input.ReadDouble();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    public void WriteTo(CodedOutputStream output)
    {
        if (Id != 0)
        {
            output.WriteRawTag(8);
            output.WriteInt32(Id);
        }

        if (!string.IsNullOrEmpty(Name))
        {
            output.WriteRawTag(18);
            output.WriteString(Name);
        }

        if (Value != 0)
        {
            output.WriteRawTag(25);
            output.WriteDouble(Value);
        }
    }

    void IBufferMessage.InternalMergeFrom(ref ParseContext input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 8:
                    Id = input.ReadInt32();
                    break;
                case 18:
                    Name = input.ReadString();
                    break;
                case 25:
                    Value = input.ReadDouble();
                    break;
                default:
                    _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
                    break;
            }
        }
    }

    void IBufferMessage.InternalWriteTo(ref WriteContext output)
    {
        if (Id != 0)
        {
            output.WriteRawTag(8);
            output.WriteInt32(Id);
        }

        if (!string.IsNullOrEmpty(Name))
        {
            output.WriteRawTag(18);
            output.WriteString(Name);
        }

        if (Value != 0)
        {
            output.WriteRawTag(25);
            output.WriteDouble(Value);
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AotPayload))]
internal sealed partial class AotJsonContext : JsonSerializerContext;
