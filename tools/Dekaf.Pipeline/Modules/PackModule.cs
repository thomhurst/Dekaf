using System.Text.RegularExpressions;
using ModularPipelines;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Options;

namespace Dekaf.Pipeline.Modules;

public record PackedProject(string Name, string Version);

// The test dependencies are optional so packaging survives SKIP_UNIT_TESTS=true: when the unit
// test modules are registered (local runs, the publish job) packing still waits for them, and when
// they are not, the edge is dropped instead of failing the graph.
[DependsOn<BuildModule>]
[DependsOn<RunStressTestsUnitTestsModule>(Optional = true)]
[DependsOn<RunUnitTestsModule>(Optional = true)]
[DependsOn<GenerateVersionModule>]
public partial class PackModule : Module<List<PackedProject>>
{
    protected override async Task<List<PackedProject>> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer ?? "1.0.0";

        var packedProjects = new List<PackedProject>();
        var sourceDirectory = Path.Combine((await context.Tools.Git.Information.GetRequiredInfoAsync(cancellationToken)).Root.Path, "src");
        var projectFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal);

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);

            // Directory.Build.targets appends a project's PrereleaseLabel to its package version. The
            // upload looks packages up by file name, so the same version is derived here.
            var label = PrereleaseLabelRegex().Match(await File.ReadAllTextAsync(projectFile, cancellationToken));
            var packageVersion = label.Success ? $"{version}-{label.Groups[1].Value}" : version;

            await context.Tools.DotNet.PackAsync(new DotNetPackOptions
            {
                ProjectSolution = projectFile,
                Configuration = "Release",
                NoBuild = true,
                IncludeSymbols = true,
                IncludeSource = true,
                Properties =
                [
                    // PackageVersion is not passed: as a global property it would override the
                    // per-project prerelease version, here and in every referenced project.
                    new KeyValue("Version", version),
                    new KeyValue("ContinuousIntegrationBuild", "true"),
                    new KeyValue("Deterministic", "true"),
                    new KeyValue("PublishRepositoryUrl", "true")
                ]
            }, null, cancellationToken);

            packedProjects.Add(new PackedProject(projectName, packageVersion));
        }

        return packedProjects;
    }

    [GeneratedRegex(@"<PrereleaseLabel>\s*([0-9A-Za-z.-]+)\s*</PrereleaseLabel>")]
    private static partial Regex PrereleaseLabelRegex();
}
