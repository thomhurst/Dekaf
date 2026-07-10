using System.Reflection;

namespace Dekaf.Fuzzing;

public sealed record RecordBatchFuzzCorpusSeed(string Name, byte[] Data);

public static class RecordBatchFuzzCorpus
{
    private const string ResourcePrefix = "Dekaf.Fuzzing.Corpus.RecordBatch.";

    public static IReadOnlyList<RecordBatchFuzzCorpusSeed> LoadEmbedded()
    {
        var assembly = typeof(RecordBatchFuzzCorpus).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => LoadSeed(assembly, name))
            .ToArray();
    }

    private static RecordBatchFuzzCorpusSeed LoadSeed(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded fuzz corpus resource '{resourceName}'");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new RecordBatchFuzzCorpusSeed(resourceName[ResourcePrefix.Length..], memory.ToArray());
    }
}
