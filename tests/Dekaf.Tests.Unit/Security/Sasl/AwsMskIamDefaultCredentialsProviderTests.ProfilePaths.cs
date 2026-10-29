using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed partial class AwsMskIamDefaultCredentialsProviderTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SharedProfile_CredentialsFilePrecedesConfigFile(bool overrideCredentials, bool overrideConfig)
    {
        using var environment = new CredentialEnvironment();
        using var home = new ProfileHome();
        Environment.SetEnvironmentVariable("AWS_PROFILE", "msk");
        Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE",
            overrideCredentials ? environment.WriteFile(ProfileFile("override-credentials")) : null);
        Environment.SetEnvironmentVariable("AWS_CONFIG_FILE",
            overrideConfig ? environment.WriteFile(ConfigProfileFile("override-config")) : null);
        using var http = CreateProfileHttpClient();
        var provider = new AwsMskIamDefaultCredentialsProvider(http, userProfileDirectory: home.DirectoryPath);

        var credentials = await provider.GetCredentialsAsync();

        await AssertProfileCredentialsAsync(credentials, overrideCredentials ? "override-credentials" : "credentials");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SharedProfile_ReplacedHomeFileIsNotSearched(bool credentialsFile, bool missingOverride)
    {
        using var environment = new CredentialEnvironment();
        using var home = new ProfileHome();
        Environment.SetEnvironmentVariable("AWS_PROFILE", "msk");
        Environment.SetEnvironmentVariable(credentialsFile ? "AWS_SHARED_CREDENTIALS_FILE" : "AWS_CONFIG_FILE", missingOverride
            ? Path.Combine(home.DirectoryPath, "missing-override")
            : environment.WriteFile(ProfileFile("other").Replace("[msk]", "[other]")));
        using var http = CreateProfileHttpClient();
        var provider = new AwsMskIamDefaultCredentialsProvider(http, userProfileDirectory: home.DirectoryPath);

        await Assert.That(async () => await provider.GetCredentialsAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SharedProfile_OneOverridePreservesOtherDefaultFile(bool overrideCredentials, bool missingOverride)
    {
        using var environment = new CredentialEnvironment();
        using var home = new ProfileHome();
        Environment.SetEnvironmentVariable("AWS_PROFILE", "msk");
        Environment.SetEnvironmentVariable(overrideCredentials ? "AWS_CONFIG_FILE" : "AWS_SHARED_CREDENTIALS_FILE", null);
        if (missingOverride)
        {
            Environment.SetEnvironmentVariable(overrideCredentials ? "AWS_SHARED_CREDENTIALS_FILE" : "AWS_CONFIG_FILE",
                Path.Combine(home.DirectoryPath, "missing-override"));
        }
        using var http = CreateProfileHttpClient();
        var provider = new AwsMskIamDefaultCredentialsProvider(http, userProfileDirectory: home.DirectoryPath);

        var credentials = await provider.GetCredentialsAsync();

        await AssertProfileCredentialsAsync(credentials, overrideCredentials ? "config" : "credentials");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SharedProfile_ConfigFileUsedWhenCredentialsProfileAbsent(bool overrideConfig, bool missingCredentialsFile)
    {
        using var environment = new CredentialEnvironment();
        using var home = new ProfileHome();
        Environment.SetEnvironmentVariable("AWS_PROFILE", "msk");
        Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", missingCredentialsFile
            ? Path.Combine(home.DirectoryPath, "missing-credentials")
            : environment.WriteFile(ProfileFile("other").Replace("[msk]", "[other]")));
        Environment.SetEnvironmentVariable("AWS_CONFIG_FILE", overrideConfig
            ? environment.WriteFile(ConfigProfileFile("override-config"))
            : null);
        using var http = CreateProfileHttpClient();
        var provider = new AwsMskIamDefaultCredentialsProvider(http, userProfileDirectory: home.DirectoryPath);

        var credentials = await provider.GetCredentialsAsync();

        await AssertProfileCredentialsAsync(credentials, overrideConfig ? "override-config" : "config");
    }

    private static HttpClient CreateProfileHttpClient() => new(new CredentialHandler((_, _) =>
        throw new NotSupportedException("Profile resolution must not make HTTP requests")));

    private static string ConfigProfileFile(string prefix) => ProfileFile(prefix).Replace("[msk]", "[profile msk]");

    private static async Task AssertProfileCredentialsAsync(AwsCredentials credentials, string prefix)
    {
        await Assert.That(credentials.AccessKeyId).IsEqualTo(prefix + "-access");
        await Assert.That(credentials.SecretAccessKey).IsEqualTo(prefix + "-secret");
        await Assert.That(credentials.SessionToken).IsEqualTo(prefix + "-token");
    }

    private sealed class ProfileHome : IDisposable
    {
        public string DirectoryPath { get; } = Directory.CreateTempSubdirectory("dekaf-aws-profile-").FullName;

        public ProfileHome()
        {
            var awsDirectory = Directory.CreateDirectory(Path.Combine(DirectoryPath, ".aws")).FullName;
            File.WriteAllText(Path.Combine(awsDirectory, "credentials"), ProfileFile("credentials"));
            File.WriteAllText(Path.Combine(awsDirectory, "config"), ConfigProfileFile("config"));
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
