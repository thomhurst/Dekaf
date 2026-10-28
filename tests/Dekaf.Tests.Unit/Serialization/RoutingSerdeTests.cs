using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks.Sources;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using Dekaf.Serialization.Routing;
using Dekaf.Tests.Unit.SchemaRegistry;

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
    public async Task TopicDeserializer_PropagatesAsyncPreparationForDerivedRoute()
    {
        var child = new PreparingDeserializer<AlphaEvent>(Alpha);
        var router = new TopicRoutingDeserializer<EventBase>()
            .Register("alpha", child)
            .Freeze();
        var preparer = (IAsyncDeserializerPreparer<EventBase>)router;
        var context = Context("alpha");

        var preparedBefore = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            out _);
        await preparer.PrepareAsync(ReadOnlyMemory<byte>.Empty, context);
        var preparedAfter = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            out var result);

        await Assert.That(((IAsyncDeserializerPreparationRequirement)router).RequiresPreparation)
            .IsTrue();
        await Assert.That(preparedBefore).IsFalse();
        await Assert.That(preparedAfter).IsTrue();
        await Assert.That(result).IsSameReferenceAs(Alpha);
        await Assert.That(child.PrepareCount).IsEqualTo(1);
    }

    [Test]
    public async Task SchemaIdDeserializer_PropagatesAsyncPreparationForDerivedRoute()
    {
        var child = new PreparingDeserializer<AlphaEvent>(Alpha);
        var router = new SchemaIdRoutingDeserializer<EventBase>()
            .Register(42, child)
            .Freeze();
        var preparer = (IAsyncDeserializerPreparer<EventBase>)router;
        var context = Context();
        var frame = Frame(42);

        var preparedBefore = preparer.TryDeserialize(frame, context, out _);
        await preparer.PrepareAsync(frame, context);
        var preparedAfter = preparer.TryDeserialize(frame, context, out var result);

        await Assert.That(preparedBefore).IsFalse();
        await Assert.That(preparedAfter).IsTrue();
        await Assert.That(result).IsSameReferenceAs(Alpha);
    }

    [Test]
    public async Task NestedHeaderDeserializer_PropagatesIndexedPreparation()
    {
        var child = new PreparingDeserializer<EventBase>(Alpha);
        var headerRouter = new HeaderRoutingDeserializer<EventBase>(
            "event-type",
            new EventDeserializer<EventBase>(Beta),
            new HeaderDeserializerRoute<EventBase>("alpha"u8.ToArray(), child));
        var router = new TopicRoutingDeserializer<EventBase>()
            .Register("events", headerRouter)
            .Freeze();
        var plan = RecordHeaderRoutingPlan.Create<EventBase, EventBase>(null, router)!;
        var headers = new[] { new Header("event-type", "alpha"u8.ToArray()) };
        var lookup = new RecordHeaderRoutingLookup(
            plan,
            headers,
            headerCount: 1,
            firstIndex: 1,
            secondIndex: 0,
            routedHeaderTailOffset: RecordHeaderRoutingPlan.FullyIndexedWithoutTail);
        var preparer = (IRecordHeaderAsyncDeserializerPreparer<EventBase>)router;
        var context = Context();

        var preparedBefore = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            in lookup,
            out _);
        await preparer.PrepareAsync(
            ReadOnlyMemory<byte>.Empty,
            context,
            lookup,
            CancellationToken.None);
        var preparedAfter = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            in lookup,
            out var result);

        await Assert.That(preparedBefore).IsFalse();
        await Assert.That(preparedAfter).IsTrue();
        await Assert.That(result).IsSameReferenceAs(Alpha);
        await Assert.That(child.HeaderPathUsed).IsTrue();
    }

    [Test]
    public async Task NestedTopicDeserializer_RefreshesPreparationRequirementAtFreeze()
    {
        var child = new PreparingDeserializer<AlphaEvent>(Alpha);
        var inner = new TopicRoutingDeserializer<AlphaEvent>();
        var outer = new TopicRoutingDeserializer<EventBase>()
            .Register("alpha", inner);
        inner.Register("alpha", child).Freeze();
        outer.Freeze();
        var preparer = (IAsyncDeserializerPreparer<EventBase>)outer;
        var context = Context("alpha");

        var preparedBefore = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            out _);
        await preparer.PrepareAsync(ReadOnlyMemory<byte>.Empty, context);
        var preparedAfter = preparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            out var result);

        await Assert.That(((IAsyncDeserializerPreparationRequirement)outer).RequiresPreparation)
            .IsTrue();
        await Assert.That(preparedBefore).IsFalse();
        await Assert.That(preparedAfter).IsTrue();
        await Assert.That(result).IsSameReferenceAs(Alpha);
    }

    [Test]
    public async Task HeaderDeserializer_RefreshesNestedPreparationRequirement()
    {
        var child = new PreparingDeserializer<EventBase>(Alpha);
        var topicRouter = new TopicRoutingDeserializer<EventBase>();
        var headerRouter = new HeaderRoutingDeserializer<EventBase>(
            "event-type",
            new EventDeserializer<EventBase>(Beta),
            new HeaderDeserializerRoute<EventBase>("alpha"u8.ToArray(), topicRouter));
        topicRouter.Register("alpha", child).Freeze();

        await Assert.That(
                ((IAsyncDeserializerPreparationRequirement)headerRouter).RequiresPreparation)
            .IsTrue();
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
    public async Task SchemaIdDeserializer_ReturnsNullForTombstoneOnEveryEntryPoint()
    {
        // An unprepared route throws from Deserialize and reports false from TryDeserialize,
        // so any result below proves the tombstone never reached it.
        var child = new PreparingDeserializer<AlphaEvent>(Alpha);
        var router = new SchemaIdRoutingDeserializer<EventBase>()
            .Register(12, child)
            .Freeze();
        var context = TombstoneContext(SerializationComponent.Value);
        var lookup = default(RecordHeaderRoutingLookup);
        var preparer = (IAsyncDeserializerPreparer<EventBase>)router;
        var headerPreparer = (IRecordHeaderAsyncDeserializerPreparer<EventBase>)router;

        var value = router.Deserialize(ReadOnlyMemory<byte>.Empty, context);
        var headerValue = ((IRecordHeaderDeserializer<EventBase>)router)
            .Deserialize(ReadOnlyMemory<byte>.Empty, context, in lookup);
        var callerOwnedValue = ((ICallerOwnedHeaderDeserializer<EventBase>)router)
            .DeserializeCallerOwned(ReadOnlyMemory<byte>.Empty, context);
        var prepared = preparer.TryDeserialize(ReadOnlyMemory<byte>.Empty, context, out var preparedValue);
        var headerPrepared = headerPreparer.TryDeserialize(
            ReadOnlyMemory<byte>.Empty,
            context,
            in lookup,
            out var headerPreparedValue);
        var prepareTask = preparer.PrepareAsync(ReadOnlyMemory<byte>.Empty, context);
        var headerPrepareTask = headerPreparer.PrepareAsync(
            ReadOnlyMemory<byte>.Empty,
            context,
            lookup,
            CancellationToken.None);

        await Assert.That(value).IsNull();
        await Assert.That(headerValue).IsNull();
        await Assert.That(callerOwnedValue).IsNull();
        await Assert.That(prepared).IsTrue();
        await Assert.That(preparedValue).IsNull();
        await Assert.That(headerPrepared).IsTrue();
        await Assert.That(headerPreparedValue).IsNull();
        await Assert.That(prepareTask.IsCompletedSuccessfully).IsTrue();
        await Assert.That(headerPrepareTask.IsCompletedSuccessfully).IsTrue();
        await Assert.That(child.PrepareCount).IsEqualTo(0);
        await Assert.That(child.HeaderPathUsed).IsFalse();
    }

    [Test]
    public async Task SchemaIdDeserializer_RejectsNullKeyWithoutFrame()
    {
        var router = new SchemaIdRoutingDeserializer<EventBase>()
            .Register(12, AlphaDeserializer)
            .Freeze();

        await Assert.That(() => router.Deserialize(
                ReadOnlyMemory<byte>.Empty,
                TombstoneContext(SerializationComponent.Key)))
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

    [Test]
    public async Task Serializers_WithoutPreparingRoutes_CompletePreparationSynchronously()
    {
        var topicRouter = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", new EventSerializer<AlphaEvent>(1))
            .Freeze();
        var typeRouter = new TypeRoutingSerializer<EventBase>()
            .Register(new EventSerializer<AlphaEvent>(1))
            .Freeze();
        IAsyncSerializerPreparationAdmission<EventBase> topicAdmission = topicRouter;
        IAsyncSerializerPreparationAdmission<EventBase> typeAdmission = typeRouter;

        var topicPreparation = topicAdmission.PrepareForSerializationAsync(Alpha, Context("alpha"));
        var typePreparation = typeAdmission.PrepareForSerializationAsync(Alpha, Context());

        await Assert.That(((ISerializerPreparationRequirement)topicRouter).RequiresPreparation).IsFalse();
        await Assert.That(((ISerializerPreparationRequirement)typeRouter).RequiresPreparation).IsFalse();
        await Assert.That(topicPreparation.IsCompletedSuccessfully).IsTrue();
        await Assert.That(topicPreparation.Result.IsPrepared).IsFalse();
        await Assert.That(typePreparation.IsCompletedSuccessfully).IsTrue();
        await Assert.That(typePreparation.Result.IsPrepared).IsFalse();
    }

    [Test]
    public async Task TopicSerializer_ForwardsPreparationToSelectedRouteBeforeSerialization()
    {
        var alpha = new PreparingSerializer<AlphaEvent>(1);
        var fallback = new PreparingSerializer<EventBase>(2);
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", alpha)
            .SetFallback(fallback)
            .Freeze();

        await router.PrepareAsync(Alpha, Context("alpha"));
        var alphaBytes = Serialize(router, Alpha, Context("alpha"));
        await router.PrepareAsync(Beta, Context("other"));
        var fallbackBytes = Serialize(router, Beta, Context("other"));

        await Assert.That(((ISerializerPreparationRequirement)router).RequiresPreparation).IsTrue();
        await Assert.That(alpha.PrepareCount).IsEqualTo(1);
        await Assert.That(fallback.PrepareCount).IsEqualTo(1);
        await Assert.That(alphaBytes).IsEquivalentTo(new byte[] { 1 });
        await Assert.That(fallbackBytes).IsEquivalentTo(new byte[] { 2 });
    }

    [Test]
    public async Task NestedSerializers_ForwardExactPreparationAdmission()
    {
        var alpha = new AdmittedSerializer<AlphaEvent>(schemaId: 31);
        var inner = new TypeRoutingSerializer<EventBase>()
            .Register(alpha)
            .Freeze();
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", inner)
            .Freeze();
        IAsyncSerializerPreparationAdmission<EventBase> admission = router;
        var context = Context("alpha");

        var prepared = await admission.PrepareForSerializationAsync(Alpha, context);
        var buffer = new ArrayBufferWriter<byte>();
        admission.SerializePrepared(Alpha, ref buffer, context, in prepared);

        await Assert.That(((ISerializerPreparationRequirement)router).RequiresPreparation).IsTrue();
        await Assert.That(prepared.IsPrepared).IsTrue();
        await Assert.That(alpha.PrepareCount).IsEqualTo(1);
        await Assert.That(alpha.SerializePreparedCount).IsEqualTo(1);
        await Assert.That(alpha.SerializeCount).IsEqualTo(0);
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 31 });
    }

    [Test]
    public async Task OuterSerializer_FrozenBeforeNestedRegistration_StillForwardsPreparation()
    {
        var alpha = new PreparingSerializer<AlphaEvent>(1);
        var inner = new TypeRoutingSerializer<EventBase>();
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", inner)
            .Freeze();
        inner.Register(alpha).Freeze();

        await router.PrepareAsync(Alpha, Context("alpha"));

        await Assert.That(alpha.PrepareCount).IsEqualTo(1);
    }

    [Test]
    public async Task TopicSerializer_ConsumesCompletedSourceBackedPreparation()
    {
        var alpha = new SourceBackedPreparingSerializer<AlphaEvent>();
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", alpha)
            .Freeze();
        IAsyncSerializerPreparationAdmission<EventBase> admission = router;

        var preparation = admission.PrepareForSerializationAsync(Alpha, Context("alpha"));

        await Assert.That(preparation.IsCompletedSuccessfully).IsTrue();
        await Assert.That(preparation.Result.IsPrepared).IsFalse();
        await Assert.That(alpha.GetResultCount).IsEqualTo(1);
    }

    [Test]
    public async Task TypeSerializer_PropagatesCancellationDuringColdPreparation()
    {
        var alpha = new BlockingPreparingSerializer<AlphaEvent>();
        var router = new TypeRoutingSerializer<EventBase>()
            .Register(alpha)
            .Freeze();
        IAsyncSerializerPreparationAdmission<EventBase> admission = router;
        using var cts = new CancellationTokenSource();

        var preparation = admission.PrepareForSerializationAsync(Alpha, Context(), cts.Token).AsTask();
        await alpha.Started.Task;
        await Assert.That(preparation.IsCompleted).IsFalse();
        await cts.CancelAsync();

        await Assert.That(async () => await preparation).Throws<OperationCanceledException>();
        await Assert.That(alpha.ObservedToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task TopicSerializer_AsyncSubjectStrategyWorksWithoutManualWarmup()
    {
        using var registry = new MockSchemaRegistryClient();
        var strategy = new YieldingSubjectNameStrategy();
        await using var schemaSerializer = new SchemaRegistrySerializer<AlphaEvent>(
            registry,
            static (_, writer) =>
            {
                writer.GetSpan(1)[0] = 9;
                writer.Advance(1);
            },
            strategy,
            static () => new Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = """{"type":"record","name":"Alpha","namespace":"test","fields":[]}"""
            });
        var router = new TopicRoutingSerializer<EventBase>()
            .Register("alpha", schemaSerializer)
            .Freeze();
        IAsyncSerializerPreparationAdmission<EventBase> admission = router;
        var context = Context("alpha");

        var prepared = await admission.PrepareForSerializationAsync(Alpha, context);
        var buffer = new ArrayBufferWriter<byte>();
        admission.SerializePrepared(Alpha, ref buffer, context, in prepared);
        var plainBytes = Serialize(router, Alpha, context);

        await Assert.That(prepared.IsPrepared).IsTrue();
        await Assert.That(strategy.AsyncResolutionCount).IsEqualTo(1);
        await Assert.That(await registry.GetAllSubjectsAsync()).Contains("routed-alpha");
        await Assert.That(buffer.WrittenSpan[0]).IsEqualTo((byte)0);
        await Assert.That(buffer.WrittenSpan[^1]).IsEqualTo((byte)9);
        await Assert.That(plainBytes).IsEquivalentTo(buffer.WrittenSpan.ToArray());
    }

    private static byte[] Serialize<T>(ISerializer<T> serializer, T value, SerializationContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(value, ref buffer, context);
        return buffer.WrittenSpan.ToArray();
    }

    private static SerializationContext Context(string topic = "events") => new()
    {
        Topic = topic,
        Component = SerializationComponent.Value
    };

    private static SerializationContext TombstoneContext(SerializationComponent component) => new()
    {
        Topic = "events",
        Component = component,
        IsNull = true
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

    private sealed class PreparingDeserializer<T>(T value) :
        IDeserializer<T>,
        IAsyncDeserializerPreparer<T>,
        IRecordHeaderAsyncDeserializerPreparer<T>
    {
        private bool _prepared;

        public int PrepareCount { get; private set; }
        public bool HeaderPathUsed { get; private set; }

        public T Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            _prepared
                ? value
                : throw new InvalidOperationException("Deserializer was not prepared.");

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out T result)
        {
            result = _prepared ? value : default!;
            return _prepared;
        }

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCount++;
            _prepared = true;
            return ValueTask.CompletedTask;
        }

        bool IRecordHeaderAsyncDeserializerPreparer<T>.TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            in RecordHeaderRoutingLookup headers,
            out T result)
        {
            HeaderPathUsed = true;
            return TryDeserialize(data, context, out result);
        }

        ValueTask IRecordHeaderAsyncDeserializerPreparer<T>.PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            RecordHeaderRoutingLookup headers,
            CancellationToken cancellationToken)
        {
            HeaderPathUsed = true;
            return PrepareAsync(data, context, cancellationToken);
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

    private sealed class PreparingSerializer<T>(byte marker) : ISerializer<T>, IAsyncSerializerPreparer<T>
    {
        private bool _prepared;

        public int PrepareCount { get; private set; }

        public ValueTask PrepareAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            _prepared = true;
            return ValueTask.CompletedTask;
        }

        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            if (!_prepared)
                throw new InvalidOperationException("Serializer was not prepared.");

            destination.GetSpan(1)[0] = marker;
            destination.Advance(1);
        }
    }

    private sealed class BlockingPreparingSerializer<T> : ISerializer<T>, IAsyncSerializerPreparer<T>
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken ObservedToken { get; private set; }

        public async ValueTask PrepareAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            ObservedToken = cancellationToken;
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
            => throw new InvalidOperationException("Serializer was not prepared.");
    }

    // Completes synchronously through an IValueTaskSource, which must be consumed to be recycled.
    private sealed class SourceBackedPreparingSerializer<T> :
        ISerializer<T>,
        IAsyncSerializerPreparer<T>,
        IValueTaskSource
    {
        public int GetResultCount { get; private set; }

        public ValueTask PrepareAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            new(this, token: 0);

        public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;

        public void GetResult(short token) => GetResultCount++;

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            continuation(state);

        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            destination.GetSpan(1)[0] = 1;
            destination.Advance(1);
        }
    }

    private sealed class AdmittedSerializer<T>(int schemaId) :
        ISerializer<T>,
        IAsyncSerializerPreparationAdmission<T>
    {
        private static readonly object PreparedSchema = new();

        public int PrepareCount { get; private set; }
        public int SerializePreparedCount { get; private set; }
        public int SerializeCount { get; private set; }

        public ValueTask PrepareAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Router must request an exact admission.");

        public ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
            T value,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            return new ValueTask<SerializerPreparationAdmission>(
                new SerializerPreparationAdmission("subject", schemaId, PreparedSchema));
        }

        public void Serialize<TWriter>(T value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            SerializeCount++;
            throw new InvalidOperationException("Admission was not forwarded.");
        }

        public void SerializePrepared<TWriter>(
            T value,
            ref TWriter destination,
            SerializationContext context,
            in SerializerPreparationAdmission admission)
            where TWriter : IBufferWriter<byte>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            SerializePreparedCount++;
            destination.GetSpan(1)[0] = (byte)admission.SchemaId;
            destination.Advance(1);
        }
    }

    private sealed class YieldingSubjectNameStrategy : IAsyncSubjectNameStrategy
    {
        private string? _subject;

        public int AsyncResolutionCount { get; private set; }

        // Completes asynchronously until warmed, like a strategy backed by a registry lookup.
        public ValueTask<string> GetSubjectNameAsync(
            string topic,
            string? recordType,
            bool isKey,
            CancellationToken cancellationToken = default) =>
            _subject is { } subject ? new(subject) : ResolveAsync(topic);

        private async ValueTask<string> ResolveAsync(string topic)
        {
            AsyncResolutionCount++;
            await Task.Yield();
            return _subject = "routed-" + topic;
        }
    }
}
