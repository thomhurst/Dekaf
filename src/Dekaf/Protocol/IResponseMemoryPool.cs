namespace Dekaf.Protocol;

/// <summary>
/// Admits a response buffer before its storage is rented.
/// </summary>
internal interface IResponseMemoryPool
{
    ValueTask<IResponseMemoryReservation> ReserveAsync(
        int bytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Releases response-buffer capacity when the raw response storage is returned.
/// </summary>
internal interface IResponseMemoryReservation : IDisposable;
