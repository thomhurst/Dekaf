namespace Dekaf.ShareConsumer;

/// <summary>
/// Controls whether share consumer records are acknowledged implicitly or only through Acknowledge.
/// </summary>
public enum ShareAcknowledgementMode
{
    /// <summary>
    /// Delivered records are accepted automatically unless Acknowledge changes their type before commit.
    /// Equivalent to Kafka's share.acknowledgement.mode=implicit.
    /// </summary>
    Implicit = 0,

    /// <summary>
    /// Delivered records are not acknowledged until Acknowledge is called.
    /// Equivalent to Kafka's share.acknowledgement.mode=explicit.
    /// </summary>
    Explicit = 1
}
