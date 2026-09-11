using Dekaf.Protocol;

namespace Dekaf.Admin;

/// <summary>The evidence available for one requested administrative mutation.</summary>
public enum AdminMutationOutcome
{
    /// <summary>The request may have applied, but no definitive response is available.</summary>
    Unknown = 0,
    /// <summary>The broker confirmed success (or successful validation for validate-only requests).</summary>
    Succeeded = 1,
    /// <summary>The broker returned a definitive error for this entity.</summary>
    Failed = 2,
    /// <summary>No mutation request was sent for this entity.</summary>
    NotAttempted = 3
}

/// <summary>A confirmed or uncertain outcome for one administrative mutation.</summary>
public sealed class AdminMutationResult
{
    public required AdminMutationOutcome Outcome { get; init; }
    /// <summary>The broker error code, when a response was received; otherwise null.</summary>
    public ErrorCode? ErrorCode { get; init; }
    /// <summary>The original broker message, or an explanation for a missing response.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>A local discovery, transport, cancellation or timeout failure, when present.</summary>
    public Exception? Exception { get; init; }
    public bool IsSuccess => Outcome == AdminMutationOutcome.Succeeded;

    internal static AdminMutationResult FromResponse(ErrorCode code, string? message) => new()
    {
        Outcome = code switch
        {
            Protocol.ErrorCode.None => AdminMutationOutcome.Succeeded,
            Protocol.ErrorCode.RequestTimedOut or Protocol.ErrorCode.NetworkException or
                Protocol.ErrorCode.UnknownServerError or Protocol.ErrorCode.LeaderNotAvailable => AdminMutationOutcome.Unknown,
            _ => AdminMutationOutcome.Failed
        },
        ErrorCode = code,
        ErrorMessage = message
    };

    internal static AdminMutationResult Unconfirmed(AdminMutationOutcome outcome, string message, Exception? exception = null) => new()
    {
        Outcome = outcome,
        ErrorMessage = message,
        Exception = exception
    };

    internal static bool IsSafeControllerRetry(AdminMutationResult result) =>
        result.Outcome == AdminMutationOutcome.Failed &&
        result.ErrorCode is { } code && code.IsRetriable() &&
        code is Protocol.ErrorCode.NotController or Protocol.ErrorCode.ThrottlingQuotaExceeded;

    internal static bool IsSafeCoordinatorRetry(AdminMutationResult result) =>
        result.Outcome == AdminMutationOutcome.Failed && result.ErrorCode is { } code && code.IsRetriable() &&
        code is Protocol.ErrorCode.NotCoordinator or Protocol.ErrorCode.CoordinatorNotAvailable or Protocol.ErrorCode.CoordinatorLoadInProgress;

}
