namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteAcls request (API key 31).
/// Deletes ACLs matching the specified filters.
/// </summary>
public sealed class DeleteAclsRequest : IKafkaRequest<DeleteAclsResponse>
{
    public static ApiKey ApiKey => ApiKey.DeleteAcls;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The filters to use when deleting ACLs.
    /// </summary>
    public required IReadOnlyList<DeleteAclsFilter> Filters { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Filters,
                static (ref KafkaProtocolWriter w, DeleteAclsFilter f, short v) => f.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Filters,
                static (ref KafkaProtocolWriter w, DeleteAclsFilter f, short v) => f.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A filter for deleting ACLs.
/// </summary>
public sealed class DeleteAclsFilter
{
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
    public sbyte PatternTypeFilter { get; init; } = 1; // Any

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
