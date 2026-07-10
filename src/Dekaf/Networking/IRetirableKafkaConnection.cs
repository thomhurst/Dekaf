namespace Dekaf.Networking;

/// <summary>
/// Coordinates pool leases and active sends while a connection leaves pool routing.
/// </summary>
internal interface IRetirableKafkaConnection
{
    int LeaseCount { get; }

    int ActiveOperationCount { get; }

    bool TryAcquireLease();

    void ReleaseLease();

    void BeginRetirement();

    void CompleteRetirement();
}
