namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for SASL re-authentication (KIP-368).
/// When enabled, the client will proactively re-authenticate on existing connections
/// before SASL session credentials expire, avoiding connection teardown/rebuild.
/// </summary>
public sealed class SaslReauthenticationConfig
{
    /// <summary>
    /// Whether SASL re-authentication is enabled.
    /// Default is true - re-authentication will occur when the broker reports a session lifetime.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The fraction of the session lifetime at which re-authentication should be triggered.
    /// For example, 0.9 means re-authentication will occur when 90% of the session lifetime has elapsed.
    /// Must be between 0.0 (exclusive) and 1.0 (exclusive).
    /// Default is 0.9 (90% of session lifetime).
    /// </summary>
    public double ReauthenticationThreshold { get; init; } = 0.9;

    /// <summary>
    /// Minimum session lifetime in milliseconds below which re-authentication is not attempted.
    /// If the broker reports a session lifetime shorter than this value, re-authentication
    /// will not be scheduled to avoid excessive re-auth overhead.
    /// Default is 10000 (10 seconds).
    /// </summary>
    public long MinSessionLifetimeMs { get; init; } = 10_000;

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when ReauthenticationThreshold is not in (0.0, 1.0) or MinSessionLifetimeMs is negative.
    /// </exception>
    public void Validate()
    {
        if (ReauthenticationThreshold is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReauthenticationThreshold),
                ReauthenticationThreshold,
                "ReauthenticationThreshold must be between 0.0 (exclusive) and 1.0 (exclusive)");
        }

        if (MinSessionLifetimeMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinSessionLifetimeMs),
                MinSessionLifetimeMs,
                "MinSessionLifetimeMs must be non-negative");
        }
    }
}
