using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedSecurityMutationAdminClient
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AclCreationOutcome>> CreateAclsDetailedAsync(
        IEnumerable<AclBinding> aclBindings, CreateAclsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedAcls(aclBindings);
        var results = await ExecuteInMemoryMutationAsync(items, static item => item.Index, static _ => (null, null),
            static _ => ErrorCode.None, options?.TimeoutMs ?? 30000, cancellationToken,
            applyFault: (item, token) => ApplyAdminAclResourceFaultAsync(item.Binding.Pattern.Type, item.Binding.Pattern.Name, token)).ConfigureAwait(false);
        return AdminClient.BuildAclOutcomes(items, results);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> AlterUserScramCredentialsDetailedAsync(
        IEnumerable<UserScramCredentialAlteration> alterations, AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedScram(alterations);
        // The existing simulator does not store or authenticate SCRAM credentials.
        // Apply faults once per user, never independently per mechanism.
        return ExecuteInMemoryMutationAsync(items, static item => item.User, static _ => (null, null),
            static _ => ErrorCode.None, options?.TimeoutMs ?? 30000, cancellationToken);
    }
}
