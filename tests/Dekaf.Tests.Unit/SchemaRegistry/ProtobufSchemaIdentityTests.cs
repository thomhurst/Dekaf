using System.Buffers;
using System.Buffers.Binary;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class ProtobufSchemaIdentityTests
{
    [Test]
    public async Task Serialize_Header_WritesGuidAndMessageIndexesOutsidePayload()
    {
        var registry = new MockSchemaRegistryClient();
        var schema = CreateSchema();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", schema);
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        var context = CreateContext(headers: new Headers());
        var config = new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            SchemaIdStrategy = SchemaIdSerializerStrategy.Header
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var message = new TestMessage { Id = 42, Name = "header", Value = 1.25 };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(message, ref destination, context);

        await Assert.That(destination.WrittenSpan.ToArray()).IsEquivalentTo(message.ToByteArray());
        var header = context.Headers!.GetFirst(SchemaIdentityHeaderNames.Value);
        await Assert.That(header).IsNotNull();
        var headerValue = header!.Value.Value;
        await Assert.That(headerValue.Length).IsEqualTo(18);
        await Assert.That(headerValue.Span[0]).IsEqualTo((byte)1);
        await Assert.That(new Guid(headerValue.Span[1..17], bigEndian: true))
            .IsEqualTo(Guid.Parse(registered.Guid!));
        await Assert.That(headerValue.Span[17]).IsEqualTo((byte)0);
        await Assert.That(schemaId).IsEqualTo(registered.Id);
    }

    [Test]
    public async Task Serialize_ExplicitId_WinsOverLatestAndRegistration()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var config = new ProtobufSerializerConfig
        {
            UseSchemaId = schemaId,
            UseLatestVersion = true,
            AutoRegisterSchemas = true
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new TestMessage { Id = 7 }, ref destination, CreateContext());

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(schemaId);
        await Assert.That(registry.GetOrRegisterSchemaCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serialize_ExplicitId_AcceptsRegistrySerializedDescriptorWithDefaultFileName()
    {
        var registry = new MockSchemaRegistryClient();
        var registryDescriptor = TestMessage.Descriptor.File.ToProto();
        registryDescriptor.Name = "default";
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = registryDescriptor.ToByteString().ToBase64()
        });
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(
            registry,
            new ProtobufSerializerConfig { UseSchemaId = schemaId });
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new TestMessage { Id = 7 }, ref destination, CreateContext());

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(schemaId);
    }

    [Test]
    public async Task Serialize_ExplicitId_RejectsDescriptorWithDifferentNonCanonicalFileName()
    {
        var registry = new MockSchemaRegistryClient();
        var registryDescriptor = TestMessage.Descriptor.File.ToProto();
        registryDescriptor.Name = "different.proto";
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = registryDescriptor.ToByteString().ToBase64()
        });
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(
            registry,
            new ProtobufSerializerConfig { UseSchemaId = schemaId });
        var destination = new ArrayBufferWriter<byte>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            serializer.Serialize(new TestMessage(), ref destination, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("does not match Protobuf message type");
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serialize_ExplicitId_RejectsNonProtobufSchema()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        });
        var config = new ProtobufSerializerConfig { UseSchemaId = schemaId };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            serializer.Serialize(new TestMessage(), ref destination, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("expected Protobuf");
    }

    [Test]
    public async Task Serialize_ExplicitId_RejectsDifferentProtobufDescriptor()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = Google.Protobuf.WellKnownTypes.StringValue.Descriptor.File.SerializedData.ToBase64()
        });
        var config = new ProtobufSerializerConfig { UseSchemaId = schemaId };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            serializer.Serialize(new TestMessage(), ref destination, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("does not match Protobuf message type");
        await Assert.That(exception.Message).Contains(TestMessage.Descriptor.FullName);
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serialize_ExplicitId_AcceptsMatchingProtobufReferenceGraph()
    {
        var registry = new MockSchemaRegistryClient();
        var registered = await RegisterReferenceGraphAsync(registry);
        var schemaId = await registry.RegisterSchemaAsync("identity-value", registered.Schema);
        var config = new ProtobufSerializerConfig { UseSchemaId = schemaId };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ReferenceGraphMessage(), ref destination, CreateContext());

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(schemaId);
    }

    [Test]
    public async Task Serialize_ExplicitId_NonConcreteClientRequestsSerializedReferenceGraph()
    {
        var backingRegistry = new MockSchemaRegistryClient();
        var registered = await RegisterReferenceGraphAsync(backingRegistry);
        var registry = Substitute.For<IFormattedSchemaRegistryClient>();
        registry.GetSchemaWithFormatAsync(
                registered.Id,
                "identity-value",
                "serialized",
                Arg.Any<CancellationToken>())
            .Returns(registered.Schema);
        registry.GetSchemaBySubjectWithFormatAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                true,
                "serialized",
                Arg.Any<CancellationToken>())
            .Returns(call => backingRegistry.GetSchemaBySubjectAsync(
                call.ArgAt<string>(0),
                call.ArgAt<string>(1),
                call.ArgAt<CancellationToken>(4)));
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(
            registry,
            new ProtobufSerializerConfig { UseSchemaId = registered.Id });
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ReferenceGraphMessage(), ref destination, CreateContext());

        await registry.Received(1).GetSchemaWithFormatAsync(
            registered.Id,
            "identity-value",
            "serialized",
            Arg.Any<CancellationToken>());
        await registry.Received().GetSchemaBySubjectWithFormatAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            true,
            "serialized",
            Arg.Any<CancellationToken>());
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(registered.Id);
    }

    [Test]
    public async Task Serialize_ExplicitId_WithoutSchemaReferences_AcceptsMatchingRootDescriptor()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = ReferenceGraphMessage.Descriptor.File.SerializedData.ToBase64()
        });
        var config = new ProtobufSerializerConfig
        {
            UseSchemaId = schemaId,
            UseSchemaReferences = false
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ReferenceGraphMessage(), ref destination, CreateContext());

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(schemaId);
    }

    [Test]
    public async Task Serialize_ExplicitId_AcceptsKnownTypeReferencesFromDifferentSkipSetting()
    {
        var registry = new MockSchemaRegistryClient();
        var descriptor = Google.Protobuf.WellKnownTypes.Api.Descriptor.File;
        var references = new SchemaReference[descriptor.Dependencies.Count];
        for (var index = 0; index < descriptor.Dependencies.Count; index++)
        {
            var dependency = descriptor.Dependencies[index];
            _ = await registry.RegisterSchemaAsync(dependency.Name, new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = dependency.SerializedData.ToBase64()
            });
            references[index] = new SchemaReference
            {
                Name = dependency.Name,
                Subject = dependency.Name,
                Version = 1
            };
        }

        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = descriptor.SerializedData.ToBase64(),
            References = references
        });
        var config = new ProtobufSerializerConfig { UseSchemaId = schemaId };
        await using var serializer = new ProtobufSchemaRegistrySerializer<Google.Protobuf.WellKnownTypes.Api>(
            registry,
            config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new Google.Protobuf.WellKnownTypes.Api(), ref destination, CreateContext());

        await Assert.That(descriptor.Dependencies.Count).IsGreaterThan(0);
        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]))
            .IsEqualTo(schemaId);
    }

    [Test]
    public async Task Serialize_ExplicitId_RejectsDifferentNestedProtobufReferenceVersion()
    {
        var registry = new MockSchemaRegistryClient();
        var registered = await RegisterReferenceGraphAsync(registry);
        var registeredLeft = await registry.GetSchemaBySubjectAsync("deps/left.proto", "1");
        _ = await registry.RegisterSchemaAsync("shared/base.proto", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = Google.Protobuf.WellKnownTypes.StringValue.Descriptor.File.SerializedData.ToBase64()
        });
        _ = await registry.RegisterSchemaAsync("deps/left.proto", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = registeredLeft.Schema.SchemaString,
            References = registeredLeft.Schema.References!
                .Select(static reference => new SchemaReference
                {
                    Name = reference.Name,
                    Subject = reference.Subject,
                    Version = reference.Name == "shared/base.proto" ? 2 : reference.Version
                })
                .ToArray()
        });
        var references = registered.Schema.References!
            .Select(static reference => new SchemaReference
            {
                Name = reference.Name,
                Subject = reference.Subject,
                Version = reference.Name == "deps/left.proto" ? 2 : reference.Version
            })
            .ToArray();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = registered.Schema.SchemaString,
            References = references
        });
        var config = new ProtobufSerializerConfig { UseSchemaId = schemaId };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            serializer.Serialize(new ReferenceGraphMessage(), ref destination, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("does not match Protobuf message type");
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serialize_Header_RequiresHeadersCollection()
    {
        var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var config = new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            SchemaIdStrategy = SchemaIdSerializerStrategy.Header
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(registry, config);
        var destination = new ArrayBufferWriter<byte>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            serializer.Serialize(new TestMessage(), ref destination, CreateContext());
            return Task.CompletedTask;
        });

        await Assert.That(exception!.Message).Contains("Headers collection");
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(SchemaIdDeserializerStrategy.Dual)]
    [Arguments(SchemaIdDeserializerStrategy.Header)]
    public async Task Deserialize_Header_RoundTripsGuidIdentity(SchemaIdDeserializerStrategy strategy)
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        var message = new TestMessage { Id = 91, Name = "guid", Value = 9.5 };
        var context = CreateContext(headers: CreateIdentityHeaders(Guid.Parse(registered.Guid!), [0]));
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = strategy };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(message.ToByteArray(), context);

        await Assert.That(result.Id).IsEqualTo(message.Id);
        await Assert.That(result.Name).IsEqualTo(message.Name);
        await Assert.That(schemaId).IsEqualTo(registered.Id);
        await Assert.That(registry.LastGetSchemaByGuidCancellationToken.CanBeCanceled).IsTrue();
    }

    [Test]
    public async Task Deserialize_Header_ValidationOnly_DoesNotRequireConsumerSubject()
    {
        var identityGuid = Guid.NewGuid();
        var schema = CreateSchema();
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetSchemaByGuidAsync(
                identityGuid.ToString("D"),
                null,
                Arg.Is<CancellationToken>(static token => token.CanBeCanceled))
            .Returns(schema);
        var message = new TestMessage { Id = 92, Name = "record-name-subject" };
        var headers = CreateIdentityHeaders(identityGuid, [0]);
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(message.ToByteArray(), CreateContext(headers: headers));

        await Assert.That(result.Id).IsEqualTo(message.Id);
        await Assert.That(result.Name).IsEqualTo(message.Name);
        await registry.DidNotReceive().LookupSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false, new byte[] { 4, 2, 0 })]
    [Arguments(true, new byte[] { 2, 1, 0 })]
    public async Task Deserialize_Header_PreservesEveryMessageIndexEncoding(
        bool deprecated,
        byte[] messageIndexes)
    {
        var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        var message = new TestMessage { Id = 17, Name = "nested" };
        var context = CreateContext(
            headers: CreateIdentityHeaders(Guid.Parse(registered.Guid!), messageIndexes));
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            UseDeprecatedFormat = deprecated
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(message.ToByteArray(), context);

        await Assert.That(result.Id).IsEqualTo(message.Id);
        await Assert.That(result.Name).IsEqualTo(message.Name);
    }

    [Test]
    public async Task Deserialize_Dual_FallsBackToPrefix()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var message = new TestMessage { Id = 22 };
        var payload = CreatePrefixPayload(schemaId, message);
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(payload, CreateContext(headers: new Headers()));

        await Assert.That(result.Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task Deserialize_Header_UseLatestVersion_PreservesMigrationContext()
    {
        var registry = new MockSchemaRegistryClient();
        var subject = "identity-value";
        var writerId = await registry.RegisterSchemaAsync(subject, CreateSchema());
        var readerId = await registry.RegisterSchemaAsync(subject, CreateSchema());
        var writer = await registry.GetSchemaBySubjectAsync(subject, "1");
        var replacement = new TestMessage { Id = 88, Name = "migrated" };
        var executor = new CapturingRuleExecutor(replacement.ToByteArray());
        var context = CreateContext(
            headers: CreateIdentityHeaders(Guid.Parse(writer.Guid!), [0]));
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            UseLatestVersion = true,
            RuleExecutor = executor
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(new TestMessage { Id = 1 }.ToByteArray(), context);

        await Assert.That(result.Id).IsEqualTo(replacement.Id);
        await Assert.That(result.Name).IsEqualTo(replacement.Name);
        await Assert.That(executor.Context).IsNotNull();
        await Assert.That(executor.Context!.SchemaId).IsEqualTo(readerId);
        await Assert.That(executor.Context.Subject).IsEqualTo(subject);
        await Assert.That(writerId).IsNotEqualTo(readerId);
    }

    [Test]
    public async Task Deserialize_Header_DistinctGuidsResolveSubjectsIndependently()
    {
        var firstSchema = CreateSchema();
        var secondSchema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = ReferenceGraphMessage.Descriptor.File.SerializedData.ToBase64()
        };
        var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync("identity-first", firstSchema);
        await registry.RegisterSchemaAsync("identity-second", secondSchema);
        var firstRegistered = await registry.GetSchemaBySubjectAsync("identity-first");
        var secondRegistered = await registry.GetSchemaBySubjectAsync("identity-second");
        var firstGuid = Guid.Parse(firstRegistered.Guid!);
        var secondGuid = Guid.Parse(secondRegistered.Guid!);
        var subjectStrategy = new SequencedSubjectNameStrategy("identity-first", "identity-second");
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            CustomSubjectNameStrategy = subjectStrategy,
            RuleExecutor = new CapturingRuleExecutor(new TestMessage { Id = 42 }.ToByteArray())
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        _ = deserializer.Deserialize(
            new TestMessage { Id = 1 }.ToByteArray(),
            CreateContext(headers: CreateIdentityHeaders(firstGuid, [0])));
        _ = deserializer.Deserialize(
            new TestMessage { Id = 2 }.ToByteArray(),
            CreateContext(headers: CreateIdentityHeaders(secondGuid, [0])));

        await Assert.That(subjectStrategy.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Deserialize_Prefix_IgnoresIdentityHeader()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var message = new TestMessage { Id = 23 };
        var headers = CreateIdentityHeaders(Guid.NewGuid(), [0]);
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Prefix };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var result = deserializer.Deserialize(CreatePrefixPayload(schemaId, message), CreateContext(headers: headers));

        await Assert.That(result.Id).IsEqualTo(message.Id);
    }

    [Test]
    public async Task Deserialize_Header_RejectsMissingIdentity()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Header };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Task.FromResult(deserializer.Deserialize(new TestMessage().ToByteArray(), CreateContext())));

        await Assert.That(exception!.Message).Contains("identity header is missing");
    }

    [Test]
    public async Task Deserialize_Header_RejectsNonProtobufGuidSchema()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        });
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        var headers = CreateIdentityHeaders(Guid.Parse(registered.Guid!), [0]);
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Header };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(deserializer.Deserialize(new TestMessage().ToByteArray(), CreateContext(headers: headers))));

        await Assert.That(exception!.Message).Contains("is not a Protobuf schema");
        await Assert.That(exception.Message).Contains(registered.Guid!);
        await Assert.That(schemaId).IsEqualTo(registered.Id);
    }

    [Test]
    public async Task Deserialize_Header_RejectsConflictingRegisteredGuid()
    {
        var identityGuid = Guid.NewGuid();
        var schema = CreateSchema();
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetSchemaByGuidAsync(
                identityGuid.ToString("D"),
                null,
                Arg.Is<CancellationToken>(static token => token.CanBeCanceled))
            .Returns(schema);
        registry.LookupSchemaAsync(
                "identity-value",
                schema,
                true,
                false,
                Arg.Is<CancellationToken>(static token => token.CanBeCanceled))
            .Returns(new RegisteredSchema
            {
                Id = 7,
                Guid = Guid.NewGuid().ToString("D"),
                Subject = "identity-value",
                Version = 1,
                Schema = schema
            });
        var headers = CreateIdentityHeaders(identityGuid, [0]);
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            RuleExecutor = new CapturingRuleExecutor(new TestMessage().ToByteArray())
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Task.FromResult(deserializer.Deserialize(new TestMessage().ToByteArray(), CreateContext(headers: headers))));

        await Assert.That(exception!.Message).Contains("conflicting GUID");
        await Assert.That(exception.Message).Contains("identity-value");
    }

    [Test]
    public async Task Deserialize_Header_RejectsTrailingMessageIndexData()
    {
        var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        var headers = CreateIdentityHeaders(Guid.Parse(registered.Guid!), [0, 42]);
        var config = new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Header };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Task.FromResult(deserializer.Deserialize(new TestMessage().ToByteArray(), CreateContext(headers: headers))));

        await Assert.That(exception!.Message).Contains("trailing message-index data");
    }

    [Test]
    public async Task Deserialize_LiveHeaderRouting_UsesReservedSlotWithManyHeaders()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            SkipSchemaValidation = true
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);
        var message = new TestMessage { Id = 31, Name = "routed" };
        var identity = CreateIdentityHeaders(Guid.NewGuid(), [0])[0];
        var headers = new Header[33];
        for (var index = 0; index < 32; index++)
            headers[index] = new Header($"noise-{index}", ReadOnlyMemory<byte>.Empty);
        headers[32] = identity;
        var plan = RecordHeaderRoutingPlan.Create<string, TestMessage>(null, deserializer)!;
        var lookup = new RecordHeaderRoutingLookup(
            plan,
            headers,
            headers.Length,
            firstIndex: 0,
            secondIndex: 33,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);

        var result = RecordHeaderDeserializer.Deserialize(
            deserializer,
            message.ToByteArray(),
            CreateContext(),
            in lookup);

        await Assert.That(result.Id).IsEqualTo(message.Id);
        await Assert.That(result.Name).IsEqualTo(message.Name);
    }

    [Test]
    public async Task Prepare_Header_SkipSchemaValidation_DoesNotResolveGuidSchema()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        var config = new ProtobufDeserializerConfig
        {
            SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
            SkipSchemaValidation = true
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, config);
        var message = new TestMessage { Id = 37, Name = "registry-unavailable" };
        var identityHeader = CreateIdentityHeaders(Guid.NewGuid(), [0])
            .GetFirst(SchemaIdentityHeaderNames.Value)!;
        var lookup = CreateRoutingLookup(deserializer, identityHeader);
        var context = CreateContext();
        var preparer = (IRecordHeaderAsyncDeserializerPreparer<TestMessage>)deserializer;

        var preparation = preparer.PrepareAsync(
            message.ToByteArray(),
            context,
            lookup,
            CancellationToken.None);
        var prepared = preparer.TryDeserialize(
            message.ToByteArray(),
            context,
            in lookup,
            out var result);

        await Assert.That(preparation.IsCompletedSuccessfully).IsTrue();
        await preparation;
        await Assert.That(prepared).IsTrue();
        await Assert.That(result.Id).IsEqualTo(message.Id);
        await Assert.That(result.Name).IsEqualTo(message.Name);
        await Assert.That(registry.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task RoutedPreparation_Dual_DeserializesPrefixButPreparesIdentityHeader()
    {
        var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync("identity-value", CreateSchema());
        var registered = await registry.GetSchemaBySubjectAsync("identity-value");
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(
            registry,
            new ProtobufDeserializerConfig { SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual });
        var preparer = (IRecordHeaderAsyncDeserializerPreparer<TestMessage>)deserializer;
        var prefixLookup = CreateRoutingLookup<TestMessage>(deserializer, identityHeader: null);
        var identityHeader = CreateIdentityHeaders(Guid.Parse(registered.Guid!), [0])
            .GetFirst(SchemaIdentityHeaderNames.Value)!;
        var headerLookup = CreateRoutingLookup(deserializer, identityHeader);

        var prefixPrepared = preparer.TryDeserialize(
            CreatePrefixPayload(schemaId, new TestMessage()),
            CreateContext(),
            in prefixLookup,
            out _);
        var headerPrepared = preparer.TryDeserialize(
            new TestMessage().ToByteArray(),
            CreateContext(),
            in headerLookup,
            out _);

        await Assert.That(prefixPrepared).IsTrue();
        await Assert.That(headerPrepared).IsFalse();
    }

    [Test]
    public async Task Constructors_RejectUnknownIdentityStrategies()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            new ProtobufSchemaRegistrySerializer<TestMessage>(registry, new ProtobufSerializerConfig
            {
                SchemaIdStrategy = (SchemaIdSerializerStrategy)99
            })));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            new ProtobufSchemaRegistryDeserializer<TestMessage>(registry, new ProtobufDeserializerConfig
            {
                SchemaIdStrategy = (SchemaIdDeserializerStrategy)99
            })));
    }

    [Test]
    public async Task Serializer_RejectsNegativeExplicitId()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            new ProtobufSchemaRegistrySerializer<TestMessage>(registry, new ProtobufSerializerConfig
            {
                UseSchemaId = -1
            })));

        await Assert.That(exception!.ParamName).IsEqualTo("useSchemaId");
    }

    private static Schema CreateSchema() => new()
    {
        SchemaType = SchemaType.Protobuf,
        SchemaString = TestMessage.Descriptor.File.SerializedData.ToBase64()
    };

    private static async Task<RegisteredSchema> RegisterReferenceGraphAsync(MockSchemaRegistryClient registry)
    {
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(registry);
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(new ReferenceGraphMessage(), ref destination, new SerializationContext
        {
            Topic = "graph",
            Component = SerializationComponent.Value
        });
        return await registry.GetSchemaBySubjectAsync("graph-value");
    }

    private static SerializationContext CreateContext(Headers? headers = null) => new()
    {
        Topic = "identity",
        Component = SerializationComponent.Value,
        Headers = headers
    };

    private static Headers CreateIdentityHeaders(Guid schemaGuid, ReadOnlySpan<byte> messageIndexes)
    {
        var frame = new byte[17 + messageIndexes.Length];
        frame[0] = 1;
        _ = schemaGuid.TryWriteBytes(frame.AsSpan(1, 16), bigEndian: true, out _);
        messageIndexes.CopyTo(frame.AsSpan(17));
        return new Headers().Add(SchemaIdentityHeaderNames.Value, frame);
    }

    private static RecordHeaderRoutingLookup CreateRoutingLookup<T>(
        IDeserializer<T> deserializer,
        Header? identityHeader)
    {
        Header[]? headers = identityHeader is null ? null : [identityHeader.Value];
        var plan = RecordHeaderRoutingPlan.Create<string, T>(null, deserializer)!;
        return new RecordHeaderRoutingLookup(
            plan,
            headers,
            headers?.Length ?? 0,
            firstIndex: 0,
            secondIndex: identityHeader is null ? 0 : 1,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
    }

    private static byte[] CreatePrefixPayload(int schemaId, TestMessage message)
    {
        var protobuf = message.ToByteArray();
        var payload = new byte[6 + protobuf.Length];
        payload[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        payload[5] = 0;
        protobuf.CopyTo(payload.AsSpan(6));
        return payload;
    }

    private sealed class CapturingRuleExecutor(byte[] replacement) : ISchemaRegistryRuleExecutor
    {
        internal SchemaRegistryRuleContext? Context { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            Context = SchemaRegistryRuleContextSnapshot.Capture(context);
            return replacement;
        }
    }

    private sealed class SequencedSubjectNameStrategy(params string[] subjects) : ISubjectNameStrategy
    {
        private int _callCount;

        internal int CallCount => Volatile.Read(ref _callCount);

        public string GetSubjectName(string topic, string? recordType, bool isKey)
        {
            var index = Interlocked.Increment(ref _callCount) - 1;
            return subjects[index];
        }
    }
}
