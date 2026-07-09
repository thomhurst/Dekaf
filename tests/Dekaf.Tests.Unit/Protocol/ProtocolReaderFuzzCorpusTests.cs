using System.Buffers.Binary;
using Dekaf.Fuzzing;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public class ProtocolReaderFuzzCorpusTests
{
    private static readonly ProtocolReaderOperation[] RequiredProductionOperations =
    [
        ProtocolReaderOperation.ReadArrayWithState,
        ProtocolReaderOperation.ReadCompactArrayWithState,
        ProtocolReaderOperation.ReadCompactNullableArrayWithState,
        ProtocolReaderOperation.ReadArrayIntoWithState,
        ProtocolReaderOperation.ReadCompactArrayIntoWithState,
        ProtocolReaderOperation.ReadCompactNonNullableString,
        ProtocolReaderOperation.ReadRawBytes,
        ProtocolReaderOperation.ReadMemorySlice,
        ProtocolReaderOperation.Skip
    ];

    private static readonly IReadOnlyDictionary<string, ProtocolReaderOperation> ExpectedCorpusOperations =
        new Dictionary<string, ProtocolReaderOperation>(StringComparer.Ordinal)
        {
            ["corrupt-varint"] = ProtocolReaderOperation.ReadUnsignedVarInt,
            ["hostile-length"] = ProtocolReaderOperation.ReadBytes,
            ["truncated-fixed"] = ProtocolReaderOperation.ReadUuid,
            ["truncated-tagged-fields"] = ProtocolReaderOperation.SkipTaggedFields,
            ["valid-fixed"] = ProtocolReaderOperation.ReadUuid,
            ["valid-varint"] = ProtocolReaderOperation.ReadUnsignedVarInt
        };

    [Test]
    public async Task OperationSet_CoversProductionReaderPaths()
    {
        var operations = Enum.GetValues<ProtocolReaderOperation>();

        foreach (var requiredOperation in RequiredProductionOperations)
        {
            await Assert.That(operations).Contains(requiredOperation);
        }
    }

    [Test]
    public async Task OperationCount_MatchesOperationEnum()
    {
        await Assert.That(ProtocolReaderFuzzTarget.OperationCount)
            .IsEqualTo(Enum.GetValues<ProtocolReaderOperation>().Length);
    }

    [Test]
    public void OperationDispatch_HandlesEveryEnumValue()
    {
        var input = new byte[9];

        foreach (var operation in Enum.GetValues<ProtocolReaderOperation>())
        {
            input[0] = ProtocolReaderFuzzTarget.GetSelector(operation);
            ProtocolReaderFuzzTarget.Run(input);

            input[0] = ProtocolReaderFuzzTarget.GetSelector(operation, segmented: true);
            ProtocolReaderFuzzTarget.Run(input);
        }
    }

    [Test]
    public async Task CheckedInCorpus_SelectsNamedOperations()
    {
        foreach (var seed in ProtocolReaderCorpus.LoadEmbedded())
        {
            await Assert.That(ProtocolReaderFuzzTarget.GetOperation(seed.Data[0]))
                .IsEqualTo(ExpectedCorpusOperations[seed.Name]);
        }
    }

    [Test]
    public async Task LengthOperations_ReadInt32Counts()
    {
        var input = new byte[304];
        BinaryPrimitives.WriteInt32BigEndian(input, 256);

        foreach (var operation in new[]
                 {
                     ProtocolReaderOperation.ReadRawBytes,
                     ProtocolReaderOperation.ReadMemorySlice,
                     ProtocolReaderOperation.Skip
                 })
        {
            await Assert.That(ExecuteAndGetRemaining(operation, input)).IsEqualTo(44);
        }
    }

    [Test]
    public async Task CheckedInCorpus_ReplaysWithoutUnexpectedFailures()
    {
        var seeds = ProtocolReaderCorpus.LoadEmbedded();

        await Assert.That(seeds.Select(seed => seed.Name)).IsEquivalentTo([
            "corrupt-varint",
            "hostile-length",
            "truncated-fixed",
            "truncated-tagged-fields",
            "valid-fixed",
            "valid-varint"
        ]);

        foreach (var seed in seeds)
        {
            ProtocolReaderFuzzTarget.Run(seed.Data);

            var segmentedInput = seed.Data.ToArray();
            segmentedInput[0] |= 0x80;
            ProtocolReaderFuzzTarget.Run(segmentedInput);
        }
    }

    private static long ExecuteAndGetRemaining(ProtocolReaderOperation operation, byte[] input)
    {
        var reader = new KafkaProtocolReader(input);
        ProtocolReaderFuzzTarget.Execute(operation, ref reader);
        return reader.Remaining;
    }
}
