using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Dekaf;

internal static class BootstrapServerList
{
    internal readonly record struct Endpoint(string Normalized, string Host, int Port);

    internal static string[] FromCommaSeparated(string servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return FromValues(servers.Split(','));
    }

    internal static string[] FromValues(params string[] servers)
    {
        ArgumentNullException.ThrowIfNull(servers);

        if (servers.Length == 0)
            throw new ArgumentException("At least one bootstrap server must be specified.", nameof(servers));

        var normalized = new string[servers.Length];
        for (var i = 0; i < servers.Length; i++)
        {
            var server = servers[i];
            if (server is null)
                throw new ArgumentException("Bootstrap server entries cannot be null.", nameof(servers));

            normalized[i] = Parse(server, nameof(servers)).Normalized;
        }

        return normalized;
    }

    internal static Endpoint Parse(string server, string paramName = "bootstrapServers")
    {
        ArgumentNullException.ThrowIfNull(server);

        var value = server.Trim();
        if (value.Length == 0)
            throw new ArgumentException("Bootstrap server entries cannot be empty.", paramName);

        var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator >= 0)
        {
            if (!IsValidScheme(value.AsSpan(0, schemeSeparator)))
                throw InvalidEntry(server, paramName);

            // The prefix is syntax only. TLS and SASL remain controlled by their builder options.
            value = value[(schemeSeparator + 3)..];
        }

        value = value.TrimEnd('/');
        if (value.Length == 0)
            throw InvalidEntry(server, paramName);

        string host;
        string portText;
        var isIpv6 = value[0] == '[';
        if (isIpv6)
        {
            var closingBracket = value.IndexOf(']');
            if (closingBracket <= 1
                || closingBracket + 2 >= value.Length
                || value[closingBracket + 1] != ':')
            {
                throw InvalidEntry(server, paramName);
            }

            host = value.Substring(1, closingBracket - 1);
            if (!IPAddress.TryParse(host, out var address)
                || address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw InvalidEntry(server, paramName);
            }

            portText = value[(closingBracket + 2)..];
        }
        else
        {
            var colonIndex = value.LastIndexOf(':');
            if (colonIndex <= 0 || colonIndex != value.IndexOf(':'))
                throw InvalidEntry(server, paramName);

            host = value[..colonIndex];
            if (Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4))
                throw InvalidEntry(server, paramName);

            portText = value[(colonIndex + 1)..];
        }

        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65_535)
        {
            throw InvalidEntry(server, paramName);
        }

        var normalized = isIpv6 ? $"[{host}]:{port}" : $"{host}:{port}";
        return new Endpoint(normalized, host, port);
    }

    private static bool IsValidScheme(ReadOnlySpan<char> scheme)
    {
        if (scheme.IsEmpty || !char.IsLetter(scheme[0]))
            return false;

        for (var i = 1; i < scheme.Length; i++)
        {
            var character = scheme[i];
            if (!char.IsLetterOrDigit(character)
                && character is not ('+' or '-' or '.' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static ArgumentException InvalidEntry(string server, string paramName) =>
        new(
            $"Invalid bootstrap server '{server}'. Expected [PROTOCOL://]host:port or [PROTOCOL://][ipv6]:port.",
            paramName);
}
