using System.Buffers;
using System.Collections.Concurrent;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class ProtobufSchemaReferenceTests
{
    [Test]
    public async Task Serialize_RegistersDescriptorGraphWithExactVersionsAndFidelity()
    {
        var registrations = new List<Registration>();
        var schemaRegistry = CreateRegistry(registrations);
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(schemaRegistry);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref destination,
            new SerializationContext { Topic = "graph", Component = SerializationComponent.Value });

        await schemaRegistry.Received().RegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            Arg.Is<CancellationToken>(static token => token.CanBeCanceled));
        await schemaRegistry.Received().LookupSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            true,
            false,
            Arg.Is<CancellationToken>(static token => token.CanBeCanceled));
        await schemaRegistry.Received().GetOrRegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<Schema>(),
            Arg.Is<CancellationToken>(static token => token.CanBeCanceled));

        await Assert.That(registrations.Select(static registration => registration.Subject).SequenceEqual([
                "shared/base.proto",
                "deps/left.proto",
                "deps/right.proto",
                "vendor.proto/common/user.proto",
                "beta/common.proto",
                "google/type/company_event.proto",
                "graph-value"
            ])).IsTrue();
        await Assert.That(registrations.Count(static registration => registration.Subject == "shared/base.proto"))
            .IsEqualTo(1);
        await Assert.That(registrations.Any(static registration =>
            registration.Subject == TimestampReflection.Descriptor.Name)).IsFalse();

        var rootRegistration = registrations[^1];
        await Assert.That(rootRegistration.Schema.SchemaString)
            .IsEqualTo(ReferenceGraphMessage.Descriptor.File.SerializedData.ToBase64());
        await Assert.That(rootRegistration.Schema.References!.Select(static reference => reference.Name).SequenceEqual([
                "deps/left.proto",
                "deps/right.proto",
                "vendor.proto/common/user.proto",
                "beta/common.proto",
                "google/type/company_event.proto"
            ])).IsTrue();

        var left = registrations.Single(static registration => registration.Subject == "deps/left.proto");
        await Assert.That(left.Schema.References!.Single().Version).IsEqualTo(3);
        var right = registrations.Single(static registration => registration.Subject == "deps/right.proto");
        await Assert.That(right.Schema.References!.Single().Version).IsEqualTo(3);

        var root = FileDescriptorProto.Parser.ParseFrom(
            Convert.FromBase64String(rootRegistration.Schema.SchemaString));
        var message = root.MessageType.Single(static descriptor => descriptor.Name == "ReferenceGraphMessage");
        await Assert.That(root.Syntax).IsEqualTo("proto3");
        await Assert.That(root.Options.JavaPackage).IsEqualTo("com.dekaf.graph");
        await Assert.That(root.Service.Single().Name).IsEqualTo("GraphService");
        await Assert.That(message.OneofDecl).Count().IsEqualTo(2);
        await Assert.That(message.Field.Single(static field => field.Name == "nickname").Proto3Optional).IsTrue();
        await Assert.That(message.NestedType.Single(static nested => nested.Name == "LabelsEntry").Options.MapEntry)
            .IsTrue();
        await Assert.That(message.NestedType).Contains(static nested => nested.Name == "Metadata");
        await Assert.That(message.EnumType).Contains(static nested => nested.Name == "Kind");
        await Assert.That(root.EnumType).Contains(static nested => nested.Name == "GraphState");
        await Assert.That(message.ReservedRange.Single().Start).IsEqualTo(10);
        await Assert.That(message.ReservedName).Contains("retired_field");

        var sharedRegistration = registrations.Single(static registration => registration.Subject == "shared/base.proto");
        var shared = FileDescriptorProto.Parser.ParseFrom(
            Convert.FromBase64String(sharedRegistration.Schema.SchemaString));
        await Assert.That(shared.Syntax).IsEqualTo("proto2");
        await Assert.That(shared.MessageType[0].Field[0].DefaultValue).IsEqualTo("legacy");
    }

    [Test]
    public async Task Serialize_QualifiedReferenceStrategy_TransformsImportPaths()
    {
        var registrations = new List<Registration>();
        var schemaRegistry = CreateRegistry(registrations);
        var config = new ProtobufSerializerConfig
        {
            ReferenceSubjectNameStrategy = ReferenceSubjectNameStrategy.Qualified
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(
            schemaRegistry,
            config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref destination,
            new SerializationContext { Topic = "graph", Component = SerializationComponent.Value });

        await Assert.That(registrations.Select(static registration => registration.Subject).ToArray())
            .Contains("shared.base");
        await Assert.That(registrations.Select(static registration => registration.Subject).ToArray())
            .Contains("vendor.proto.common.user");
        await Assert.That(registrations.Select(static registration => registration.Subject).ToArray())
            .Contains("beta.common");
    }

    [Test]
    public async Task Serialize_CustomReferenceStrategy_ReceivesTopicReferenceAndComponent()
    {
        var registrations = new List<Registration>();
        var schemaRegistry = CreateRegistry(registrations);
        var strategy = new RecordingReferenceStrategy();
        var config = new ProtobufSerializerConfig
        {
            CustomReferenceSubjectNameStrategy = strategy
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(
            schemaRegistry,
            config);
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref destination,
            new SerializationContext { Topic = "graph", Component = SerializationComponent.Key });

        await Assert.That(registrations.Select(static registration => registration.Subject).ToArray())
            .Contains("graph-key-shared/base.proto");
        await Assert.That(strategy.Calls).Contains(("graph", "shared/base.proto", true));
    }

    [Test]
    public async Task Serialize_RecordNameStrategy_ReusesResolvedReferenceGraphAcrossTopics()
    {
        var registrations = new List<Registration>();
        var schemaRegistry = CreateRegistry(registrations);
        var config = new ProtobufSerializerConfig { SubjectNameStrategy = SubjectNameStrategy.RecordName };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(
            schemaRegistry,
            config);

        var firstDestination = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref firstDestination,
            new SerializationContext { Topic = "first", Component = SerializationComponent.Value });
        var secondDestination = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref secondDestination,
            new SerializationContext { Topic = "second", Component = SerializationComponent.Value });

        await Assert.That(registrations.Count).IsEqualTo(7);
        await Assert.That(registrations.Count(static registration =>
            registration.Subject == ReferenceGraphMessage.Descriptor.FullName)).IsEqualTo(1);
    }

    [Test]
    public async Task Serialize_CustomReferenceStrategy_DoesNotReuseGraphAcrossTopics()
    {
        var registrations = new List<Registration>();
        var schemaRegistry = CreateRegistry(registrations);
        var strategy = new RecordingReferenceStrategy();
        var config = new ProtobufSerializerConfig
        {
            SubjectNameStrategy = SubjectNameStrategy.RecordName,
            CustomReferenceSubjectNameStrategy = strategy
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(
            schemaRegistry,
            config);

        var firstDestination = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref firstDestination,
            new SerializationContext { Topic = "first", Component = SerializationComponent.Value });
        var secondDestination = new ArrayBufferWriter<byte>();
        serializer.Serialize(
            new ReferenceGraphMessage(),
            ref secondDestination,
            new SerializationContext { Topic = "second", Component = SerializationComponent.Value });

        await Assert.That(strategy.Calls).Contains(("first", "shared/base.proto", false));
        await Assert.That(strategy.Calls).Contains(("second", "shared/base.proto", false));
        await Assert.That(registrations.Count(static registration =>
            registration.Subject == ReferenceGraphMessage.Descriptor.FullName)).IsEqualTo(2);
    }

    [Test]
    public async Task SerializerConfig_DefaultsMatchConfluentKnownTypeBehavior()
    {
        var config = new ProtobufSerializerConfig();

        await Assert.That(config.SkipKnownTypes).IsTrue();
        await Assert.That(config.ReferenceSubjectNameStrategy)
            .IsEqualTo(ReferenceSubjectNameStrategy.ReferenceName);
    }

    [Test]
    public async Task PrepareAsync_ConcurrentTopics_CoalescesSharedReferenceResolution()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var sharedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sharedRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrationCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var nextId = 10;
        schemaRegistry.RegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var subject = call.ArgAt<string>(0);
                registrationCounts.AddOrUpdate(subject, 1, static (_, count) => count + 1);
                if (subject == "shared/base.proto")
                {
                    sharedEntered.TrySetResult();
                    await sharedRelease.Task.ConfigureAwait(false);
                }

                return Interlocked.Increment(ref nextId);
            });
        schemaRegistry.LookupSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                true,
                false,
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new RegisteredSchema
            {
                Id = Volatile.Read(ref nextId),
                Subject = call.ArgAt<string>(0),
                Version = 1,
                Schema = call.ArgAt<Schema>(1)
            }));
        schemaRegistry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Interlocked.Increment(ref nextId)));
        await using var serializer = new ProtobufSchemaRegistrySerializer<ReferenceGraphMessage>(schemaRegistry);

        var first = serializer.PrepareAsync("graph-a", new ReferenceGraphMessage());
        await sharedEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = serializer.PrepareAsync("graph-b", new ReferenceGraphMessage());

        await Assert.That(registrationCounts["shared/base.proto"]).IsEqualTo(1);
        sharedRelease.TrySetResult();
        await Task.WhenAll(first.AsTask(), second.AsTask());

        await Assert.That(registrationCounts.Values.All(static count => count == 1)).IsTrue();
    }

    private static ISchemaRegistryClient CreateRegistry(List<Registration> registrations)
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        var nextId = 10;

        schemaRegistry.RegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var subject = call.ArgAt<string>(0);
                var schema = call.ArgAt<Schema>(1);
                registrations.Add(new Registration(subject, schema));
                return Task.FromResult(nextId++);
            });
        schemaRegistry.LookupSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                true,
                false,
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var subject = call.ArgAt<string>(0);
                var schema = call.ArgAt<Schema>(1);
                return Task.FromResult(new RegisteredSchema
                {
                    Id = nextId,
                    Subject = subject,
                    Version = subject == "shared/base.proto" ? 3 : 1,
                    Schema = schema
                });
            });
        schemaRegistry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                registrations.Add(new Registration(call.ArgAt<string>(0), call.ArgAt<Schema>(1)));
                return Task.FromResult(nextId++);
            });

        return schemaRegistry;
    }

    private sealed record Registration(string Subject, Schema Schema);

    private sealed class RecordingReferenceStrategy : IReferenceSubjectNameStrategy
    {
        internal List<(string Topic, string ReferenceName, bool IsKey)> Calls { get; } = [];

        public string GetSubjectName(string topic, string referenceName, bool isKey)
        {
            Calls.Add((topic, referenceName, isKey));
            return $"{topic}-{(isKey ? "key" : "value")}-{referenceName}";
        }
    }
}

public sealed class ReferenceGraphMessage : IMessage<ReferenceGraphMessage>, IBufferMessage
{
    private static readonly MessageParser<ReferenceGraphMessage> MessageParser = new(
        static () => new ReferenceGraphMessage());
    private UnknownFieldSet? _unknownFields;

    static ReferenceGraphMessage()
    {
        var shared = CreateSharedDescriptor();
        var left = CreateImportDescriptor("deps/left.proto", "graph.left", "Left", shared.Name);
        var right = CreateImportDescriptor("deps/right.proto", "graph.right", "Right", shared.Name);
        var vendor = CreateCommonDescriptor("vendor.proto/common/user.proto", "graph.vendor", "VendorCommon");
        var beta = CreateCommonDescriptor("beta/common.proto", "graph.beta", "BetaCommon");
        var googleUser = CreateCommonDescriptor(
            "google/type/company_event.proto",
            "google.type.company",
            "CompanyEvent");
        var root = CreateRootDescriptor(left.Name, right.Name, vendor.Name, beta.Name, googleUser.Name);
        var descriptorBytes = new List<ByteString>
        {
            shared.ToByteString(),
            left.ToByteString(),
            right.ToByteString(),
            vendor.ToByteString(),
            beta.ToByteString(),
            googleUser.ToByteString(),
            TimestampReflection.Descriptor.SerializedData,
            root.ToByteString()
        };

        var descriptors = FileDescriptor.BuildFromByteStrings(descriptorBytes);
        Descriptor = descriptors.Single(static file => file.Name == "graph/root.proto").MessageTypes[0];
    }

    public static MessageDescriptor Descriptor { get; }
    public static MessageParser<ReferenceGraphMessage> Parser => MessageParser;
    MessageDescriptor IMessage.Descriptor => Descriptor;

    public int CalculateSize() => 0;
    public ReferenceGraphMessage Clone() => new();
    public bool Equals(ReferenceGraphMessage? other) => other is not null;
    public override bool Equals(object? obj) => obj is ReferenceGraphMessage;
    public override int GetHashCode() => 0;
    public void MergeFrom(ReferenceGraphMessage message) { }

    public void MergeFrom(CodedInputStream input)
    {
        while (input.ReadTag() != 0)
            input.SkipLastField();
    }

    public void WriteTo(CodedOutputStream output) { }

    void IBufferMessage.InternalMergeFrom(ref ParseContext input)
    {
        while (input.ReadTag() != 0)
            _unknownFields = UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
    }

    void IBufferMessage.InternalWriteTo(ref WriteContext output) { }

    private static FileDescriptorProto CreateSharedDescriptor()
    {
        var descriptor = new FileDescriptorProto
        {
            Name = "shared/base.proto",
            Package = "graph.shared",
            Syntax = "proto2"
        };
        var message = new DescriptorProto { Name = "SharedData" };
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "legacy_name",
            Number = 1,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.String,
            DefaultValue = "legacy"
        });
        descriptor.MessageType.Add(message);
        return descriptor;
    }

    private static FileDescriptorProto CreateImportDescriptor(
        string name,
        string package,
        string messageName,
        string sharedName)
    {
        var descriptor = new FileDescriptorProto
        {
            Name = name,
            Package = package,
            Syntax = "proto3"
        };
        descriptor.Dependency.Add(sharedName);
        var message = new DescriptorProto { Name = messageName };
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "shared",
            Number = 1,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.Message,
            TypeName = ".graph.shared.SharedData"
        });
        descriptor.MessageType.Add(message);
        return descriptor;
    }

    private static FileDescriptorProto CreateCommonDescriptor(string name, string package, string messageName)
    {
        var descriptor = new FileDescriptorProto
        {
            Name = name,
            Package = package,
            Syntax = "proto3"
        };
        descriptor.MessageType.Add(new DescriptorProto { Name = messageName });
        return descriptor;
    }

    private static FileDescriptorProto CreateRootDescriptor(
        string leftName,
        string rightName,
        string vendorName,
        string betaName,
        string googleUserName)
    {
        var descriptor = new FileDescriptorProto
        {
            Name = "graph/root.proto",
            Package = "graph.root",
            Syntax = "proto3",
            Options = new Google.Protobuf.Reflection.FileOptions { JavaPackage = "com.dekaf.graph" }
        };
        descriptor.Dependency.Add(leftName);
        descriptor.Dependency.Add(rightName);
        descriptor.Dependency.Add(vendorName);
        descriptor.Dependency.Add(betaName);
        descriptor.Dependency.Add(googleUserName);
        descriptor.Dependency.Add(TimestampReflection.Descriptor.Name);

        var message = new DescriptorProto { Name = "ReferenceGraphMessage" };
        message.OneofDecl.Add(new OneofDescriptorProto { Name = "choice" });
        message.OneofDecl.Add(new OneofDescriptorProto { Name = "_nickname" });
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "choice_text",
            Number = 1,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.String,
            OneofIndex = 0
        });
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "choice_number",
            Number = 2,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.Int32,
            OneofIndex = 0
        });
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "nickname",
            Number = 3,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.String,
            OneofIndex = 1,
            Proto3Optional = true
        });
        var mapEntry = new DescriptorProto
        {
            Name = "LabelsEntry",
            Options = new MessageOptions { MapEntry = true }
        };
        mapEntry.Field.Add(new FieldDescriptorProto
        {
            Name = "key",
            Number = 1,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.String
        });
        mapEntry.Field.Add(new FieldDescriptorProto
        {
            Name = "value",
            Number = 2,
            Label = FieldDescriptorProto.Types.Label.Optional,
            Type = FieldDescriptorProto.Types.Type.Int32
        });
        message.NestedType.Add(mapEntry);
        message.NestedType.Add(new DescriptorProto { Name = "Metadata" });
        message.EnumType.Add(new EnumDescriptorProto
        {
            Name = "Kind",
            Value =
            {
                new EnumValueDescriptorProto { Name = "KIND_UNSPECIFIED", Number = 0 }
            }
        });
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "labels",
            Number = 4,
            Label = FieldDescriptorProto.Types.Label.Repeated,
            Type = FieldDescriptorProto.Types.Type.Message,
            TypeName = ".graph.root.ReferenceGraphMessage.LabelsEntry"
        });
        message.ReservedRange.Add(new DescriptorProto.Types.ReservedRange { Start = 10, End = 20 });
        message.ReservedName.Add("retired_field");
        descriptor.MessageType.Add(message);
        descriptor.EnumType.Add(new EnumDescriptorProto
        {
            Name = "GraphState",
            Value =
            {
                new EnumValueDescriptorProto { Name = "GRAPH_STATE_UNSPECIFIED", Number = 0 },
                new EnumValueDescriptorProto { Name = "GRAPH_STATE_READY", Number = 1 }
            }
        });
        descriptor.Service.Add(new ServiceDescriptorProto
        {
            Name = "GraphService",
            Method =
            {
                new MethodDescriptorProto
                {
                    Name = "Resolve",
                    InputType = ".graph.root.ReferenceGraphMessage",
                    OutputType = ".graph.root.ReferenceGraphMessage"
                }
            }
        });
        return descriptor;
    }
}
