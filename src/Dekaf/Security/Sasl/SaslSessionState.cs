namespace Dekaf.Security.Sasl;

/// <summary>
/// Tracks the state of a SASL session, including when re-authentication is needed.
/// This class is thread-safe for reads after initialization.
/// </summary>
internal sealed class SaslSessionState
{
    private readonly long _sessionLifetimeMs;
    private readonly double _reauthenticationThreshold;
    private readonly long _minSessionLifetimeMs;
    private readonly DateTimeOffset _authenticationTime;
    private readonly long _reauthenticationDelayMs;

    /// <summary>
    /// Creates a new SASL session state from the broker-reported session lifetime.
    /// </summary>
    /// <param name="sessionLifetimeMs">
    /// The session lifetime in milliseconds as reported by the broker in SaslAuthenticateResponse.
    /// A value of 0 means the session does not expire.
    /// </param>
    /// <param name="config">The re-authentication configuration.</param>
    /// <param name="authenticationTime">The time when authentication completed. If null, uses current time.</param>
    public SaslSessionState(
        long sessionLifetimeMs,
        SaslReauthenticationConfig config,
        DateTimeOffset? authenticationTime = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        _sessionLifetimeMs = sessionLifetimeMs;
        _reauthenticationThreshold = config.ReauthenticationThreshold;
        _minSessionLifetimeMs = config.MinSessionLifetimeMs;
        _authenticationTime = authenticationTime ?? DateTimeOffset.UtcNow;

        // Calculate re-auth delay
        _reauthenticationDelayMs = CalculateReauthenticationDelay();
    }

    /// <summary>
    /// Gets the session lifetime in milliseconds as reported by the broker.
    /// A value of 0 means the session does not expire.
    /// </summary>
    public long SessionLifetimeMs => _sessionLifetimeMs;

    /// <summary>
    /// Gets the time at which authentication was completed.
    /// </summary>
    public DateTimeOffset AuthenticationTime => _authenticationTime;

    /// <summary>
    /// Gets the computed delay in milliseconds before re-authentication should occur.
    /// A value of 0 or negative means re-authentication is not needed.
    /// </summary>
    public long ReauthenticationDelayMs => _reauthenticationDelayMs;

    /// <summary>
    /// Gets whether re-authentication is needed for this session.
    /// Returns false if the session has no expiry or if the session lifetime is too short.
    /// </summary>
    public bool RequiresReauthentication => _reauthenticationDelayMs > 0;

    /// <summary>
    /// Gets the absolute time at which re-authentication should be triggered.
    /// Returns null if re-authentication is not needed.
    /// </summary>
    public DateTimeOffset? ReauthenticationDeadline =>
        RequiresReauthentication
            ? _authenticationTime.AddMilliseconds(_reauthenticationDelayMs)
            : null;

    /// <summary>
    /// Gets the absolute time at which the session expires.
    /// Returns null if the session does not expire.
    /// </summary>
    public DateTimeOffset? SessionExpiryTime =>
        _sessionLifetimeMs > 0
            ? _authenticationTime.AddMilliseconds(_sessionLifetimeMs)
            : null;

    /// <summary>
    /// Checks whether re-authentication should be initiated now based on the current time.
    /// </summary>
    /// <param name="now">The current time. If null, uses UtcNow.</param>
    /// <returns>True if re-authentication should be initiated.</returns>
    public bool ShouldReauthenticateNow(DateTimeOffset? now = null)
    {
        if (!RequiresReauthentication)
        {
            return false;
        }

        var currentTime = now ?? DateTimeOffset.UtcNow;
        return currentTime >= ReauthenticationDeadline;
    }

    private long CalculateReauthenticationDelay()
    {
        // No session expiry - no re-auth needed
        if (_sessionLifetimeMs <= 0)
        {
            return 0;
        }

        // Session lifetime too short - skip re-auth to avoid overhead
        if (_sessionLifetimeMs < _minSessionLifetimeMs)
        {
            return 0;
        }

        // Calculate the delay as a fraction of session lifetime
        var delay = (long)(_sessionLifetimeMs * _reauthenticationThreshold);

        // Ensure the delay is at least 1ms if we're doing re-auth
        return Math.Max(delay, 1);
    }
}
