using System.Text;

namespace Dekaf.Benchmarks.Infrastructure;

internal enum BenchmarkJob
{
    Dry,
    Short,
    Medium,
    Long,
    Default
}

internal static class BenchmarkJobSelection
{
    internal static string[] ExpandResponseFiles(string[] arguments)
    {
        var expanded = new List<string>(arguments.Length);

        foreach (var argument in arguments)
        {
            if (argument is not ['@', .. var path] || !File.Exists(path))
            {
                expanded.Add(argument);
                continue;
            }

            foreach (var line in File.ReadAllLines(path))
                expanded.AddRange(ConsumeTokens(line));
        }

        return expanded.ToArray();
    }

    internal static BenchmarkJob? GetExplicitJob(string[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument is "-j" or "--job")
                return i + 1 < arguments.Length ? Resolve(arguments[i + 1]) : null;

            const string longPrefix = "--job=";
            const string shortPrefix = "-j=";
            if (argument.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
                return Resolve(argument[longPrefix.Length..]);
            if (argument.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
                return Resolve(argument[shortPrefix.Length..]);
        }

        return null;
    }

    private static BenchmarkJob? Resolve(string value) => value.ToUpperInvariant() switch
    {
        "DRY" => BenchmarkJob.Dry,
        "SHORT" => BenchmarkJob.Short,
        "MEDIUM" => BenchmarkJob.Medium,
        "LONG" => BenchmarkJob.Long,
        "DEFAULT" => BenchmarkJob.Default,
        _ => null
    };

    // Mirrors BenchmarkDotNet's ConfigParser response-file tokenization.
    private static IEnumerable<string> ConsumeTokens(string line)
    {
        var insideQuotes = false;
        var token = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == ' ' && !insideQuotes)
            {
                if (token.Length > 0)
                {
                    yield return GetToken(token);
                    token.Clear();
                }

                continue;
            }

            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (character == '\\' && insideQuotes && i + 1 < line.Length)
            {
                if (line[i + 1] == '"')
                {
                    insideQuotes = false;
                    i++;
                    continue;
                }

                if (line[i + 1] == '\\')
                {
                    token.Append('\\');
                    i++;
                    continue;
                }
            }

            token.Append(character);
        }

        if (token.Length > 0)
            yield return GetToken(token);

        static string GetToken(StringBuilder tokenBuilder)
        {
            var value = tokenBuilder.ToString();
            return value.Contains(' ') ? $" {value}" : value;
        }
    }
}
