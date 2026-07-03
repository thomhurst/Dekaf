namespace Dekaf.Security.Sasl;

/// <summary>
/// AWS credentials used to sign AWS_MSK_IAM authentication payloads.
/// </summary>
public sealed class AwsCredentials
{
    /// <summary>
    /// Creates AWS credentials.
    /// </summary>
    public AwsCredentials(
        string accessKeyId,
        string secretAccessKey,
        string? sessionToken = null,
        DateTimeOffset? expiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretAccessKey);

        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken;
        Expiration = expiration;
    }

    /// <summary>
    /// AWS access key ID.
    /// </summary>
    public string AccessKeyId { get; }

    /// <summary>
    /// AWS secret access key.
    /// </summary>
    public string SecretAccessKey { get; }

    /// <summary>
    /// Optional AWS session token for temporary credentials.
    /// </summary>
    public string? SessionToken { get; }

    /// <summary>
    /// Optional expiration time for temporary credentials.
    /// </summary>
    public DateTimeOffset? Expiration { get; }

    internal bool IsExpired(DateTimeOffset now, TimeSpan refreshBuffer)
        => Expiration is { } expiration && expiration <= now.Add(refreshBuffer);
}

/// <summary>
/// Provides AWS credentials for AWS_MSK_IAM authentication.
/// </summary>
public interface IAwsCredentialsProvider
{
    /// <summary>
    /// Gets AWS credentials.
    /// </summary>
    ValueTask<AwsCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// AWS credentials provider backed by a fixed credential set.
/// </summary>
public sealed class StaticAwsCredentialsProvider : IAwsCredentialsProvider
{
    private readonly AwsCredentials _credentials;

    /// <summary>
    /// Creates a static AWS credentials provider.
    /// </summary>
    public StaticAwsCredentialsProvider(AwsCredentials credentials)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
    }

    /// <inheritdoc />
    public ValueTask<AwsCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
        => new(_credentials);
}

internal sealed class DelegateAwsCredentialsProvider : IAwsCredentialsProvider
{
    private readonly Func<CancellationToken, ValueTask<AwsCredentials>> _provider;

    public DelegateAwsCredentialsProvider(Func<CancellationToken, ValueTask<AwsCredentials>> provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ValueTask<AwsCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
        => _provider(cancellationToken);
}
