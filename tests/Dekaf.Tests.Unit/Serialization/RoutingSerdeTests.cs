using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;

namespace Dekaf.Tests.Unit.Serialization;

public sealed class RoutingSerdeTests
{
    private static readonly AlphaEvent Alpha = new();
    private static readonly BetaEvent Beta = new();
    private static readonly EventDeserializer<AlphaEvent> AlphaDeserializer = new(Alpha);
    private static readonly EventDeserializer<BetaEvent> BetaDeserializer = new(Beta);

    [Test]
    public async Task TopicDeserializer_RoutesDerivedTypesAndFallback()
    {
        var fallback = new EventDeserializer<EventBase>(Beta);
        var router = new TopicRoutingDeserializer<EventBase>()
            .Register("alpha", AlphaDeserializer)
            .Register("beta", BetaDeserializer)
            .SetFallback(fallback)
            .Freeze();

        await Assert.That(router.IsFrozen).IsTrue();
        await Assert.That(router.Deserialize(ReadOnlyMemory<byte>.Empty, Context("alpha"))).IsSameReferenceAs(Alpha);
        await Assert.That(router.Deserialize(ReadOnlyMemory<byte>.Empty, Context("beta"))).IsSameReferenceAs(Beta);
        await Assert.That(router.Deserialize(ReadOnlyMemory<byte>.Empty, Context("other"))).IsSameReferenceAs(Beta);
    }

    [Test]
    public async Task SchemaIdDeserializer_RoutesFullConfluentFrame()
    {
        var capturing = new CapturingDeserializer(Alpha);
        var router = new SchemaIdRoutingDeserializer<EventBase>()
            .Register(42, capturing)
            .Freeze();
        var frame = Frame(42, 1, 2, 3);

        var result = router.Deserialize(frame, Context());

        await Assert.That(result).IsSameReferenceAs(Alpha);
        await Assert.That(capturing.Data.Span.SequenceEqual(frame)).IsTrue();
    }

    [Test]
    public async Task SchemaIdDeserializer_RoutesMultipleSchemaVersions()
    {
        var router = new SchemaIdRoutingDeserializer<EventBase>()
            .Register(10, AlphaDeserializer)
            .Register(11, BetaDeserializer)
            .Freeze();

        await Assert.That(router.Deserialize(Frame(10), Context())).IsSameReferenceAs(Alpha);
        await Assert.That(router.Deserialize(Frame(11), Context())).IsSameReferenceAs(Beta);
    }

    [Test]
    public async Task Routers_RejectUnknownAndMalformedRoutes()
    {
        var topicRouter = new TopicRoutingDeserializer<EventBase>().Freeze();
        var schemaRouter = new SchemaIdRoutingDeserializer<EventBase>().Freeze();

        await Assert.That(() => topicRouter.Deserialize(ReadOnlyMemory<byte>.Empty, Context("unknown")))
            .Throws<SerializationException>();
        await Assert.That(() => schemaRouter.Deserialize(new byte[] { 1, 0, 0, 0, 1 }, Context()))
            .Throws<SerializationException>();
        await Assert.That(() => schemaRouter.Deserialize(Frame(99), Context()))
            .Throws<SerializationException>();
    }

    [Test]
    public async Task Routers_RequireFreezeAndRejectLaterMutation()
    {
        var router = new TopicRoutingDeserializer<EventBase>()
            .Register("alpha", AlphaDeserializer);

        await Assert.That(() => router.Deserialize(ReadOnlyMemory<byte>.Empty, Context("alpha")))
            .Throws<InvalidOperationException>();

        router.Freeze();

        await Assert.That(() => router.Register("beta", BetaDeserializer))
            .Throws<InvalidOperationException>();
        await Assert.That(() => router.SetFallback(new EventDeserializer<EventBase>(Beta)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TopicSerializer_RoutesDerivedTypeWithoutDelegateAllocation()
    {
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", new EventSerializer<AlphaEvent>(0xA1))
            .Register("beta", new EventSerializer<BetaEvent>(0xB2))
            .Freeze();
        var alphaBuffer = new ArrayBufferWriter<byte>();
        var betaBuffer = new ArrayBufferWriter<byte>();

        router.Serialize(Alpha, ref alphaBuffer, Context("alpha"));
        router.Serialize(Beta, ref betaBuffer, Context("beta"));

        await Assert.That(alphaBuffer.WrittenSpan[0]).IsEqualTo((byte)0xA1);
        await Assert.That(betaBuffer.WrittenSpan[0]).IsEqualTo((byte)0xB2);
    }

    [Test]
    public async Task TopicSerializer_RejectsValueOfWrongDerivedType()
    {
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", new EventSerializer<AlphaEvent>(0xA1))
            .Freeze();
        var buffer = new ArrayBufferWriter<byte>();

        void SerializeWrongType() => router.Serialize(Beta, ref buffer, Context("alpha"));

        await Assert.That(SerializeWrongType).Throws<SerializationException>();
    }

    [Test]
    public async Task TypeSerializer_RoutesExactRuntimeTypeAndFallback()
    {
        var router = new TypeRoutingSerializer<EventBase>()
            .Register(new EventSerializer<AlphaEvent>(0xA1))
            .SetFallback(new EventSerializer<EventBase>(0xFF))
            .Freeze();
        var alphaBuffer = new ArrayBufferWriter<byte>();
        var betaBuffer = new ArrayBufferWriter<byte>();

        router.Serialize(Alpha, ref alphaBuffer, Context());
        router.Serialize(Beta, ref betaBuffer, Context());

        await Assert.That(alphaBuffer.WrittenSpan[0]).IsEqualTo((byte)0xA1);
        await Assert.That(betaBuffer.WrittenSpan[0]).IsEqualTo((byte)0xFF);
    }

    [Test]
    public async Task Serializers_AdvertiseNestedRecordHeaderCapability()
    {
        var nested = new TypeRoutingSerializer<EventBase>()
            .Register(new RecordHeaderEventSerializer<AlphaEvent>())
            .Freeze();
        var topicRouter = new TopicRoutingSerializer<EventBase>()
            .Register("nested", nested)
            .Freeze();
        var typeRouter = new TypeRoutingSerializer<EventBase>()
            .Register(new RecordHeaderEventSerializer<AlphaEvent>())
            .Freeze();
        var fallbackRouter = new TopicRoutingSerializer<EventBase>()
            .SetFallback(new RecordHeaderEventSerializer<EventBase>())
            .Freeze();
        var plainRouter = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", new EventSerializer<AlphaEvent>(0xA1))
            .Freeze();

        await Assert.That(((IRecordHeaderSerializer)topicRouter).ProducesRecordHeaders).IsTrue();
        await Assert.That(((IRecordHeaderSerializer)typeRouter).ProducesRecordHeaders).IsTrue();
        await Assert.That(((IRecordHeaderSerializer)fallbackRouter).ProducesRecordHeaders).IsTrue();
        await Assert.That(((IRecordHeaderSerializer)plainRouter).ProducesRecordHeaders).IsFalse();
    }

    [Test]
    public async Task FrozenRouter_SupportsConcurrentReads()
    {
        var router = new TopicRoutingDeserializer<EventBase>()
            .Register("alpha", AlphaDeserializer)
            .Freeze();
        var failures = 0;

        Parallel.For(0, 10_000, _ =>
        {
            if (!ReferenceEquals(router.Deserialize(ReadOnlyMemory<byte>.Empty, Context("alpha")), Alpha))
                Interlocked.Increment(ref failures);
        });

        await Assert.That(failures).IsEqualTo(0);
    }

    [Test]
    public async Task Registration_IsThreadSafeBeforeFreeze()
    {
        const int routeCount = 64;
        var router = new TopicRoutingDeserializer<EventBase>();

        Parallel.For(0, routeCount, index =>
            router.Register($"topic-{index}", AlphaDeserializer));
        router.Freeze();

        var failures = 0;
        Parallel.For(0, routeCount, index =>
        {
            if (!ReferenceEquals(
                    router.Deserialize(ReadOnlyMemory<byte>.Empty, Context($"topic-{index}")),
                    Alpha))
            {
                Interlocked.Increment(ref failures);
            }
        });

        await Assert.That(failures).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeResult_ProvidesRawKeyOnlyToValueDeserializer()
    {
        var key = new byte[] { 1, 2, 3 };
        var keyDeserializer = new ContextCapturingDeserializer<string>("key");
        var valueDeserializer = new ContextCapturingDeserializer<string>("value");

        _ = new ConsumeResult<string, string>(
            "events", 0, 0, key, false, new byte[] { 4 }, false, null, 0,
            TimestampType.NotAvailable, null, keyDeserializer, valueDeserializer);

        await Assert.That(keyDeserializer.Context.KeyData.IsEmpty).IsTrue();
        await Assert.That(keyDeserializer.Context.IsKeyNull).IsTrue();
        await Assert.That(valueDeserializer.Context.KeyData.Span.SequenceEqual(key)).IsTrue();
        await Assert.That(valueDeserializer.Context.IsKeyNull).IsFalse();
    }

    [Test]
    public async Task ConsumeResult_DistinguishesNullKeyFromEmptyKey()
    {
        var nullKey = new ContextCapturingDeserializer<string>("value");
        var emptyKey = new ContextCapturingDeserializer<string>("value");

        _ = new ConsumeResult<string, string>(
            "events", 0, 0, ReadOnlyMemory<byte>.Empty, true, new byte[] { 1 }, false, null, 0,
            TimestampType.NotAvailable, null, null, nullKey);
        _ = new ConsumeResult<string, string>(
            "events", 0, 0, ReadOnlyMemory<byte>.Empty, false, new byte[] { 1 }, false, null, 0,
            TimestampType.NotAvailable, null, null, emptyKey);

        await Assert.That(nullKey.Context.IsKeyNull).IsTrue();
        await Assert.That(emptyKey.Context.IsKeyNull).IsFalse();
        await Assert.That(emptyKey.Context.KeyData.IsEmpty).IsTrue();
    }

    private static SerializationContext Context(string topic = "events") => new()
    {
        Topic = topic,
        Component = SerializationComponent.Value
    };

    private static byte[] Frame(int schemaId, params byte[] payload)
    {
        var frame = new byte[sizeof(byte) + sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1), schemaId);
        payload.CopyTo(frame, 5);
        return frame;
    }

    private abstract class EventBase;
    private sealed class AlphaEvent : EventBase;
    private sealed class BetaEvent : EventBase;

    private sealed class EventDeserializer<T>(T value) : IDeserializer<T>
    {
        public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => value;
    }

    private sealed class CapturingDeserializer(EventBase value) : IDeserializer<EventBase>
    {
        public ReadOnlyMemory<byte> Data { get; private set; }

        public EventBase Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Data = data;
            return value;
        }
    }

    private sealed class ContextCapturingDeserializer<T>(T value) : IDeserializer<T>
    {
        public SerializationContext Context { get; private set; }

        public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Context = context;
            return value;
        }
    }

    private sealed class EventSerializer<T>(byte marker) : ISerializer<T>
    {
        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            destination.GetSpan(1)[0] = marker;
            destination.Advance(1);
        }
    }

    private sealed class RecordHeaderEventSerializer<T> : ISerializer<T>, IRecordHeaderSerializer
    {
        public bool ProducesRecordHeaders => true;

        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            context.Headers!.Add("identity", [1]);
            destination.GetSpan(1)[0] = 1;
            destination.Advance(1);
        }
    }
}
