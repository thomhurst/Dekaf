using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Fuzzing;

public static class ProtocolReaderFuzzTarget
{
    private const int MaxInputLength = 1024 * 1024;
    private const byte SegmentedInputMask = 0x80;
    private const byte ExtendedOperationMask = 0x40;
    private const int LegacyOperationCount = (int)ProtocolReaderOperation.SkipTaggedFields + 1;
    internal static readonly int OperationCount = Enum.GetValues<ProtocolReaderOperation>().Length;

    public static void Run(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty || input.Length > MaxInputLength)
        {
            return;
        }

        var selector = input[0];
        if (GetOperation(selector) is not { } operation)
        {
            return;
        }

        try
        {
            if ((selector & SegmentedInputMask) == 0)
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

    internal static ProtocolReaderOperation? GetOperation(byte selector)
    {
        var operationSelector = selector & ~SegmentedInputMask;
        // Preserve the original corpus's modulo-22 selectors. Appended operations use the
        // 0x40 lane so extending the enum cannot silently retarget existing seeds.
        var operationValue = (operationSelector & ExtendedOperationMask) == 0
            ? operationSelector % LegacyOperationCount
            : LegacyOperationCount + (operationSelector & ~ExtendedOperationMask);

        return operationValue < OperationCount
            ? (ProtocolReaderOperation)operationValue
            : null;
    }

    internal static byte GetSelector(ProtocolReaderOperation operation, bool segmented = false)
    {
        var operationValue = (int)operation;
        if ((uint)operationValue >= (uint)OperationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var selector = operationValue < LegacyOperationCount
            ? (byte)operationValue
            : (byte)(ExtendedOperationMask + operationValue - LegacyOperationCount);
        return segmented ? (byte)(selector | SegmentedInputMask) : selector;
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

    internal static void Execute(ProtocolReaderOperation operation, ref KafkaProtocolReader reader)
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
                _ = reader.ReadRawBytes(reader.ReadInt32());
                break;
            case ProtocolReaderOperation.ReadMemorySlice:
                _ = reader.ReadMemorySlice(reader.ReadInt32());
                break;
            case ProtocolReaderOperation.Skip:
                reader.Skip(reader.ReadInt32());
                break;
            default:
                throw new InvalidOperationException($"Unknown protocol reader fuzz operation: {operation}");
        }
    }

    private static byte ReadUInt8WithState(ref KafkaProtocolReader reader, byte _)
    {
        return reader.ReadUInt8();
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

internal enum ProtocolReaderOperation : byte
{
    ReadInt8 = 0,
    ReadUInt8 = 1,
    ReadInt16 = 2,
    ReadInt32 = 3,
    ReadInt64 = 4,
    ReadUInt16 = 5,
    ReadUInt32 = 6,
    ReadUuid = 7,
    ReadFloat64 = 8,
    ReadVarInt = 9,
    ReadVarLong = 10,
    ReadUnsignedVarInt = 11,
    ReadBoolean = 12,
    ReadString = 13,
    ReadCompactString = 14,
    ReadCompactStringBytes = 15,
    ReadBytes = 16,
    ReadCompactBytes = 17,
    ReadArray = 18,
    ReadCompactArray = 19,
    ReadCompactNullableArray = 20,
    SkipTaggedFields = 21,
    ReadArrayWithState = 22,
    ReadCompactArrayWithState = 23,
    ReadCompactNullableArrayWithState = 24,
    ReadArrayIntoWithState = 25,
    ReadCompactArrayIntoWithState = 26,
    ReadCompactNonNullableString = 27,
    ReadRawBytes = 28,
    ReadMemorySlice = 29,
    Skip = 30
}
