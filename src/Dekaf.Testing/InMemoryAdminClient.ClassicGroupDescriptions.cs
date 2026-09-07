using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IClassicGroupDescriptionAdminClient
{
    public ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> DescribeClassicGroupsAsync(
        IEnumerable<string> groupIds, DescribeClassicGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var groups = AdminClient.ValidateClassicGroupIds(groupIds, cancellationToken);
        ThrowIfDisposed();
        var opts = options ?? new DescribeClassicGroupsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        if (groups.Length == 0)
            return new(new Dictionary<string, ClassicGroupDescriptionResult>(StringComparer.Ordinal));
        return ExecuteWithTimeoutAsync<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>>(async token =>
        {
            var results = new Dictionary<string, ClassicGroupDescriptionResult>(groups.Length, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                try
                {
                    await ApplyAdminFaultAsync(token, groupId: group).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    results.Add(group, _cluster.DescribeClassicGroup(group, opts.IncludeAuthorizedOperations));
                }
                catch (KafkaException exception) when (exception.ErrorCode is { } code &&
                    !token.IsCancellationRequested)
                {
                    results.Add(group, AdminClient.ClassicGroupError(group, code, exception.Message));
                }
            }
            return results;
        }, opts.TimeoutMs, nameof(DescribeClassicGroupsAsync), cancellationToken);
    }
}
