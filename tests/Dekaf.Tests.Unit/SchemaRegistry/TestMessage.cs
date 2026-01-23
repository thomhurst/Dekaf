using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dekaf.Tests.Unit.SchemaRegistry;

/// <summary>
/// A simple test protobuf message for unit testing.
/// This class is a manually-created protobuf message that would typically be generated from a .proto file.
/// </summary>
public sealed class TestMessage : IMessage<TestMessage>
{
    private static readonly MessageParser<TestMessage> _parser = new(() => new TestMessage());
    private static readonly MessageDescriptor _descriptor;
    private static readonly FileDescriptor _fileDescriptor;

    static TestMessage()
    {
        // Build the file descriptor for the test message
        var fileDescriptorProto = new Google.Protobuf.Reflection.FileDescriptorProto
        {
            Name = "test_message.proto",
            Package = "dekaf.tests",
            Syntax = "proto3"
        };

        // Add the message type
        var messageType = new Google.Protobuf.Reflection.DescriptorProto
        {
            Name = "TestMessage"
        };

        // Add fields
        messageType.Field.Add(new Google.Protobuf.Reflection.FieldDescriptorProto
        {
            Name = "id",
            Number = 1,
            Type = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type.Int32,
            Label = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Label.Optional
        });

        messageType.Field.Add(new Google.Protobuf.Reflection.FieldDescriptorProto
        {
            Name = "name",
            Number = 2,
            Type = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type.String,
            Label = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Label.Optional
        });

        messageType.Field.Add(new Google.Protobuf.Reflection.FieldDescriptorProto
        {
            Name = "value",
            Number = 3,
            Type = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type.Double,
            Label = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Label.Optional
        });

        fileDescriptorProto.MessageType.Add(messageType);

        // Build the descriptor
        _fileDescriptor = FileDescriptor.BuildFromByteStrings(
            [fileDescriptorProto.ToByteString()]).Single();
        _descriptor = _fileDescriptor.MessageTypes[0];
    }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }

    public static MessageParser<TestMessage> Parser => _parser;
    public static MessageDescriptor Descriptor => _descriptor;

    MessageDescriptor IMessage.Descriptor => _descriptor;

    public int CalculateSize()
    {
        var size = 0;

        if (Id != 0)
            size += 1 + CodedOutputStream.ComputeInt32Size(Id);

        if (!string.IsNullOrEmpty(Name))
            size += 1 + CodedOutputStream.ComputeStringSize(Name);

        if (Value != 0)
            size += 1 + 8; // Fixed64 for double

        return size;
    }

    public TestMessage Clone()
    {
        return new TestMessage
        {
            Id = Id,
            Name = Name,
            Value = Value
        };
    }

    public bool Equals(TestMessage? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && Name == other.Name && Value.Equals(other.Value);
    }

    public override bool Equals(object? obj) => Equals(obj as TestMessage);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Value);

    public void MergeFrom(TestMessage message)
    {
        if (message.Id != 0) Id = message.Id;
        if (!string.IsNullOrEmpty(message.Name)) Name = message.Name;
        if (message.Value != 0) Value = message.Value;
    }

    public void MergeFrom(CodedInputStream input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 8: // Field 1, type int32
                    Id = input.ReadInt32();
                    break;
                case 18: // Field 2, type string (length-delimited)
                    Name = input.ReadString();
                    break;
                case 25: // Field 3, type double (fixed64)
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
            output.WriteRawTag(8); // Field 1, wire type varint
            output.WriteInt32(Id);
        }

        if (!string.IsNullOrEmpty(Name))
        {
            output.WriteRawTag(18); // Field 2, wire type length-delimited
            output.WriteString(Name);
        }

        if (Value != 0)
        {
            output.WriteRawTag(25); // Field 3, wire type fixed64
            output.WriteDouble(Value);
        }
    }
}
