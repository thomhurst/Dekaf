namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeUserScramCredentials request (API key 50).
/// Describes SCRAM credentials for users.
/// </summary>
public sealed class DescribeUserScramCredentialsRequest : IKafkaRequest<DescribeUserScramCredentialsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeUserScramCredentials;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The users to describe, or null/empty to describe all users.
    /// </summary>
    public IReadOnlyList<UserName>? Users { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        // Users: COMPACT_NULLABLE_ARRAY
        writer.WriteCompactNullableArray(
            Users,
            (ref KafkaProtocolWriter w, UserName u) => u.Write(ref w, version));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// User name entry for DescribeUserScramCredentials request.
/// </summary>
public sealed class UserName
{
    /// <summary>
    /// The user name.
    /// </summary>
    public required string Name { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);
        writer.WriteEmptyTaggedFields();
    }
}
