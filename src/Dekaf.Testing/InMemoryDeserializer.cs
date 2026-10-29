using Dekaf.Serialization;

namespace Dekaf.Testing;

internal static class InMemoryDeserializer
{
    internal static ValueTask<T> DeserializeAsync<T>(
        IAsyncDeserializer<T>? asyncDeserializer,
        IDeserializer<T> deserializer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (asyncDeserializer is not null)
            return asyncDeserializer.DeserializeAsync(data, context, cancellationToken);

        if (!DeserializerPreparation.RequiresPreparation(deserializer))
            return new(RecordHeaderDeserializer.DeserializeCallerOwned(deserializer, data, context));

        var preparer = (IAsyncDeserializerPreparer<T>)deserializer;
        return preparer.TryDeserialize(data, context, out var value)
            ? new(value)
            : PrepareAndDeserializeAsync(preparer, data, context, cancellationToken);
    }

    private static async ValueTask<T> PrepareAndDeserializeAsync<T>(
        IAsyncDeserializerPreparer<T> preparer,
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken)
    {
        // Match production's bounded retry when cached state changes during preparation.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await preparer.PrepareAsync(data, context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (preparer.TryDeserialize(data, context, out var value))
                return value;
        }

        throw new InvalidOperationException("Deserializer remained unprepared after PrepareAsync completed.");
    }
}
