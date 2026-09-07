using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IClassicGroupDescriptionAdminClient
{
    public ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> DescribeClassicGroupsAsync(
        IEnumerable<string> groupIds, DescribeClassicGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var groups = ValidateClassicGroupIds(groupIds, cancellationToken);
        var opts = options ?? new DescribeClassicGroupsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (groups.Length == 0)
            return new(new Dictionary<string, ClassicGroupDescriptionResult>(StringComparer.Ordinal));
        return ExecuteWithTimeoutAsync(
            token => DescribeClassicGroupsCoreAsync(groups, opts.IncludeAuthorizedOperations, token),
            opts.TimeoutMs, nameof(DescribeClassicGroupsAsync), cancellationToken);
    }

    internal static string[] ValidateClassicGroupIds(IEnumerable<string> groupIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        var groups = groupIds.ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(groupIds));
            if (!seen.Add(group))
                throw new ArgumentException($"Group ID '{group}' is duplicated.", nameof(groupIds));
        }
        return groups;
    }

    private async ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> DescribeClassicGroupsCoreAsync(
        string[] groupIds, bool includeAuthorizedOperations, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var results = new Dictionary<string, ClassicGroupDescriptionResult>(groupIds.Length, StringComparer.Ordinal);
        var retryErrors = new Dictionary<string, ClassicGroupDescriptionResult>(StringComparer.Ordinal);
        try
        {
            await WithRetryAsync(async () =>
            {
                retryErrors.Clear();
                Exception? retryFailure = null;
                var batches = new Dictionary<int, List<string>>();
                foreach (var groupId in groupIds)
                {
                    if (results.ContainsKey(groupId))
                        continue;
                    try
                    {
                        var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
                        if (!batches.TryGetValue(coordinatorId, out var batch))
                            batches.Add(coordinatorId, batch = []);
                        batch.Add(groupId);
                    }
                    catch (KafkaException exception) when (exception.ErrorCode is { } code &&
                        !RetryHelper.IsRetriableRequestFailure(exception))
                    {
                        results[groupId] = ClassicGroupError(groupId, code, exception.Message);
                    }
                    catch (Exception exception) when (RetryHelper.IsRetriableRequestFailure(exception) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        retryErrors[groupId] = ClassicGroupError(groupId, GetRetryErrorCode(exception), exception.Message);
                        retryFailure ??= exception;
                    }
                }

                foreach (var (coordinatorId, batch) in batches)
                {
                    try
                    {
                        using var lease = await _connectionPool.LeaseConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);
                        var connection = lease.Connection;
                        var version = _metadataManager.GetNegotiatedApiVersion(connection, ApiKey.DescribeGroups,
                            DescribeGroupsRequest.LowestSupportedVersion, DescribeGroupsRequest.HighestSupportedVersion);
                        var response = await connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
                            new() { Groups = batch, IncludeAuthorizedOperations = includeAuthorizedOperations },
                            version, cancellationToken).ConfigureAwait(false);
                        var missing = new HashSet<string>(batch, StringComparer.Ordinal);
                        foreach (var group in response.Groups)
                        {
                            if (!missing.Remove(group.GroupId))
                                continue;
                            if (group.ErrorCode.IsRetriable())
                            {
                                retryErrors[group.GroupId] = ClassicGroupError(group.GroupId, group.ErrorCode, group.ErrorMessage);
                                retryFailure ??= new GroupException(group.ErrorCode,
                                    group.ErrorMessage ?? $"DescribeClassicGroups failed: {group.ErrorCode}") { GroupId = group.GroupId };
                            }
                            else
                            {
                                results[group.GroupId] = group.ErrorCode == ErrorCode.None
                                    ? MapClassicGroup(group, coordinatorId, includeAuthorizedOperations)
                                    : ClassicGroupError(group.GroupId, group.ErrorCode, group.ErrorMessage);
                            }
                        }
                        foreach (var groupId in missing)
                            results[groupId] = ClassicGroupError(groupId, ErrorCode.UnknownServerError,
                                "DescribeGroups omitted the requested group.");
                    }
                    catch (KafkaException exception) when (exception.ErrorCode is { } code &&
                        !RetryHelper.IsRetriableRequestFailure(exception))
                    {
                        foreach (var groupId in batch)
                            results.TryAdd(groupId, ClassicGroupError(groupId, code, exception.Message));
                    }
                    catch (Exception exception) when (RetryHelper.IsRetriableRequestFailure(exception) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        foreach (var groupId in batch)
                            if (!results.ContainsKey(groupId))
                                retryErrors[groupId] = ClassicGroupError(groupId, GetRetryErrorCode(exception), exception.Message);
                        retryFailure ??= exception;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (retryFailure is not null)
                    throw retryFailure;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
            RetryHelper.IsRetriableRequestFailure(exception))
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception) when (RetryHelper.IsRetriableRequestFailure(exception) &&
            !cancellationToken.IsCancellationRequested)
        {
            foreach (var groupId in groupIds)
                if (!results.ContainsKey(groupId))
                    results[groupId] = retryErrors.TryGetValue(groupId, out var error)
                        ? error : ClassicGroupError(groupId, GetRetryErrorCode(exception), exception.Message);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }

    internal static ClassicGroupDescriptionResult MapClassicGroup(DescribeGroupsResponseGroup group,
        int coordinatorId, bool includeAuthorizedOperations)
    {
        var members = new ClassicGroupMemberDescription[group.Members.Count];
        var consumer = string.Equals(group.ProtocolType, "consumer", StringComparison.Ordinal);
        for (var i = 0; i < members.Length; i++)
        {
            var member = group.Members[i];
            members[i] = new ClassicGroupMemberDescription
            {
                MemberId = member.MemberId, GroupInstanceId = member.GroupInstanceId,
                ClientId = member.ClientId, ClientHost = member.ClientHost,
                Metadata = member.MemberMetadata, AssignmentData = member.MemberAssignment,
                Assignment = consumer ? ParseMemberAssignment(member.MemberAssignment) : null
            };
        }
        return new ClassicGroupDescriptionResult
        {
            GroupId = group.GroupId,
            Description = new ClassicGroupDescription
            {
                GroupId = group.GroupId, ProtocolType = group.ProtocolType, ProtocolData = group.ProtocolData,
                State = group.GroupState, CoordinatorId = coordinatorId, Members = members,
                AuthorizedOperations = includeAuthorizedOperations && group.AuthorizedOperations != int.MinValue
                    ? group.AuthorizedOperations : null
            }
        };
    }

    internal static ClassicGroupDescriptionResult ClassicGroupError(string groupId, ErrorCode code, string? message = null) =>
        new() { GroupId = groupId, ErrorCode = code, ErrorMessage = message };
}
