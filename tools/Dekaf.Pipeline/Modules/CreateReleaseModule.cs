using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Configuration;
using ModularPipelines.Context;
using ModularPipelines.Git.Attributes;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.Models;
using ModularPipelines.Modules;
using Octokit;

namespace Dekaf.Pipeline.Modules;

/// <summary>
/// Creates a GitHub release with auto-generated release notes, but only when a NuGet publish
/// actually shipped packages in this run.
/// </summary>
/// <remarks>
/// The release is tagged <c>v{version}</c> with the version GitVersion computed (see
/// <see cref="GenerateVersionModule"/>). These tags are GitVersion's version sources: each one lets
/// the next commit on main advance the patch, so every publish gets a fresh, unique version. Because
/// of that, a publish must always create its tag — see the uniqueness note in GenerateVersionModule.
/// </remarks>
[RunOnlyOnBranch("main")]
[DependsOn<UploadToNuGetModule>(Optional = true)]
[DependsOn<GenerateVersionModule>]
public class CreateReleaseModule : Module<Release>
{
    protected override ModuleConfiguration Configure() =>
        new ModuleConfigurationBuilder()
            .WithSkipWhen(async ctx =>
            {
                // Only release when the NuGet upload ran and actually published something. The upload
                // module returns an empty list when publishing is disabled (no ShouldPublish / no API
                // key / no packages), and throws when every push was a duplicate — so a non-empty
                // result is the signal that new packages reached the feed.
                if (ctx.GetModuleIfRegistered<UploadToNuGetModule>() is not { } uploadModule)
                {
                    return SkipDecision.Skip("UploadToNuGetModule is not registered");
                }

                var upload = await uploadModule;

                if (upload.IsSkipped)
                {
                    return SkipDecision.Skip("NuGet upload was skipped");
                }

                return upload.ValueOrDefault is { Count: > 0 }
                    ? SkipDecision.DoNotSkip
                    : SkipDecision.Skip("No NuGet packages were published");
            })
            .Build();

    protected override async Task<Release?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versionModule = await context.GetModule<GenerateVersionModule>();
        var version = versionModule.ValueOrDefault?.SemVer
            ?? throw new InvalidOperationException("No version was generated for the release.");

        var github = context.GitHub();
        var repositoryId = long.Parse(github.EnvironmentVariables.RepositoryId!);
        var tag = $"v{version}";

        // Generating notes from the previous release's tag gives a clean "what changed since"
        // changelog. On the very first release there is no previous tag; let GitHub infer the range.
        string? previousTag = null;
        try
        {
            var lastRelease = await github.Client.Repository.Release.GetLatest(repositoryId);
            previousTag = lastRelease.TagName;
        }
        catch (NotFoundException)
        {
            context.Logger.LogInformation("No previous release found; generating notes from the first commit.");
        }

        var releaseNotes = await github.Client.Repository.Release.GenerateReleaseNotes(
            repositoryId,
            new GenerateReleaseNotesRequest(tag) { PreviousTagName = previousTag });

        context.Logger.LogInformation("Creating GitHub release {Tag}", tag);

        return await github.Client.Repository.Release.Create(
            repositoryId,
            new NewRelease(tag)
            {
                Name = version,
                GenerateReleaseNotes = false,
                Body = releaseNotes.Body
            });
    }
}
