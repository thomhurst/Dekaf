using System.Reflection;

namespace Dekaf.Fuzzing;

public sealed record ResponseParserCorpusSeed(string Name, byte[] Data);

public static class ResponseParserCorpus
{
    private const string ResourcePrefix = "Dekaf.Fuzzing.Corpus.Responses.";

    public static IReadOnlyList<ResponseParserCorpusSeed> LoadEmbedded()
    {
        var assembly = typeof(ResponseParserCorpus).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => LoadSeed(assembly, name))
            .ToArray();
    }

    private static ResponseParserCorpusSeed LoadSeed(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded response fuzz seed '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new ResponseParserCorpusSeed(resourceName[ResourcePrefix.Length..], memory.ToArray());
    }
}
