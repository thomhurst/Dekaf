namespace Dekaf;

internal static class BootstrapServerList
{
    internal static string[] FromCommaSeparated(string servers)
    {
        CompatibilityThrowHelpers.ThrowIfNull(servers);
        return FromValues(servers.Split(','));
    }

    internal static string[] FromValues(params string[] servers)
    {
        CompatibilityThrowHelpers.ThrowIfNull(servers);

        if (servers.Length == 0)
            throw new ArgumentException("At least one bootstrap server must be specified.", nameof(servers));

        var normalized = new string[servers.Length];
        for (var i = 0; i < servers.Length; i++)
        {
            var server = servers[i];
            if (server is null)
                throw new ArgumentException("Bootstrap server entries cannot be null.", nameof(servers));

            var trimmed = server.Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Bootstrap server entries cannot be empty.", nameof(servers));

            normalized[i] = trimmed;
        }

        return normalized;
    }
}
