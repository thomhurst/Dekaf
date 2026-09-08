using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CsProj;

namespace Dekaf.Benchmarks;

// The friend assembly name is shared with the repository's normal benchmark project.
// Explicitly locate this fixture project instead of BDN's assembly-name-based search.
public sealed class EvidenceGenerator() : CsProjGenerator("net10.0", null!, null!, null!)
{
    protected override FileInfo GetProjectFilePath(Type benchmarkTarget, ILogger logger)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(benchmarkTarget.Assembly.Location)!);
        while (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "Runner.csproj");
            if (File.Exists(project)) return new FileInfo(project);
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Cannot locate the administrative evidence Runner.csproj.");
    }
}
