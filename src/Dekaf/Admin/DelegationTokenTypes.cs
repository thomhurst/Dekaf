namespace Dekaf.Admin;

/// <summary>
/// Kafka principal used for delegation token owners, requesters, and renewers.
/// </summary>
public readonly record struct DelegationTokenPrincipal(string PrincipalType, string PrincipalName);

/// <summary>
/// Delegation token details returned by Kafka admin APIs.
/// </summary>
public sealed class DelegationToken
{
    public required DelegationTokenPrincipal Owner { get; init; }
    public DelegationTokenPrincipal? TokenRequester { get; init; }
    public required DateTimeOffset IssueTimestamp { get; init; }
    public required DateTimeOffset ExpiryTimestamp { get; init; }
    public required DateTimeOffset MaxTimestamp { get; init; }
    public required string TokenId { get; init; }
    public required byte[] Hmac { get; init; }
    public required IReadOnlyList<DelegationTokenPrincipal> Renewers { get; init; }
}
