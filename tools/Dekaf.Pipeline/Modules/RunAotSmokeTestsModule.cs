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
    protected override ModuleConfiguration Configure()
    {
        return new ModuleConfigurationBuilder()
            .WithTimeout(TimeSpan.FromMinutes(10))
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
        var project = rootDirectory.FindFile(x => x.Name == "Dekaf.Tests.Aot.csproj");
        if (project is null)
        {
            throw new InvalidOperationException("Dekaf.Tests.Aot.csproj not found");
        }

        var outputDirectory = Path.Combine(
            rootDirectory.Path,
            "artifacts",
            "aot",
            "core");

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

        var runResult = await command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions(Path.Combine(outputDirectory, "Dekaf.Tests.Aot")),
            new CommandExecutionOptions
            {
                WorkingDirectory = outputDirectory
            },
            cancellationToken);

        return [publishResult, runResult];
    }
}
