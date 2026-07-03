using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

[NotInParallel("AwsEnvironment")]
public sealed class AwsMskIamDefaultCredentialsProviderTests
{
    [Test]
    public async Task GetCredentialsAsync_UsesEnvironmentCredentials()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN");

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "env-access");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "env-secret");
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", "env-token");

            var provider = new AwsMskIamDefaultCredentialsProvider();

            var credentials = await provider.GetCredentialsAsync();

            await Assert.That(credentials.AccessKeyId).IsEqualTo("env-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("env-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("env-token");
        }
        finally
        {
            snapshot.Restore();
        }
    }

    [Test]
    public async Task GetCredentialsAsync_UsesConfiguredSharedCredentialsProfile()
    {
        var snapshot = EnvironmentSnapshot.Capture(
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AWS_SESSION_TOKEN",
            "AWS_SHARED_CREDENTIALS_FILE",
            "AWS_PROFILE",
            "AWS_ROLE_ARN",
            "AWS_WEB_IDENTITY_TOKEN_FILE",
            "AWS_CONTAINER_CREDENTIALS_FULL_URI",
            "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI",
            "AWS_EC2_METADATA_DISABLED");
        var credentialsFile = Path.GetTempFileName();

        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
            Environment.SetEnvironmentVariable("AWS_ROLE_ARN", null);
            Environment.SetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI", null);
            Environment.SetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", null);
            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
            Environment.SetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE", credentialsFile);
            Environment.SetEnvironmentVariable("AWS_PROFILE", "msk-prod");

            await File.WriteAllTextAsync(
                credentialsFile,
                """
                [default]
                aws_access_key_id = default-access
                aws_secret_access_key = default-secret

                [msk-prod]
                aws_access_key_id = profile-access
                aws_secret_access_key = profile-secret
                aws_session_token = profile-token
                """);

            var provider = new AwsMskIamDefaultCredentialsProvider();

            var credentials = await provider.GetCredentialsAsync();

            await Assert.That(credentials.AccessKeyId).IsEqualTo("profile-access");
            await Assert.That(credentials.SecretAccessKey).IsEqualTo("profile-secret");
            await Assert.That(credentials.SessionToken).IsEqualTo("profile-token");
        }
        finally
        {
            File.Delete(credentialsFile);
            snapshot.Restore();
        }
    }

    private sealed class EnvironmentSnapshot
    {
        private readonly Dictionary<string, string?> _values;

        private EnvironmentSnapshot(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public static EnvironmentSnapshot Capture(params string[] names)
        {
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var name in names)
                values[name] = Environment.GetEnvironmentVariable(name);
            return new EnvironmentSnapshot(values);
        }

        public void Restore()
        {
            foreach (var (name, value) in _values)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
