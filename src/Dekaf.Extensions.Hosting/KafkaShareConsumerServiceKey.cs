namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Identifies the share consumer owned by a hosted share consumer service.
/// </summary>
/// <remarks>
/// Each hosted share consumer service registers its consumer as a keyed singleton under this key.
/// Resolve it to reach a specific worker's consumer, for example from a health check, even when
/// several services share the same message types and public service key:
/// <code>provider.GetRequiredKeyedService&lt;IKafkaShareConsumer&lt;string, string&gt;&gt;(KafkaShareConsumerServiceKey.For&lt;OrderWorker&gt;());</code>
/// </remarks>
/// <param name="ServiceType">The hosted service type.</param>
/// <param name="ServiceKey">The public service key, or <see langword="null"/> for an unkeyed registration.</param>
public sealed record KafkaShareConsumerServiceKey(Type ServiceType, object? ServiceKey)
{
    /// <summary>Creates the key for <typeparamref name="TService"/> registered with <paramref name="serviceKey"/>.</summary>
    /// <typeparam name="TService">The hosted service type.</typeparam>
    /// <param name="serviceKey">The public service key, or <see langword="null"/> for an unkeyed registration.</param>
    public static KafkaShareConsumerServiceKey For<TService>(object? serviceKey = null) => new(typeof(TService), serviceKey);
}
