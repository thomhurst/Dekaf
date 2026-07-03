using System.Text.Json;
using System.Xml.Linq;

namespace Dekaf.Security.Sasl;

/// <summary>
/// Default AWS credentials provider for AWS_MSK_IAM authentication.
/// </summary>
public sealed class AwsMskIamDefaultCredentialsProvider : IAwsCredentialsProvider
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly string? _profileName;
    private readonly string? _region;
    private readonly string _webIdentitySessionName = "dekaf-" + Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AwsCredentials? _cachedCredentials;

    /// <summary>
    /// Creates the default AWS credentials provider.
    /// </summary>
    public AwsMskIamDefaultCredentialsProvider(string? profileName = null, string? region = null)
    {
        _profileName = profileName;
        _region = region;
    }

    /// <inheritdoc />
    public async ValueTask<AwsCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cachedCredentials;
        if (cached is not null && !cached.IsExpired(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)))
            return cached;

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cachedCredentials;
            if (cached is not null && !cached.IsExpired(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)))
                return cached;

            var credentials =
                TryGetEnvironmentCredentials() ??
                await TryGetWebIdentityCredentialsAsync(cancellationToken).ConfigureAwait(false) ??
                TryGetProfileCredentials() ??
                await TryGetContainerCredentialsAsync(cancellationToken).ConfigureAwait(false) ??
                await TryGetInstanceMetadataCredentialsAsync(cancellationToken).ConfigureAwait(false);

            if (credentials is null)
            {
                throw new InvalidOperationException(
                    "Unable to resolve AWS credentials for AWS_MSK_IAM. Checked environment variables, web identity, shared AWS profiles, ECS credentials, and EC2 instance metadata.");
            }

            _cachedCredentials = credentials;
            return credentials;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static AwsCredentials? TryGetEnvironmentCredentials()
    {
        var accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
            return null;

        return new AwsCredentials(
            accessKeyId,
            secretAccessKey,
            Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"));
    }

    private async Task<AwsCredentials?> TryGetWebIdentityCredentialsAsync(CancellationToken cancellationToken)
    {
        var roleArn = Environment.GetEnvironmentVariable("AWS_ROLE_ARN");
        var tokenFile = Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE");

        if (string.IsNullOrWhiteSpace(roleArn) || string.IsNullOrWhiteSpace(tokenFile) || !File.Exists(tokenFile))
            return null;

        var webIdentityToken = await File.ReadAllTextAsync(tokenFile, cancellationToken).ConfigureAwait(false);
        var sessionName = Environment.GetEnvironmentVariable("AWS_ROLE_SESSION_NAME") ?? _webIdentitySessionName;
        var endpoint = BuildStsEndpoint(ResolveStsRegion());

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "AssumeRoleWithWebIdentity",
                ["Version"] = "2011-06-15",
                ["RoleArn"] = roleArn,
                ["RoleSessionName"] = sessionName,
                ["WebIdentityToken"] = webIdentityToken,
                ["DurationSeconds"] = "3600"
            })
        };

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var document = XDocument.Parse(xml);
        var credentials = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Credentials");
        if (credentials is null)
            throw new InvalidOperationException("STS AssumeRoleWithWebIdentity response did not contain credentials");

        return new AwsCredentials(
            RequiredElement(credentials, "AccessKeyId"),
            RequiredElement(credentials, "SecretAccessKey"),
            RequiredElement(credentials, "SessionToken"),
            DateTimeOffset.Parse(RequiredElement(credentials, "Expiration"), null, System.Globalization.DateTimeStyles.AssumeUniversal));
    }

    private AwsCredentials? TryGetProfileCredentials()
    {
        var profileName = _profileName ?? Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default";

        foreach (var path in GetProfilePaths())
        {
            if (!File.Exists(path))
                continue;

            var profiles = ReadProfileFile(path);
            if (TryGetProfile(profiles, profileName, out var profile) &&
                profile.TryGetValue("aws_access_key_id", out var accessKeyId) &&
                profile.TryGetValue("aws_secret_access_key", out var secretAccessKey))
            {
                profile.TryGetValue("aws_session_token", out var sessionToken);
                return new AwsCredentials(accessKeyId, secretAccessKey, sessionToken);
            }
        }

        return null;
    }

    private static bool TryGetProfile(
        Dictionary<string, Dictionary<string, string>> profiles,
        string profileName,
        out Dictionary<string, string> profile)
    {
        if (profiles.TryGetValue(profileName, out profile!))
            return true;

        return profiles.TryGetValue("profile " + profileName, out profile!);
    }

    private static async Task<AwsCredentials?> TryGetContainerCredentialsAsync(CancellationToken cancellationToken)
    {
        var fullUri = Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_FULL_URI");
        var relativeUri = Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI");

        Uri? endpoint = null;
        if (!string.IsNullOrWhiteSpace(fullUri) && Uri.TryCreate(fullUri, UriKind.Absolute, out var parsedFullUri))
        {
            endpoint = parsedFullUri;
        }
        else if (!string.IsNullOrWhiteSpace(relativeUri))
        {
            endpoint = new Uri("http://169.254.170.2" + relativeUri);
        }

        if (endpoint is null)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            ApplyContainerAuthorization(request);

            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseJsonCredentials(json);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<AwsCredentials?> TryGetInstanceMetadataCredentialsAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(
            Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var token = await TryGetImdsTokenAsync(cancellationToken).ConfigureAwait(false);
            var roleName = await GetImdsStringAsync("http://169.254.169.254/latest/meta-data/iam/security-credentials/", token, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(roleName))
                return null;

            roleName = roleName.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            var json = await GetImdsStringAsync(
                "http://169.254.169.254/latest/meta-data/iam/security-credentials/" + Uri.EscapeDataString(roleName),
                token,
                cancellationToken).ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(json) ? null : ParseJsonCredentials(json);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<string?> TryGetImdsTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "http://169.254.169.254/latest/api/token");
        request.Headers.Add("X-aws-ec2-metadata-token-ttl-seconds", "21600");

        try
        {
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static async Task<string?> GetImdsStringAsync(
        string endpoint,
        string? token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Add("X-aws-ec2-metadata-token", token);

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static void ApplyContainerAuthorization(HttpRequestMessage request)
    {
        var token = Environment.GetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN");
        var tokenFile = Environment.GetEnvironmentVariable("AWS_CONTAINER_AUTHORIZATION_TOKEN_FILE");

        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile) && File.Exists(tokenFile))
            token = File.ReadAllText(tokenFile).Trim();

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);
    }

    private string ResolveStsRegion()
        => _region ??
           Environment.GetEnvironmentVariable("AWS_REGION") ??
           Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ??
           "us-east-1";

    private static Uri BuildStsEndpoint(string region)
        => region.StartsWith("cn-", StringComparison.Ordinal)
            ? new Uri($"https://sts.{region}.amazonaws.com.cn/")
            : new Uri($"https://sts.{region}.amazonaws.com/");

    private static string RequiredElement(XElement parent, string localName)
        => parent.Elements().First(element => element.Name.LocalName == localName).Value;

    private static IEnumerable<string> GetProfilePaths()
    {
        var sharedCredentialsFile = Environment.GetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE");
        if (!string.IsNullOrWhiteSpace(sharedCredentialsFile))
            yield return sharedCredentialsFile;

        var configFile = Environment.GetEnvironmentVariable("AWS_CONFIG_FILE");
        if (!string.IsNullOrWhiteSpace(configFile))
            yield return configFile;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".aws", "credentials");
            yield return Path.Combine(home, ".aws", "config");
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ReadProfileFile(string path)
    {
        var profiles = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? currentProfile = null;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                var section = line[1..^1].Trim();
                currentProfile = profiles.GetValueOrDefault(section);
                if (currentProfile is null)
                {
                    currentProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    profiles[section] = currentProfile;
                }
                continue;
            }

            if (currentProfile is null)
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            currentProfile[line[..equalsIndex].Trim()] = line[(equalsIndex + 1)..].Trim();
        }

        return profiles;
    }

    private static AwsCredentials ParseJsonCredentials(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new AwsCredentials(
            RequiredProperty(root, "AccessKeyId"),
            RequiredProperty(root, "SecretAccessKey"),
            TryGetProperty(root, "Token"),
            TryParseExpiration(TryGetProperty(root, "Expiration")));
    }

    private static string RequiredProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : throw new InvalidOperationException($"AWS credentials response did not contain {name}");

    private static string? TryGetProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? TryParseExpiration(string? value)
        => DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var expiration)
            ? expiration
            : null;
}
