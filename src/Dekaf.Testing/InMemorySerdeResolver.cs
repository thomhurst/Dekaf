using Dekaf.Serialization;

namespace Dekaf.Testing;

internal static class InMemorySerdeResolver
{
    public static ISerializer<T> Serializer<T>()
    {
        return Serde<T>();
    }

    public static IDeserializer<T> Deserializer<T>()
    {
        return Serde<T>();
    }

    private static ISerde<T> Serde<T>()
    {
        var type = typeof(T);

        if (type == typeof(string))
            return (ISerde<T>)Serializers.String;
        if (type == typeof(byte[]))
            return (ISerde<T>)Serializers.ByteArray;
        if (type == typeof(ReadOnlyMemory<byte>))
            return (ISerde<T>)Serializers.RawBytes;
        if (type == typeof(int))
            return (ISerde<T>)Serializers.Int32;
        if (type == typeof(long))
            return (ISerde<T>)Serializers.Int64;
        if (type == typeof(Guid))
            return (ISerde<T>)Serializers.Guid;
        if (type == typeof(double))
            return (ISerde<T>)Serializers.Double;
        if (type == typeof(float))
            return (ISerde<T>)Serializers.Float;
        if (type == typeof(DateTime))
            return (ISerde<T>)Serializers.DateTime;
        if (type == typeof(DateTimeOffset))
            return (ISerde<T>)Serializers.DateTimeOffset;
        if (type == typeof(TimeSpan))
            return (ISerde<T>)Serializers.TimeSpan;
        if (type == typeof(Ignore))
            return (ISerde<T>)Serializers.Ignore;

        throw new InvalidOperationException(
            $"No built-in Dekaf serializer is available for {type.FullName}. Register ISerializer<{type.Name}>/IDeserializer<{type.Name}> or pass one to the in-memory client constructor.");
    }
}
