using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using ModularPipelines.Options;

namespace Dekaf.Pipeline.Modules;

[DependsOn<BuildModule>]
public class RunAotSmokeTestsModule(ICommand command) : Module<IReadOnlyList<CommandResult>>
{
    private static readonly AotSmokeProject[] Projects =
    [
        new("Dekaf.Tests.Aot.csproj", "core"),
        new("Dekaf.Tests.Aot.DependencyInjection.csproj", "dependency-injection")
    ];

    protected override ModuleConfiguration Configure()
    {
        return new ModuleConfigurationBuilder()
            .WithTimeout(TimeSpan.FromMinutes(20))
            .Build();
    }

    protected override async Task<IReadOnlyList<CommandResult>?> ExecuteAsync(
        IModuleContext context,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            context.Logger.LogInformation("Skipping NativeAOT smoke tests on non-Linux OS.");
            return null;
        }

        var rootDirectory = context.Git().RootDirectory;
        var results = new List<CommandResult>(Projects.Length * 2);

        foreach (var smokeProject in Projects)
        {
            var project = rootDirectory.FindFile(x => x.Name == smokeProject.ProjectFileName);
            if (project is null)
            {
                throw new InvalidOperationException($"{smokeProject.ProjectFileName} not found");
            }

            var outputDirectory = Path.Combine(
                rootDirectory.Path,
                "artifacts",
                "aot",
                smokeProject.OutputName);

            var publishResult = await context.DotNet().Publish(
                new DotNetPublishOptions
                {
                    ProjectSolution = project.Path,
                    Configuration = "Release",
                    Framework = "net10.0",
                    Runtime = "linux-x64",
                    NoRestore = true,
                    Output = outputDirectory,
                    Properties =
                    [
                        new KeyValue("PublishAot", "true"),
                        new KeyValue("SelfContained", "true"),
                        new KeyValue("ContinuousIntegrationBuild", "true"),
                        new KeyValue("TreatWarningsAsErrors", "true")
                    ]
                },
                null,
                cancellationToken);
            results.Add(publishResult);

            var executablePath = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(smokeProject.ProjectFileName));
            var runResult = await command.ExecuteCommandLineTool(
                new GenericCommandLineToolOptions(executablePath),
                new CommandExecutionOptions
                {
                    WorkingDirectory = outputDirectory
                },
                cancellationToken);
            results.Add(runResult);
        }

        return results;
    }

    private sealed record AotSmokeProject(string ProjectFileName, string OutputName);
}
