namespace Dekaf.Extensions.Hosting;

/// <summary>Controls hosted share consumer shutdown and acquisition renewal.</summary>
public sealed class KafkaShareConsumerServiceOptions
{
    /// <summary>Maximum shutdown wait, including processing and final acknowledgement submission. Default: 30 seconds.</summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Finishes the current delivered record on shutdown. No further records are polled. Default: true.</summary>
    public bool DrainOnShutdown { get; init; } = true;

    /// <summary>
    /// Maximum interval between renewal attempts while asynchronous work is pending. The interval
    /// is reduced to one third of the broker-reported acquisition timeout when available.
    /// Renewal requires ShareFetch/ShareAcknowledge v2. A renewal failure stops processing.
    /// </summary>
    public TimeSpan RenewalInterval { get; init; } = TimeSpan.FromSeconds(10);
}
