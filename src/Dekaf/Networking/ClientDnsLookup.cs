namespace Dekaf.Networking;

/// <summary>
/// Controls how broker hostnames are resolved before connecting.
/// </summary>
public enum ClientDnsLookup
{
    /// <summary>
    /// Resolve all IP addresses for a hostname and try them in order until one connects.
    /// </summary>
    UseAllDnsIps,

    /// <summary>
    /// Resolve hostnames to their canonical DNS name before connecting.
    /// </summary>
    ResolveCanonicalBootstrapServersOnly
}
