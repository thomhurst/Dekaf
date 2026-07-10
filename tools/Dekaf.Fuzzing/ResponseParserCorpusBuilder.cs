using System.Globalization;

namespace Dekaf.Fuzzing;

internal static class ResponseParserCorpusBuilder
{
    public static void Build(string fixturesDirectory, string outputDirectory)
    {
        var fixtures = Directory.GetFiles(fixturesDirectory, "*.bin")
            .Order(StringComparer.Ordinal)
            .Select(ReadFixture)
            .ToArray();
        var expectedNames = ResponseParserRegistry.Registrations
            .SelectMany(static registration => registration.SupportedVersions
                .Select(version => $"{registration.ResponseType.Name}.v{version}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var fixtureNames = fixtures
            .Select(static fixture => fixture.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!fixtureNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Response fixtures must cover every registered response parser and supported version.");
        }

        var registrationsByName = ResponseParserRegistry.Registrations
            .ToDictionary(static registration => registration.ResponseType.Name, StringComparer.Ordinal);
        Directory.CreateDirectory(outputDirectory);
        foreach (var fixture in fixtures)
        {
            var registration = registrationsByName[fixture.ResponseTypeName];
            var input = ResponseParserFuzzTarget.CreateInput(
                registration.ApiKey,
                fixture.Version,
                fixture.Payload);
            File.WriteAllBytes(Path.Combine(outputDirectory, fixture.Name), input);
        }
    }

    private static ResponseFixture ReadFixture(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var versionSeparator = name.LastIndexOf(".v", StringComparison.Ordinal);
        if (versionSeparator <= 0 ||
            !short.TryParse(
                name.AsSpan(versionSeparator + 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var version))
        {
            throw new InvalidOperationException($"Invalid response fixture name '{name}'.");
        }

        return new ResponseFixture(
            name,
            name[..versionSeparator],
            version,
            File.ReadAllBytes(path));
    }

    private sealed record ResponseFixture(
        string Name,
        string ResponseTypeName,
        short Version,
        byte[] Payload);
}
