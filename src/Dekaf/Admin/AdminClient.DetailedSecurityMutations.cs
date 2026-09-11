using System.Security.Cryptography;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedSecurityMutationAdminClient
{
    internal readonly record struct IndexedAcl(int Index, AclBinding Binding);
    internal sealed record ScramUserMutation(string User, List<UserScramCredentialAlteration> Alterations);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AclCreationOutcome>> CreateAclsDetailedAsync(
        IEnumerable<AclBinding> aclBindings, CreateAclsOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var items = SnapshotDetailedAcls(aclBindings);
        var results = await ExecuteDetailedMutationAsync<int, IndexedAcl, CreateAclsRequest, CreateAclsResponse>(
            items, static item => item.Index,
            new(ApiKey.CreateAcls, CreateAclsRequest.LowestSupportedVersion, CreateAclsRequest.HighestSupportedVersion, nameof(CreateAclsAsync)),
            timeout, static (pending, _) => new() { Creations = pending.Select(static item => new AclCreation
            {
                ResourceType = (sbyte)item.Binding.Pattern.Type, ResourceName = item.Binding.Pattern.Name,
                ResourcePatternType = (sbyte)item.Binding.Pattern.PatternType, Principal = item.Binding.Entry.Principal,
                Host = item.Binding.Entry.Host, Operation = (sbyte)item.Binding.Entry.Operation,
                PermissionType = (sbyte)item.Binding.Entry.Permission
            }).ToArray() },
            static (pending, response) =>
            {
                var mapped = new Dictionary<int, AdminMutationResult>(pending.Count);
                for (var index = 0; index < pending.Count; index++)
                {
                    // ACL responses have no identity field. A malformed count destroys
                    // the positional association; never attribute another ACL's success.
                    mapped.Add(pending[index].Index, response.Results.Count == pending.Count
                        ? AdminMutationResult.FromResponse(response.Results[index].ErrorCode, response.Results[index].ErrorMessage)
                        : AdminMutationResult.Unconfirmed(AdminMutationOutcome.Unknown, "The ACL response count did not match the request."));
                }
                return mapped;
            }, cancellationToken).ConfigureAwait(false);
        return BuildAclOutcomes(items, results);
    }

    internal static IReadOnlyList<AclCreationOutcome> BuildAclOutcomes(List<IndexedAcl> items,
        IReadOnlyDictionary<int, AdminMutationResult> results) =>
        items.Select(item => new AclCreationOutcome { Binding = item.Binding, Result = results[item.Index] }).ToArray();

    internal static List<IndexedAcl> SnapshotDetailedAcls(IEnumerable<AclBinding> aclBindings)
    {
        ArgumentNullException.ThrowIfNull(aclBindings);
        var items = new List<IndexedAcl>();
        foreach (var binding in aclBindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(binding.Pattern);
            ArgumentNullException.ThrowIfNull(binding.Entry);
            ArgumentException.ThrowIfNullOrEmpty(binding.Pattern.Name);
            ArgumentException.ThrowIfNullOrEmpty(binding.Entry.Principal);
            ArgumentException.ThrowIfNullOrEmpty(binding.Entry.Host);
            if (binding.Pattern.Type is < ResourceType.Topic or > ResourceType.DelegationToken
                || binding.Pattern.PatternType is not (PatternType.Literal or PatternType.Prefixed)
                || binding.Entry.Operation is < AclOperation.All or > AclOperation.IdempotentWrite
                || binding.Entry.Permission is not (AclPermissionType.Allow or AclPermissionType.Deny))
                throw new ArgumentException("ACL creation requires concrete resource, pattern, operation and permission types.", nameof(aclBindings));
            items.Add(new(items.Count, binding));
        }
        return items;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> AlterUserScramCredentialsDetailedAsync(
        IEnumerable<UserScramCredentialAlteration> alterations, AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timeout = options?.TimeoutMs ?? 30000;
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        var items = SnapshotDetailedScram(alterations);
        // Derive once per administrative upsertion, before any asynchronous dispatch.
        // Confirmed-rejection retries reuse the exact salt and salted password.
        var prepared = items.Select(item => new PreparedScramUser(item.User,
            item.Alterations.OfType<UserScramCredentialDeletion>().Select(static deletion => new ScramCredentialDeletion
            { Name = deletion.User, Mechanism = (byte)deletion.Mechanism }).ToArray(),
            item.Alterations.OfType<UserScramCredentialUpsertion>().Select(static upsertion =>
            {
                var salt = upsertion.Salt ?? RandomNumberGenerator.GetBytes(32);
                return new ScramCredentialUpsertion
                {
                    Name = upsertion.User, Mechanism = (byte)upsertion.Mechanism, Iterations = upsertion.Iterations,
                    Salt = salt, SaltedPassword = ComputeSaltedPassword(upsertion.Password, salt, upsertion.Iterations, upsertion.Mechanism)
                };
            }).ToArray())).ToList();
        return ExecuteDetailedMutationAsync<string, PreparedScramUser, AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
            prepared, static item => item.User,
            new(ApiKey.AlterUserScramCredentials, AlterUserScramCredentialsRequest.LowestSupportedVersion,
                AlterUserScramCredentialsRequest.HighestSupportedVersion, nameof(AlterUserScramCredentialsAsync)),
            timeout, static (pending, _) => new()
            {
                Deletions = pending.SelectMany(static item => item.Deletions).ToArray(),
                Upsertions = pending.SelectMany(static item => item.Upsertions).ToArray()
            }, static (_, response) => MapMutationResults(response.Results, static item => item.User,
                static item => item.ErrorCode, static item => item.ErrorMessage), cancellationToken);
    }

    private sealed record PreparedScramUser(string User, ScramCredentialDeletion[] Deletions, ScramCredentialUpsertion[] Upsertions);

    internal static List<ScramUserMutation> SnapshotDetailedScram(IEnumerable<UserScramCredentialAlteration> alterations)
    {
        ArgumentNullException.ThrowIfNull(alterations);
        var groups = new Dictionary<string, ScramUserMutation>(StringComparer.Ordinal);
        var identities = new HashSet<(string User, ScramMechanism Mechanism)>();
        foreach (var alteration in alterations)
        {
            ArgumentNullException.ThrowIfNull(alteration);
            ArgumentException.ThrowIfNullOrEmpty(alteration.User);
            var mechanism = alteration switch
            {
                UserScramCredentialDeletion deletion => deletion.Mechanism,
                UserScramCredentialUpsertion upsertion => upsertion.Mechanism,
                _ => throw new ArgumentException("Unsupported SCRAM alteration type.", nameof(alterations))
            };
            if (mechanism is not (ScramMechanism.ScramSha256 or ScramMechanism.ScramSha512))
                throw new ArgumentException("Unsupported SCRAM mechanism.", nameof(alterations));
            if (!identities.Add((alteration.User, mechanism)))
                throw new ArgumentException("A user/mechanism may only be altered once per request.", nameof(alterations));
            UserScramCredentialAlteration snapshot = alteration;
            if (alteration is UserScramCredentialUpsertion upsert)
            {
                ArgumentException.ThrowIfNullOrEmpty(upsert.Password);
                ArgumentOutOfRangeException.ThrowIfLessThan(upsert.Iterations, 4096);
                snapshot = new UserScramCredentialUpsertion
                {
                    User = upsert.User, Mechanism = upsert.Mechanism, Iterations = upsert.Iterations,
                    Password = upsert.Password, Salt = upsert.Salt?.ToArray()
                };
            }
            if (!groups.TryGetValue(alteration.User, out var group))
                groups.Add(alteration.User, group = new(alteration.User, []));
            group.Alterations.Add(snapshot);
        }
        return groups.Values.ToList();
    }
}
