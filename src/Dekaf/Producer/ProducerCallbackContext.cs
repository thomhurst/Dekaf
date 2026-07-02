namespace Dekaf.Producer;

internal static class ProducerCallbackContext
{
    [ThreadStatic]
    private static bool t_isInDeliveryCallback;

    internal static bool IsInDeliveryCallback => t_isInDeliveryCallback;

    internal static void Invoke(
        Action<RecordMetadata, Exception?> callback,
        RecordMetadata metadata,
        Exception? exception)
    {
        var previous = t_isInDeliveryCallback;
        t_isInDeliveryCallback = true;
        try
        {
            callback(metadata, exception);
        }
        finally
        {
            t_isInDeliveryCallback = previous;
        }
    }
}
