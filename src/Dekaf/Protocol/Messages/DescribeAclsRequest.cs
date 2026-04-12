namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeAcls request (API key 29).
/// Describes ACLs matching the specified filter.
/// </summary>
public sealed class DescribeAclsRequest : IKafkaRequest<DescribeAclsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeAcls;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The resource type to filter on.
    /// </summary>
    public sbyte ResourceTypeFilter { get; init; }

    /// <summary>
    /// The resource name to filter on, or null for any.
    /// </summary>
    public string? ResourceNameFilter { get; init; }

    /// <summary>
    /// The pattern type to filter on (v1+).
    /// </summary>
    public sbyte PatternTypeFilter { get; init; } = 3; // Literal

    /// <summary>
    /// The principal to filter on, or null for any.
    /// </summary>
    public string? PrincipalFilter { get; init; }

    /// <summary>
    /// The host to filter on, or null for any.
    /// </summary>
    public string? HostFilter { get; init; }

    /// <summary>
    /// The operation to filter on.
    /// </summary>
    public sbyte Operation { get; init; }

    /// <summary>
    /// The permission type to filter on.
    /// </summary>
    public sbyte PermissionType { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        writer.WriteInt8(ResourceTypeFilter);

        if (isFlexible)
            writer.WriteCompactNullableString(ResourceNameFilter);
        else
            writer.WriteString(ResourceNameFilter);

        if (version >= 1)
        {
            writer.WriteInt8(PatternTypeFilter);
        }

        if (isFlexible)
        {
            writer.WriteCompactNullableString(PrincipalFilter);
            writer.WriteCompactNullableString(HostFilter);
        }
        else
        {
            writer.WriteString(PrincipalFilter);
            writer.WriteString(HostFilter);
        }

        writer.WriteInt8(Operation);
        writer.WriteInt8(PermissionType);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
