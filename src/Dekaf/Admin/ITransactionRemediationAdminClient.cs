namespace Dekaf.Admin;

/// <summary>
/// Optional admin-client capability for forcefully terminating transactions by transactional ID.
/// </summary>
/// <remarks>
/// Dekaf's built-in and in-memory admin clients implement this interface. Custom
/// <see cref="IAdminClient"/> implementations can implement it to support
/// <see cref="AdminClientTransactionRemediationExtensions.ForceTerminateTransactionAsync"/>.
/// </remarks>
public interface ITransactionRemediationAdminClient
{
    /// <summary>
    /// Forcefully terminates the active transaction for a transactional ID.
    /// </summary>
    /// <param name="transactionalId">The transactional ID whose active transaction should be terminated.</param>
    /// <param name="options">Options controlling the termination request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The termination result.</returns>
    ValueTask<ForceTerminateTransactionResultInfo> ForceTerminateTransactionAsync(
        string transactionalId,
        ForceTerminateTransactionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Transaction-remediation operations for <see cref="IAdminClient"/>.
/// </summary>
public static class AdminClientTransactionRemediationExtensions
{
    /// <summary>
    /// Forcefully terminates the active transaction for a transactional ID.
    /// </summary>
    /// <param name="adminClient">The admin client.</param>
    /// <param name="transactionalId">The transactional ID whose active transaction should be terminated.</param>
    /// <param name="options">Options controlling the termination request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The termination result.</returns>
    /// <exception cref="NotSupportedException">
    /// The admin client does not implement <see cref="ITransactionRemediationAdminClient"/>.
    /// </exception>
    public static ValueTask<ForceTerminateTransactionResultInfo> ForceTerminateTransactionAsync(
        this IAdminClient adminClient,
        string transactionalId,
        ForceTerminateTransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);

        return adminClient is ITransactionRemediationAdminClient transactionRemediationAdminClient
            ? transactionRemediationAdminClient.ForceTerminateTransactionAsync(
                transactionalId,
                options,
                cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support transaction termination.");
    }
}
