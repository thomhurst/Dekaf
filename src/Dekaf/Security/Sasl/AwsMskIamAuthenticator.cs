using System.Text;
using System.Text.Json;
using Dekaf.Errors;

namespace Dekaf.Security.Sasl;

/// <summary>
/// AWS_MSK_IAM SASL mechanism authenticator.
/// </summary>
public sealed class AwsMskIamAuthenticator : ISaslAuthenticator
{
    private static readonly AwsMskIamConfig DefaultConfig = new();

    private readonly AwsMskIamConfig _config;
    private readonly string _host;
    private readonly IAwsCredentialsProvider _credentialsProvider;
    private AwsCredentials? _credentials;
    private bool _initialResponseSent;
    private bool _complete;

    /// <summary>
    /// Creates an AWS_MSK_IAM authenticator.
    /// </summary>
    public AwsMskIamAuthenticator(AwsMskIamConfig? config, string host)
    {
        _config = config ?? DefaultConfig;
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _host = host;
        _credentialsProvider = _config.CreateCredentialsProvider();
    }

    /// <inheritdoc />
    public string MechanismName => "AWS_MSK_IAM";

    /// <inheritdoc />
    public bool IsComplete => _complete;

    /// <summary>
    /// Fetches AWS credentials before the synchronous initial SASL response is created.
    /// </summary>
    public async ValueTask<AwsCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        _credentials = await _credentialsProvider.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
        return _credentials;
    }

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        if (_initialResponseSent)
            throw new InvalidOperationException("GetInitialResponse can only be called once");

        var credentials = _credentials
            ?? throw new InvalidOperationException("AWS credentials are not available. Call GetCredentialsAsync before authentication.");

        var region = ResolveRegion();
        var payload = AwsMskIamSignedPayload.Create(
            credentials,
            _host,
            region,
            string.IsNullOrWhiteSpace(_config.UserAgent) ? "dekaf" : _config.UserAgent,
            DateTimeOffset.UtcNow,
            _config.AuthenticationPayloadExpiration);

        _initialResponseSent = true;
        return Encoding.UTF8.GetBytes(payload);
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        if (challenge.Length == 0)
            throw new AuthenticationException("AWS_MSK_IAM authentication failed: broker returned an empty response");

        var response = Encoding.UTF8.GetString(challenge);
        try
        {
            using var document = JsonDocument.Parse(response);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new AuthenticationException("AWS_MSK_IAM authentication failed: broker response was not a JSON object");
        }
        catch (JsonException ex)
        {
            throw new AuthenticationException("AWS_MSK_IAM authentication failed: broker response was not valid JSON", ex);
        }

        _complete = true;
        return null;
    }

    private string ResolveRegion()
    {
        var region =
            _config.Region ??
            AwsMskIamRegionResolver.TryResolveFromHost(_host) ??
            Environment.GetEnvironmentVariable("AWS_REGION") ??
            Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");

        if (string.IsNullOrWhiteSpace(region))
            throw new InvalidOperationException(
                "AWS_MSK_IAM authentication requires an AWS region. Set AwsMskIamConfig.Region or use an MSK broker hostname containing the region.");

        return AwsMskIamRegionResolver.ValidateRegion(region);
    }
}

internal static class AwsMskIamRegionResolver
{
    public static string? TryResolveFromHost(string host)
    {
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < labels.Length; i++)
        {
            if (!IsAmazonAwsSuffix(labels, i) || i < 2)
                continue;

            var serviceLabel = labels[i - 2];
            var region = labels[i - 1];
            if (string.Equals(serviceLabel, "kafka", StringComparison.OrdinalIgnoreCase) && LooksLikeRegion(region))
                return region;
        }

        return null;
    }

    private static bool IsAmazonAwsSuffix(string[] labels, int index)
    {
        if (!string.Equals(labels[index], "amazonaws", StringComparison.OrdinalIgnoreCase))
            return false;

        return (index == labels.Length - 2 &&
                string.Equals(labels[index + 1], "com", StringComparison.OrdinalIgnoreCase)) ||
               (index == labels.Length - 3 &&
                string.Equals(labels[index + 1], "com", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(labels[index + 2], "cn", StringComparison.OrdinalIgnoreCase));
    }

    public static string ValidateRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new InvalidOperationException("AWS_MSK_IAM region cannot be empty.");

        var value = region.Trim();
        if (value.Any(static c => !IsRegionCharacter(c)))
            throw new InvalidOperationException(
                $"AWS_MSK_IAM region '{region}' is invalid. Region names may only contain lowercase letters, digits, and hyphens.");

        return value;
    }

    private static bool LooksLikeRegion(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        return parts[0].Length == 2 &&
               parts[^1].Length == 1 &&
               char.IsDigit(parts[^1][0]) &&
               parts.All(part => part.All(char.IsLetterOrDigit));
    }

    private static bool IsRegionCharacter(char value)
        => value is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
}
