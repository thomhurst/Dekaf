namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateAcls request (API key 30).
/// Creates one or more ACLs.
/// </summary>
public sealed class CreateAclsRequest : IKafkaRequest<CreateAclsResponse>
{
    public static ApiKey ApiKey => ApiKey.CreateAcls;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The ACLs to create.
    /// </summary>
    public required IReadOnlyList<AclCreation> Creations { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Creations,
                static (ref KafkaProtocolWriter w, AclCreation c, short v) => c.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Creations,
                static (ref KafkaProtocolWriter w, AclCreation c, short v) => c.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// An ACL creation entry.
/// </summary>
public sealed class AclCreation
{
    /// <summary>
    /// The resource type.
    /// </summary>
    public sbyte ResourceType { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// The resource pattern type (v1+).
    /// </summary>
    public sbyte ResourcePatternType { get; init; } = 3; // Literal

    /// <summary>
    /// The principal.
    /// </summary>
    public required string Principal { get; init; }

    /// <summary>
    /// The host.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The operation.
    /// </summary>
    public sbyte Operation { get; init; }

    /// <summary>
    /// The permission type.
    /// </summary>
    public sbyte PermissionType { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        writer.WriteInt8(ResourceType);

        if (isFlexible)
            writer.WriteCompactString(ResourceName);
        else
            writer.WriteString(ResourceName);

        if (version >= 1)
        {
            writer.WriteInt8(ResourcePatternType);
        }

        if (isFlexible)
        {
            writer.WriteCompactString(Principal);
            writer.WriteCompactString(Host);
        }
        else
        {
            writer.WriteString(Principal);
            writer.WriteString(Host);
        }

        writer.WriteInt8(Operation);
        writer.WriteInt8(PermissionType);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
