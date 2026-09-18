using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

public record PackedProject(string Name, string Version);

// The test dependencies are optional so packaging survives SKIP_UNIT_TESTS=true: when the unit
// test modules are registered (local runs, the publish job) packing still waits for them, and when
// they are not, the edge is dropped instead of failing the graph.
[DependsOn<BuildModule>]
[DependsOn<RunStressTestsUnitTestsModule>(Optional = true)]
[DependsOn<RunUnitTestsModule>(Optional = true)]
[DependsOn<GenerateVersionModule>]
public class PackModule : Module<List<PackedProject>>
{
    protected override async Task<List<PackedProject>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer ?? "1.0.0";

        var packedProjects = new List<PackedProject>();
        var sourceDirectory = Path.Combine(context.Git().RootDirectory.Path, "src");
        var projectFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal);

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);

            await context.DotNet().Pack(new DotNetPackOptions
            {
                ProjectSolution = projectFile,
                Configuration = "Release",
                NoBuild = true,
                IncludeSymbols = true,
                IncludeSource = true,
                Properties =
                [
                    new KeyValue("Version", version),
                    new KeyValue("PackageVersion", version),
                    new KeyValue("ContinuousIntegrationBuild", "true"),
                    new KeyValue("Deterministic", "true"),
                    new KeyValue("PublishRepositoryUrl", "true")
                ]
            }, null, cancellationToken);

            packedProjects.Add(new PackedProject(projectName, version));
        }

        return packedProjects;
    }
}
