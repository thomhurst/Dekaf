using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace Dekaf.Pipeline.Modules;

public record PackedProject(string Name, string Version);

[DependsOn<BuildModule>]
[DependsOn<RunUnitTestsModule>]
[DependsOn<GenerateVersionModule>]
public class PackModule : Module<List<PackedProject>>
{
    private static readonly string[] PackageProjects =
    [
        "Dekaf",
        "Dekaf.Compression.Lz4",
        "Dekaf.Compression.Snappy",
        "Dekaf.Compression.Zstd",
        "Dekaf.Extensions.DependencyInjection",
        "Dekaf.Extensions.Hosting",
        "Dekaf.SchemaRegistry",
        "Dekaf.SchemaRegistry.Avro",
        "Dekaf.SchemaRegistry.Protobuf",
        "Dekaf.Serialization.Json"
    ];

    protected override async Task<List<PackedProject>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer ?? "1.0.0";

        var packedProjects = new List<PackedProject>();

        foreach (var projectName in PackageProjects)
        {
            var projectFile = context.Git().RootDirectory.FindFile(x => x.Name == $"{projectName}.csproj");

            if (projectFile is null)
            {
                continue;
            }

            await context.DotNet().Pack(new DotNetPackOptions
            {
                ProjectSolution = projectFile.Path,
                Configuration = "Release",
                NoBuild = true,
                Properties =
                [
                    new KeyValue("Version", version),
                    new KeyValue("PackageVersion", version)
                ]
            }, null, cancellationToken);

            packedProjects.Add(new PackedProject(projectName, version));
        }

        return packedProjects;
    }
}
