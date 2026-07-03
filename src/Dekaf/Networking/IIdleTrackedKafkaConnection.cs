namespace Dekaf.Networking;

internal interface IIdleTrackedKafkaConnection
{
    long LastUsedTimestampMs { get; }

    int PendingRequestCount { get; }

    void Touch();
}
