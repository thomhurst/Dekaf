using System.Reflection;

namespace Dekaf.Fuzzing;

public sealed record ProtocolReaderCorpusSeed(string Name, byte[] Data);

public static class ProtocolReaderCorpus
{
    private const string ResourcePrefix = "Dekaf.Fuzzing.Corpus.ProtocolReader.";

    public static IReadOnlyList<ProtocolReaderCorpusSeed> LoadEmbedded()
    {
        var assembly = typeof(ProtocolReaderCorpus).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => LoadSeed(assembly, name))
            .ToArray();
    }

    private static ProtocolReaderCorpusSeed LoadSeed(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded fuzz corpus resource '{resourceName}'");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new ProtocolReaderCorpusSeed(resourceName[ResourcePrefix.Length..], memory.ToArray());
    }
}
