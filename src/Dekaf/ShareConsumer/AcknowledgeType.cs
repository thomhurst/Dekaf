namespace Dekaf.ShareConsumer;

/// <summary>
/// Per-record acknowledgement types for share group consumption (KIP-932).
/// Values match the Kafka wire protocol encoding in AcknowledgementBatch.AcknowledgeTypes.
/// </summary>
public enum AcknowledgeType : byte
{
    Gap = 0,
    Accept = 1,
    Release = 2,
    Reject = 3,
    Renew = 4
}
