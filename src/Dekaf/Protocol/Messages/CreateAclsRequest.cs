namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateAcls request (API key 30).
/// Creates one or more ACLs.
/// </summary>
public sealed class CreateAclsRequest : IKafkaRequest<CreateAclsResponse>
{
    public static ApiKey ApiKey => ApiKey.CreateAcls;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The ACLs to create.
    /// </summary>
    public required IReadOnlyList<AclCreation> Creations { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Creations,
            static (ref KafkaProtocolWriter w, AclCreation c, short v) => c.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
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
        writer.WriteInt8(ResourceType);

        writer.WriteCompactString(ResourceName);

        writer.WriteInt8(ResourcePatternType);

        writer.WriteCompactString(Principal);
        writer.WriteCompactString(Host);

        writer.WriteInt8(Operation);
        writer.WriteInt8(PermissionType);

        writer.WriteEmptyTaggedFields();
    }
}
