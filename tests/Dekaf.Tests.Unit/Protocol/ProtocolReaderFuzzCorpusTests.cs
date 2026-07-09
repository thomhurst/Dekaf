using Dekaf.Fuzzing;

namespace Dekaf.Tests.Unit.Protocol;

public class ProtocolReaderFuzzCorpusTests
{
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
}
