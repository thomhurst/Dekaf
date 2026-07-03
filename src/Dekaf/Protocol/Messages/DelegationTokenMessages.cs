namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateDelegationToken request (API key 38).
/// </summary>
public sealed class CreateDelegationTokenRequest : IKafkaRequest<CreateDelegationTokenResponse>
{
    public static ApiKey ApiKey => ApiKey.CreateDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 3;

    public DelegationTokenPrincipalData? Owner { get; init; }
    public IReadOnlyList<DelegationTokenPrincipalData>? Renewers { get; init; }
    public long MaxLifetimeMs { get; init; } = -1;

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var flexible = IsFlexibleVersion(version);

        if (version >= 3)
        {
            DelegationTokenCodec.WriteString(ref writer, Owner?.PrincipalType, flexible);
            DelegationTokenCodec.WriteString(ref writer, Owner?.PrincipalName, flexible);
        }

        WriteArray(ref writer, Renewers ?? [], flexible);
        writer.WriteInt64(MaxLifetimeMs);

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }

    private static void WriteArray(
        ref KafkaProtocolWriter writer,
        IReadOnlyList<DelegationTokenPrincipalData> principals,
        bool flexible)
    {
        if (flexible)
        {
            writer.WriteCompactArray(
                principals,
                static (ref KafkaProtocolWriter w, DelegationTokenPrincipalData p, bool f) => p.Write(ref w, f),
                true);
        }
        else
        {
            writer.WriteArray(
                principals,
                static (ref KafkaProtocolWriter w, DelegationTokenPrincipalData p, bool f) => p.Write(ref w, f),
                false);
        }
    }
}

internal static class DelegationTokenCodec
{
    internal static void WriteString(ref KafkaProtocolWriter writer, string? value, bool flexible)
    {
        if (flexible)
        {
            writer.WriteCompactNullableString(value);
        }
        else
        {
            writer.WriteString(value);
        }
    }

    internal static void WriteBytes(ref KafkaProtocolWriter writer, ReadOnlySpan<byte> value, bool flexible)
    {
        if (flexible)
        {
            writer.WriteCompactBytes(value);
        }
        else
        {
            writer.WriteBytes(value);
        }
    }

    internal static string ReadString(ref KafkaProtocolReader reader, bool flexible)
        => (flexible ? reader.ReadCompactString() : reader.ReadString()) ?? string.Empty;

    internal static byte[] ReadBytes(ref KafkaProtocolReader reader, bool flexible)
        => (flexible ? reader.ReadCompactBytes() : reader.ReadBytes()) ?? [];
}

/// <summary>
/// CreateDelegationToken response (API key 38).
/// </summary>
public sealed class CreateDelegationTokenResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.CreateDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 3;

    public ErrorCode ErrorCode { get; init; }
    public required string PrincipalType { get; init; }
    public required string PrincipalName { get; init; }
    public string? TokenRequesterPrincipalType { get; init; }
    public string? TokenRequesterPrincipalName { get; init; }
    public long IssueTimestampMs { get; init; }
    public long ExpiryTimestampMs { get; init; }
    public long MaxTimestampMs { get; init; }
    public required string TokenId { get; init; }
    public required byte[] Hmac { get; init; }
    public int ThrottleTimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = CreateDelegationTokenRequest.IsFlexibleVersion(version);
        var errorCode = (ErrorCode)reader.ReadInt16();
        var principalType = DelegationTokenCodec.ReadString(ref reader, flexible);
        var principalName = DelegationTokenCodec.ReadString(ref reader, flexible);
        string? tokenRequesterPrincipalType = null;
        string? tokenRequesterPrincipalName = null;

        if (version >= 3)
        {
            tokenRequesterPrincipalType = DelegationTokenCodec.ReadString(ref reader, flexible);
            tokenRequesterPrincipalName = DelegationTokenCodec.ReadString(ref reader, flexible);
        }

        var issueTimestampMs = reader.ReadInt64();
        var expiryTimestampMs = reader.ReadInt64();
        var maxTimestampMs = reader.ReadInt64();
        var tokenId = DelegationTokenCodec.ReadString(ref reader, flexible);
        var hmac = DelegationTokenCodec.ReadBytes(ref reader, flexible);
        var throttleTimeMs = reader.ReadInt32();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new CreateDelegationTokenResponse
        {
            ErrorCode = errorCode,
            PrincipalType = principalType,
            PrincipalName = principalName,
            TokenRequesterPrincipalType = tokenRequesterPrincipalType,
            TokenRequesterPrincipalName = tokenRequesterPrincipalName,
            IssueTimestampMs = issueTimestampMs,
            ExpiryTimestampMs = expiryTimestampMs,
            MaxTimestampMs = maxTimestampMs,
            TokenId = tokenId,
            Hmac = hmac,
            ThrottleTimeMs = throttleTimeMs
        };
    }
}

/// <summary>
/// RenewDelegationToken request (API key 39).
/// </summary>
public sealed class RenewDelegationTokenRequest : IKafkaRequest<RenewDelegationTokenResponse>
{
    public static ApiKey ApiKey => ApiKey.RenewDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public required byte[] Hmac { get; init; }
    public long RenewPeriodMs { get; init; } = -1;

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var flexible = IsFlexibleVersion(version);
        DelegationTokenCodec.WriteBytes(ref writer, Hmac, flexible);
        writer.WriteInt64(RenewPeriodMs);

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// RenewDelegationToken response (API key 39).
/// </summary>
public sealed class RenewDelegationTokenResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.RenewDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public ErrorCode ErrorCode { get; init; }
    public long ExpiryTimestampMs { get; init; }
    public int ThrottleTimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = RenewDelegationTokenRequest.IsFlexibleVersion(version);
        var errorCode = (ErrorCode)reader.ReadInt16();
        var expiryTimestampMs = reader.ReadInt64();
        var throttleTimeMs = reader.ReadInt32();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new RenewDelegationTokenResponse
        {
            ErrorCode = errorCode,
            ExpiryTimestampMs = expiryTimestampMs,
            ThrottleTimeMs = throttleTimeMs
        };
    }
}

/// <summary>
/// ExpireDelegationToken request (API key 40).
/// </summary>
public sealed class ExpireDelegationTokenRequest : IKafkaRequest<ExpireDelegationTokenResponse>
{
    public static ApiKey ApiKey => ApiKey.ExpireDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public required byte[] Hmac { get; init; }
    public long ExpiryTimePeriodMs { get; init; } = -1;

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var flexible = IsFlexibleVersion(version);
        DelegationTokenCodec.WriteBytes(ref writer, Hmac, flexible);
        writer.WriteInt64(ExpiryTimePeriodMs);

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// ExpireDelegationToken response (API key 40).
/// </summary>
public sealed class ExpireDelegationTokenResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ExpireDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public ErrorCode ErrorCode { get; init; }
    public long ExpiryTimestampMs { get; init; }
    public int ThrottleTimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = ExpireDelegationTokenRequest.IsFlexibleVersion(version);
        var errorCode = (ErrorCode)reader.ReadInt16();
        var expiryTimestampMs = reader.ReadInt64();
        var throttleTimeMs = reader.ReadInt32();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new ExpireDelegationTokenResponse
        {
            ErrorCode = errorCode,
            ExpiryTimestampMs = expiryTimestampMs,
            ThrottleTimeMs = throttleTimeMs
        };
    }
}

/// <summary>
/// DescribeDelegationToken request (API key 41).
/// </summary>
public sealed class DescribeDelegationTokenRequest : IKafkaRequest<DescribeDelegationTokenResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 3;

    public IReadOnlyList<DelegationTokenPrincipalData>? Owners { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var flexible = IsFlexibleVersion(version);

        if (flexible)
        {
            writer.WriteCompactNullableArray(
                Owners,
                static (ref KafkaProtocolWriter w, DelegationTokenPrincipalData p, bool f) => p.Write(ref w, f),
                true);
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteNullableArray(
                Owners,
                static (ref KafkaProtocolWriter w, DelegationTokenPrincipalData p, bool f) => p.Write(ref w, f),
                false);
        }
    }
}

/// <summary>
/// DescribeDelegationToken response (API key 41).
/// </summary>
public sealed class DescribeDelegationTokenResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeDelegationToken;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 3;

    public ErrorCode ErrorCode { get; init; }
    public required IReadOnlyList<DescribedDelegationTokenData> Tokens { get; init; }
    public int ThrottleTimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = DescribeDelegationTokenRequest.IsFlexibleVersion(version);
        var errorCode = (ErrorCode)reader.ReadInt16();
        var tokens = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribedDelegationTokenData.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribedDelegationTokenData.Read(ref r, v),
                version);
        var throttleTimeMs = reader.ReadInt32();

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeDelegationTokenResponse
        {
            ErrorCode = errorCode,
            Tokens = tokens,
            ThrottleTimeMs = throttleTimeMs
        };
    }
}

/// <summary>
/// Kafka principal data used by delegation token protocol messages.
/// </summary>
public sealed class DelegationTokenPrincipalData
{
    public required string PrincipalType { get; init; }
    public required string PrincipalName { get; init; }

    public void Write(ref KafkaProtocolWriter writer, bool flexible)
    {
        DelegationTokenCodec.WriteString(ref writer, PrincipalType, flexible);
        DelegationTokenCodec.WriteString(ref writer, PrincipalName, flexible);

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }

    public static DelegationTokenPrincipalData Read(ref KafkaProtocolReader reader, bool flexible)
    {
        var principalType = DelegationTokenCodec.ReadString(ref reader, flexible);
        var principalName = DelegationTokenCodec.ReadString(ref reader, flexible);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DelegationTokenPrincipalData
        {
            PrincipalType = principalType,
            PrincipalName = principalName
        };
    }
}

/// <summary>
/// Token data returned by DescribeDelegationToken.
/// </summary>
public sealed class DescribedDelegationTokenData
{
    public required string PrincipalType { get; init; }
    public required string PrincipalName { get; init; }
    public string? TokenRequesterPrincipalType { get; init; }
    public string? TokenRequesterPrincipalName { get; init; }
    public long IssueTimestampMs { get; init; }
    public long ExpiryTimestampMs { get; init; }
    public long MaxTimestampMs { get; init; }
    public required string TokenId { get; init; }
    public required byte[] Hmac { get; init; }
    public required IReadOnlyList<DelegationTokenPrincipalData> Renewers { get; init; }

    public static DescribedDelegationTokenData Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = DescribeDelegationTokenRequest.IsFlexibleVersion(version);
        var principalType = DelegationTokenCodec.ReadString(ref reader, flexible);
        var principalName = DelegationTokenCodec.ReadString(ref reader, flexible);
        string? tokenRequesterPrincipalType = null;
        string? tokenRequesterPrincipalName = null;

        if (version >= 3)
        {
            tokenRequesterPrincipalType = DelegationTokenCodec.ReadString(ref reader, flexible);
            tokenRequesterPrincipalName = DelegationTokenCodec.ReadString(ref reader, flexible);
        }

        var issueTimestampMs = reader.ReadInt64();
        var expiryTimestampMs = reader.ReadInt64();
        var maxTimestampMs = reader.ReadInt64();
        var tokenId = DelegationTokenCodec.ReadString(ref reader, flexible);
        var hmac = DelegationTokenCodec.ReadBytes(ref reader, flexible);
        var renewers = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, bool f) => DelegationTokenPrincipalData.Read(ref r, f),
                true)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, bool f) => DelegationTokenPrincipalData.Read(ref r, f),
                false);

        if (flexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribedDelegationTokenData
        {
            PrincipalType = principalType,
            PrincipalName = principalName,
            TokenRequesterPrincipalType = tokenRequesterPrincipalType,
            TokenRequesterPrincipalName = tokenRequesterPrincipalName,
            IssueTimestampMs = issueTimestampMs,
            ExpiryTimestampMs = expiryTimestampMs,
            MaxTimestampMs = maxTimestampMs,
            TokenId = tokenId,
            Hmac = hmac,
            Renewers = renewers
        };
    }
}
