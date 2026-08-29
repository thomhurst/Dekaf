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
}
