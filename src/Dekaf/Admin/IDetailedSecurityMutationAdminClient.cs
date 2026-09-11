namespace Dekaf.Admin;

/// <summary>Optional capability for detailed ACL and SCRAM mutation outcomes.</summary>
/// <remarks>
/// Confirmed results survive partial failure. Only confirmed controller/quota rejections
/// may be retried; ambiguous sends are never replayed. TimeoutMs bounds discovery, sends
/// and retries. Pre-cancellation throws; cancellation during execution returns outcomes.
/// Existing convenience APIs retain their exception behavior.
/// </remarks>
public interface IDetailedSecurityMutationAdminClient
{
    /// <summary>Returns one result per input ACL occurrence, in input order, including duplicates.</summary>
    ValueTask<IReadOnlyList<AclCreationOutcome>> CreateAclsDetailedAsync(
        IEnumerable<AclBinding> aclBindings, CreateAclsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Returns one result per user; all of a user's mechanisms are altered atomically.</summary>
    /// <remarks>Duplicate alterations of the same user/mechanism are rejected before dispatch.</remarks>
    ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> AlterUserScramCredentialsDetailedAsync(
        IEnumerable<UserScramCredentialAlteration> alterations, AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The requested ACL binding and its confirmed or unconfirmed creation outcome.</summary>
public sealed class AclCreationOutcome
{
    /// <summary>The original immutable binding at this position in the input.</summary>
    public required AclBinding Binding { get; init; }

    /// <summary>The outcome for this occurrence of the binding.</summary>
    public required AdminMutationResult Result { get; init; }
}

/// <summary>Detailed security mutations for compatible <see cref="IAdminClient"/> implementations.</summary>
public static class AdminClientDetailedSecurityMutationExtensions
{
    public static ValueTask<IReadOnlyList<AclCreationOutcome>> CreateAclsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<AclBinding> aclBindings, CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).CreateAclsDetailedAsync(aclBindings, options, cancellationToken);

    public static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> AlterUserScramCredentialsDetailedAsync(
        this IAdminClient adminClient, IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterUserScramCredentialsDetailedAsync(alterations, options, cancellationToken);

    private static IDetailedSecurityMutationAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient as IDetailedSecurityMutationAdminClient
            ?? throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support detailed security mutations.");
    }
}
