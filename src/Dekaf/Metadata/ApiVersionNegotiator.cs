using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Metadata;

internal static class ApiVersionNegotiator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNegotiate(
        short brokerMinVersion,
        short brokerMaxVersion,
        short clientMinVersion,
        short clientMaxVersion,
        out short negotiatedVersion)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clientMinVersion, clientMaxVersion);

        var lowestUsableVersion = Math.Max(clientMinVersion, brokerMinVersion);
        var highestUsableVersion = Math.Min(clientMaxVersion, brokerMaxVersion);
        if (lowestUsableVersion > highestUsableVersion)
        {
            negotiatedVersion = default;
            return false;
        }

        negotiatedVersion = highestUsableVersion;
        return true;
    }

    [DoesNotReturn]
    public static void ThrowApiAbsent(ApiKey apiKey, short clientMinVersion, short clientMaxVersion) =>
        throw new BrokerVersionException(
            $"Broker does not support {apiKey} (API key {(short)apiKey}) for client " +
            $"[{clientMinVersion}, {clientMaxVersion}]: API absent.");

    [DoesNotReturn]
    public static void ThrowDisjointRange(
        ApiKey apiKey,
        short brokerMinVersion,
        short brokerMaxVersion,
        short clientMinVersion,
        short clientMaxVersion) =>
        throw new BrokerVersionException(
            $"Broker does not support {apiKey} (API key {(short)apiKey}) for client " +
            $"[{clientMinVersion}, {clientMaxVersion}]: broker [{brokerMinVersion}, {brokerMaxVersion}].");
}
