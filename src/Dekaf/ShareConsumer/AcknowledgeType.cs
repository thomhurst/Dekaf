using System.ComponentModel;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Per-record acknowledgement types for share group consumption (KIP-932).
/// Values match the Kafka wire protocol encoding in AcknowledgementBatch.AcknowledgeTypes.
/// </summary>
public enum AcknowledgeType : byte
{
    /// <summary>
    /// Internal wire protocol value for gap records. Do not use directly —
    /// gap records are never delivered to consumers.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    Gap = 0,
    Accept = 1,
    Release = 2,
    Reject = 3,
    Renew = 4
}
