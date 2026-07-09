using System.Reflection;
using Dekaf.Fuzzing;

namespace Dekaf.Tests.Unit.Protocol;

public class ProtocolReaderFuzzCorpusTests
{
    private static readonly string[] RequiredProductionOperations =
    [
        "ReadArrayWithState",
        "ReadCompactArrayWithState",
        "ReadCompactNullableArrayWithState",
        "ReadArrayIntoWithState",
        "ReadCompactArrayIntoWithState",
        "ReadCompactNonNullableString",
        "ReadRawBytes",
        "ReadMemorySlice",
        "Skip"
    ];

    [Test]
    public async Task OperationSet_CoversProductionReaderPaths()
    {
        var operationNames = Enum.GetNames(GetOperationType());

        foreach (var requiredOperation in RequiredProductionOperations)
        {
            await Assert.That(operationNames).Contains(requiredOperation);
        }
    }

    [Test]
    public async Task OperationCount_MatchesOperationEnum()
    {
        var operationType = GetOperationType();
        var countField = typeof(ProtocolReaderFuzzTarget).GetField(
            "OperationCount",
            BindingFlags.NonPublic | BindingFlags.Static);

        await Assert.That(countField).IsNotNull();
        await Assert.That((int)countField!.GetValue(null)!).IsEqualTo(Enum.GetValues(operationType).Length);
    }

    [Test]
    public void OperationDispatch_HandlesEveryEnumValue()
    {
        var input = new byte[9];
        var operationCount = Enum.GetValues(GetOperationType()).Length;

        for (var selector = 0; selector < operationCount; selector++)
        {
            input[0] = (byte)selector;
            ProtocolReaderFuzzTarget.Run(input);

            input[0] = (byte)(selector | 0x80);
            ProtocolReaderFuzzTarget.Run(input);
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

    private static Type GetOperationType()
    {
        return typeof(ProtocolReaderFuzzTarget).GetNestedType(
                   "ProtocolReaderOperation",
                   BindingFlags.NonPublic)
               ?? throw new InvalidOperationException("Protocol reader fuzz operation enum was not found.");
    }
}
