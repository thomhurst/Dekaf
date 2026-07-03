namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for AWS_MSK_IAM SASL authentication.
/// </summary>
public sealed class AwsMskIamConfig
{
    private IAwsCredentialsProvider? _resolvedCredentialsProvider;

    /// <summary>
    /// AWS region for the MSK broker. When null, Dekaf tries the broker hostname and AWS region environment variables.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// AWS profile name to use from shared AWS credentials/config files.
    /// When null, AWS_PROFILE or the default profile is used.
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// User agent value embedded in the AWS_MSK_IAM payload.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Validity period, in seconds, for the SigV4 presigned authentication payload.
    /// Amazon MSK recommends 15 minutes.
    /// </summary>
    public TimeSpan AuthenticationPayloadExpiration { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Custom AWS credentials provider. Takes precedence over the default credential chain.
    /// </summary>
    public IAwsCredentialsProvider? CredentialsProvider { get; init; }

    /// <summary>
    /// Custom AWS credentials provider callback. Used only when <see cref="CredentialsProvider"/> is null.
    /// </summary>
    public Func<CancellationToken, ValueTask<AwsCredentials>>? CredentialsProviderFunc { get; init; }

    internal IAwsCredentialsProvider CreateCredentialsProvider()
    {
        if (CredentialsProvider is not null)
            return CredentialsProvider;

        var provider = _resolvedCredentialsProvider;
        if (provider is not null)
            return provider;

        provider = CredentialsProviderFunc is not null
            ? new DelegateAwsCredentialsProvider(CredentialsProviderFunc)
            : new AwsMskIamDefaultCredentialsProvider(ProfileName, Region);

        return Interlocked.CompareExchange(ref _resolvedCredentialsProvider, provider, null) ?? provider;
    }
}
