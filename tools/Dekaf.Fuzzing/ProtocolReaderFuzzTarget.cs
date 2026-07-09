using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Fuzzing;

public static class ProtocolReaderFuzzTarget
{
    private const int MaxInputLength = 1024 * 1024;
    private static readonly int OperationCount = Enum.GetValues<ProtocolReaderOperation>().Length;

    public static void Run(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty || input.Length > MaxInputLength)
        {
            return;
        }

        var selector = input[0];
        var operation = (ProtocolReaderOperation)((selector & 0x7F) % OperationCount);

        try
        {
            if ((selector & 0x80) == 0)
            {
                RunContiguous(operation, input[1..]);
            }
            else
            {
                RunSegmented(operation, input[1..]);
            }
        }
        catch (InsufficientDataException)
        {
            // Truncated input is an expected parse outcome.
        }
        catch (MalformedProtocolDataException)
        {
            // Structurally invalid input is an expected parse outcome.
        }
    }

    private static void RunContiguous(ProtocolReaderOperation operation, ReadOnlySpan<byte> input)
    {
        var reader = new KafkaProtocolReader(input);
        Execute(operation, ref reader);
    }

    private static void RunSegmented(ProtocolReaderOperation operation, ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
        {
            RunContiguous(operation, input);
            return;
        }

        var buffer = input.ToArray();
        var split = buffer.Length / 2;
        var first = new BufferSegment(buffer.AsMemory(0, split));
        var last = first.Append(buffer.AsMemory(split));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var reader = new KafkaProtocolReader(sequence);
        Execute(operation, ref reader);
    }

    private static void Execute(ProtocolReaderOperation operation, ref KafkaProtocolReader reader)
    {
        switch (operation)
        {
            case ProtocolReaderOperation.ReadInt8:
                _ = reader.ReadInt8();
                break;
            case ProtocolReaderOperation.ReadUInt8:
                _ = reader.ReadUInt8();
                break;
            case ProtocolReaderOperation.ReadInt16:
                _ = reader.ReadInt16();
                break;
            case ProtocolReaderOperation.ReadInt32:
                _ = reader.ReadInt32();
                break;
            case ProtocolReaderOperation.ReadInt64:
                _ = reader.ReadInt64();
                break;
            case ProtocolReaderOperation.ReadUInt16:
                _ = reader.ReadUInt16();
                break;
            case ProtocolReaderOperation.ReadUInt32:
                _ = reader.ReadUInt32();
                break;
            case ProtocolReaderOperation.ReadUuid:
                _ = reader.ReadUuid();
                break;
            case ProtocolReaderOperation.ReadFloat64:
                _ = reader.ReadFloat64();
                break;
            case ProtocolReaderOperation.ReadVarInt:
                _ = reader.ReadVarInt();
                break;
            case ProtocolReaderOperation.ReadVarLong:
                _ = reader.ReadVarLong();
                break;
            case ProtocolReaderOperation.ReadUnsignedVarInt:
                _ = reader.ReadUnsignedVarInt();
                break;
            case ProtocolReaderOperation.ReadBoolean:
                _ = reader.ReadBoolean();
                break;
            case ProtocolReaderOperation.ReadString:
                _ = reader.ReadString();
                break;
            case ProtocolReaderOperation.ReadCompactString:
                _ = reader.ReadCompactString();
                break;
            case ProtocolReaderOperation.ReadCompactStringBytes:
                _ = reader.ReadCompactStringBytes();
                break;
            case ProtocolReaderOperation.ReadBytes:
                _ = reader.ReadBytes();
                break;
            case ProtocolReaderOperation.ReadCompactBytes:
                _ = reader.ReadCompactBytes();
                break;
            case ProtocolReaderOperation.ReadArray:
                _ = reader.ReadArray(static (ref KafkaProtocolReader itemReader) => itemReader.ReadUInt8());
                break;
            case ProtocolReaderOperation.ReadCompactArray:
                _ = reader.ReadCompactArray(static (ref KafkaProtocolReader itemReader) => itemReader.ReadUInt8());
                break;
            case ProtocolReaderOperation.ReadCompactNullableArray:
                _ = reader.ReadCompactNullableArray(static (ref KafkaProtocolReader itemReader) => itemReader.ReadUInt8());
                break;
            case ProtocolReaderOperation.SkipTaggedFields:
                reader.SkipTaggedFields();
                break;
            case ProtocolReaderOperation.ReadArrayWithState:
                _ = reader.ReadArray(ReadUInt8WithState, (byte)0);
                break;
            case ProtocolReaderOperation.ReadCompactArrayWithState:
                _ = reader.ReadCompactArray(ReadUInt8WithState, (byte)0);
                break;
            case ProtocolReaderOperation.ReadCompactNullableArrayWithState:
                _ = reader.ReadCompactNullableArray(ReadUInt8WithState, (byte)0);
                break;
            case ProtocolReaderOperation.ReadArrayIntoWithState:
                _ = reader.ReadArrayInto(new List<byte>(), ReadUInt8WithState, (byte)0);
                break;
            case ProtocolReaderOperation.ReadCompactArrayIntoWithState:
                _ = reader.ReadCompactArrayInto(new List<byte>(), ReadUInt8WithState, (byte)0);
                break;
            case ProtocolReaderOperation.ReadCompactNonNullableString:
                _ = reader.ReadCompactNonNullableString();
                break;
            case ProtocolReaderOperation.ReadRawBytes:
                _ = reader.ReadRawBytes(reader.ReadUInt8());
                break;
            case ProtocolReaderOperation.ReadMemorySlice:
                _ = reader.ReadMemorySlice(reader.ReadUInt8());
                break;
            case ProtocolReaderOperation.Skip:
                reader.Skip(reader.ReadUInt8());
                break;
            default:
                throw new InvalidOperationException($"Unknown protocol reader fuzz operation: {operation}");
        }
    }

    private static byte ReadUInt8WithState(ref KafkaProtocolReader reader, byte _)
    {
        return reader.ReadUInt8();
    }

    private enum ProtocolReaderOperation : byte
    {
        ReadInt8,
        ReadUInt8,
        ReadInt16,
        ReadInt32,
        ReadInt64,
        ReadUInt16,
        ReadUInt32,
        ReadUuid,
        ReadFloat64,
        ReadVarInt,
        ReadVarLong,
        ReadUnsignedVarInt,
        ReadBoolean,
        ReadString,
        ReadCompactString,
        ReadCompactStringBytes,
        ReadBytes,
        ReadCompactBytes,
        ReadArray,
        ReadCompactArray,
        ReadCompactNullableArray,
        SkipTaggedFields,
        ReadArrayWithState,
        ReadCompactArrayWithState,
        ReadCompactNullableArrayWithState,
        ReadArrayIntoWithState,
        ReadCompactArrayIntoWithState,
        ReadCompactNonNullableString,
        ReadRawBytes,
        ReadMemorySlice,
        Skip
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
