using System.Net;
#if !NETSTANDARD2_0
using System.Net.Security;
#endif
using System.Security.Principal;

namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for GSSAPI (Kerberos) authentication.
/// </summary>
public sealed class GssapiConfig
{
    internal const string ClientKeytabEnvironmentVariable = "KRB5_CLIENT_KTNAME";

    private static readonly object s_keytabEnvironmentLock = new();
    private static string? s_configuredKeytabEnvironmentValue;

    /// <summary>
    /// The Kerberos service principal name. Defaults to "kafka".
    /// The full SPN will be: {ServiceName}/{hostname}@{Realm}
    /// </summary>
    public string ServiceName { get; init; } = "kafka";

    /// <summary>
    /// The client principal name (e.g., "user@REALM.COM").
    /// </summary>
    /// <remarks>
    /// When set, Dekaf passes this value to <see cref="NegotiateAuthenticationClientOptions.Credential"/>
    /// as the client identity. The underlying platform must still be able to satisfy that
    /// identity from its credential cache or configured client keytab; Dekaf does not store a
    /// Kerberos password.
    /// </remarks>
    public string? Principal { get; init; }

    /// <summary>
    /// Path to the keytab file for service authentication.
    /// </summary>
    /// <remarks>
    /// On Unix-like platforms, Dekaf sets <c>KRB5_CLIENT_KTNAME</c> before the first
    /// authentication attempt. MIT/Heimdal Kerberos treat this as a process-wide setting, so
    /// different keytabs in the same process are rejected. Windows keytab selection is not
    /// supported by <see cref="NegotiateAuthentication"/> and fails during build/initialization.
    /// </remarks>
    public string? KeytabPath { get; init; }

    /// <summary>
    /// The Kerberos realm. If not specified, uses the default realm from the system configuration.
    /// </summary>
    public string? Realm { get; init; }

    internal static void ValidateForBuild(SaslMechanism mechanism, GssapiConfig? config)
    {
        if (mechanism != SaslMechanism.Gssapi)
        {
            return;
        }

        if (config is null)
        {
            throw new InvalidOperationException("GSSAPI configuration must be provided when SaslMechanism is Gssapi.");
        }

        config.Validate();
    }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceName);

        if (Principal is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Principal);
        }

        if (Realm is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Realm);
        }

        if (KeytabPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(KeytabPath);
            if (OperatingSystem.IsWindows())
            {
                throw new NotSupportedException(
                    "GSSAPI KeytabPath is not supported on Windows by .NET NegotiateAuthentication. " +
                    "Use the Windows credential store, run the process as the desired identity, or omit KeytabPath.");
            }

            var localPath = GetLocalKeytabPath(NormalizeKeytabEnvironmentValue(KeytabPath));
            if (localPath is not null && !File.Exists(localPath))
            {
                throw new FileNotFoundException("GSSAPI keytab file was not found.", localPath);
            }
        }
    }

#if !NETSTANDARD2_0
    internal NegotiateAuthenticationClientOptions CreateClientOptions(string targetHost)
    {
        Validate();

        var options = new NegotiateAuthenticationClientOptions
        {
            Package = "Kerberos",
            TargetName = BuildSpn(targetHost),
            RequiredProtectionLevel = ProtectionLevel.None,
            AllowedImpersonationLevel = TokenImpersonationLevel.Identification
        };

        var credential = CreateCredential();
        if (credential is not null)
        {
            options.Credential = credential;
        }

        return options;
    }
#endif

    internal void ApplyKeytabEnvironment()
    {
        if (KeytabPath is null)
        {
            return;
        }

        Validate();

        var environmentValue = NormalizeKeytabEnvironmentValue(KeytabPath);
        lock (s_keytabEnvironmentLock)
        {
            var existing = Environment.GetEnvironmentVariable(ClientKeytabEnvironmentVariable);
            if (!string.IsNullOrEmpty(existing) && !string.Equals(existing, environmentValue, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot use GSSAPI KeytabPath '{KeytabPath}' because process-wide " +
                    $"{ClientKeytabEnvironmentVariable} is already set to '{existing}'. " +
                    "Use one client keytab per process or configure Kerberos credentials externally.");
            }

            if (s_configuredKeytabEnvironmentValue is not null &&
                !string.Equals(s_configuredKeytabEnvironmentValue, environmentValue, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot use GSSAPI KeytabPath '{KeytabPath}' because this process already configured " +
                    $"{ClientKeytabEnvironmentVariable} as '{s_configuredKeytabEnvironmentValue}'. " +
                    "Use one client keytab per process or configure Kerberos credentials externally.");
            }

            Environment.SetEnvironmentVariable(ClientKeytabEnvironmentVariable, environmentValue);
            s_configuredKeytabEnvironmentValue = environmentValue;
        }
    }

    internal string BuildSpn(string targetHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);

        var spn = $"{ServiceName}/{targetHost}";
        return Realm is null ? spn : $"{spn}@{Realm}";
    }

    internal NetworkCredential? CreateCredential()
    {
        if (Principal is null)
        {
            return null;
        }

        var principal = Principal.Trim();
        if (principal.Contains('@', StringComparison.Ordinal) || Realm is null)
        {
            return new NetworkCredential(principal, string.Empty);
        }

        return new NetworkCredential(principal, string.Empty, Realm);
    }

    internal static string NormalizeKeytabEnvironmentValue(string keytabPath)
    {
        var trimmed = keytabPath.Trim();
        const string filePrefix = "FILE:";
        if (trimmed.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return filePrefix + Path.GetFullPath(trimmed[filePrefix.Length..]);
        }

        return trimmed.Contains(':', StringComparison.Ordinal)
            ? trimmed
            : Path.GetFullPath(trimmed);
    }

    private static string? GetLocalKeytabPath(string environmentValue)
    {
        const string filePrefix = "FILE:";
        if (environmentValue.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return environmentValue[filePrefix.Length..];
        }

        return environmentValue.Contains(':', StringComparison.Ordinal)
            ? null
            : environmentValue;
    }
}
