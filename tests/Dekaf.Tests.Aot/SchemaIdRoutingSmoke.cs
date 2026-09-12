using System.Buffers.Binary;
using Dekaf.Errors;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;

internal static class SchemaIdRoutingSmoke
{
    public static async Task RunAsync()
    {
        var context = new SerializationContext { Topic = "aot-routes", Component = SerializationComponent.Value };
        var first = new PreparingRoute<FirstEvent>(static value => new FirstEvent(value));
        var second = new PreparingRoute<SecondEvent>(static value => new SecondEvent(value));
        var router = new SchemaIdRoutingDeserializer<RoutedEvent>()
            .Register(10, first)
            .Register(11, second)
            .Freeze();
        var preparer = (IAsyncDeserializerPreparer<RoutedEvent>)router;
        var frame = Frame(10, 42);

        Require(!preparer.TryDeserialize(frame, context, out _), "Unprepared schema route must request preparation.");
        await preparer.PrepareAsync(frame, context);
        Require(preparer.TryDeserialize(frame, context, out var result), "Prepared schema route must deserialize.");
        Require(result is FirstEvent { Value: 42 }, "Schema ID 10 must preserve the derived type and payload.");
        Require(first.LastFrame.Span.SequenceEqual(frame), "Route must receive the complete Confluent frame.");

        var secondFrame = Frame(11, 99);
        Require(!preparer.TryDeserialize(secondFrame, context, out _), "Preparation must remain route-specific.");
        await preparer.PrepareAsync(secondFrame, context);
        Require(router.Deserialize(secondFrame, context) is SecondEvent { Value: 99 }, "Schema ID 11 must select its own route.");
        Require(router.Deserialize(frame, context) is FirstEvent { Value: 42 }, "Alternating IDs must retain both routes.");

        RequireRejected(router, Frame(12, 0), context);
        RequireRejected(router, new byte[4], context);
        var badMagic = Frame(10, 1);
        badMagic[0] = 1;
        RequireRejected(router, badMagic, context);
        Console.WriteLine("Schema-ID routing NativeAOT smoke passed.");
    }

    private static byte[] Frame(int id, byte value)
    {
        var bytes = new byte[6];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1, 4), id);
        bytes[5] = value;
        return bytes;
    }

    private static void RequireRejected(SchemaIdRoutingDeserializer<RoutedEvent> router, byte[] frame, SerializationContext context)
    {
        try
        {
            router.Deserialize(frame, context);
        }
        catch (SerializationException)
        {
            return;
        }
        throw new InvalidOperationException("Invalid or unregistered schema frame was accepted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private abstract record RoutedEvent(byte Value);
    private sealed record FirstEvent(byte Value) : RoutedEvent(Value);
    private sealed record SecondEvent(byte Value) : RoutedEvent(Value);

    private sealed class PreparingRoute<T>(Func<byte, T> create) : IDeserializer<T>, IAsyncDeserializerPreparer<T>
    {
        private bool _prepared;
        public ReadOnlyMemory<byte> LastFrame { get; private set; }

        public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Require(_prepared, "Route was not prepared.");
            LastFrame = data.ToArray();
            return create(data.Span[5]);
        }

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out T result)
        {
            result = _prepared ? Deserialize(data, context) : default!;
            return _prepared;
        }

        public async ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            _prepared = true;
        }
    }
}
