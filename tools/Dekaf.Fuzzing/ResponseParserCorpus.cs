using System.Reflection;

namespace Dekaf.Fuzzing;

public sealed record ResponseParserCorpusSeed(string Name, byte[] Data);

public static class ResponseParserCorpus
{
    private const string ResourcePrefix = "Dekaf.Fuzzing.Corpus.Responses.";
    private const string RegressionResourcePrefix = "Dekaf.Fuzzing.Corpus.ResponseRegressions.";

    public static IReadOnlyList<ResponseParserCorpusSeed> LoadEmbedded() =>
        LoadEmbedded(ResourcePrefix);

    /// <summary>
    /// Loads promoted crash inputs that must be rejected by the response parsers.
    /// Unlike <see cref="LoadEmbedded()"/> seeds, these are intentionally malformed and
    /// carry descriptive names rather than the strict TypeName.vN naming.
    /// </summary>
    public static IReadOnlyList<ResponseParserCorpusSeed> LoadEmbeddedRegressions() =>
        LoadEmbedded(RegressionResourcePrefix);

    private static IReadOnlyList<ResponseParserCorpusSeed> LoadEmbedded(string resourcePrefix)
    {
        var assembly = typeof(ResponseParserCorpus).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => LoadSeed(assembly, name, resourcePrefix))
            .ToArray();
    }

    private static ResponseParserCorpusSeed LoadSeed(Assembly assembly, string resourceName, string resourcePrefix)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded response fuzz seed '{resourceName}'.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new ResponseParserCorpusSeed(resourceName[resourcePrefix.Length..], memory.ToArray());
    }
}
