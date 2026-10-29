using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry;

namespace Dekaf.Tests.Unit.Serialization;

public sealed class SerializationReentrancyTests
{
    private static readonly SerializationContext Context = new() { Topic = "reentrant", Component = SerializationComponent.Value };

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PlainJson_NestedSameType_PreservesBothDocuments(bool nestedThrows)
    {
        var fail = nestedThrows;
        var innerOptions = Options(new CallbackConverter(() =>
        {
            if (!fail) return;
            fail = false;
            throw new InvalidOperationException("nested");
        }));
        var inner = new Dekaf.Serialization.Json.JsonSerializer<Payload>(innerOptions);
        var nested = new ArrayBufferWriter<byte>();
        var converter = new CallbackConverter(() =>
        {
            inner.Serialize(new Payload("inner"), ref nested, Context);
        });
        var outer = new Dekaf.Serialization.Json.JsonSerializer<Payload>(Options(converter));
        var output = new ArrayBufferWriter<byte>();
        Exception? error = null;
        try { outer.Serialize(new Payload("outer"), ref output, Context); }
        catch (InvalidOperationException exception) { error = exception; }
        var firstOutput = Text(output);
        output.ResetWrittenCount();
        nested.ResetWrittenCount();
        outer.Serialize(new Payload("recovered"), ref output, Context);
        var recovered = Text(output);
        output.ResetWrittenCount();
        nested.ResetWrittenCount();
        outer.Serialize(new Payload("outer"), ref output, Context);

        await Assert.That(error is not null).IsEqualTo(nestedThrows);
        if (!nestedThrows)
            await Assert.That(firstOutput).IsEqualTo("{\"value\":\"outer\"}");
        await Assert.That(Text(nested)).IsEqualTo("{\"value\":\"inner\"}");
        await Assert.That(recovered).IsEqualTo("{\"value\":\"recovered\"}");
        await Assert.That(Text(output)).IsEqualTo("{\"value\":\"outer\"}");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Registry_NestedDifferentType_ProtectsPayload(bool outerJson, bool innerJson)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var inner = innerJson
            ? (IAsyncDisposable)new JsonSchemaRegistrySerializer<string>(registry, "{}")
            : new SchemaRegistrySerializer<string>(registry, static (value, writer) => Write(writer, value), static () => Schema());
        var innerSerializer = (ISerializer<string>)inner;
        var nested = new ArrayBufferWriter<byte>();
        Action callback = () => innerSerializer.Serialize("inner", ref nested, Context);
        var converter = new CallbackConverter(callback);
        await using var outer = outerJson
            ? (IAsyncDisposable)new JsonSchemaRegistrySerializer<Payload>(registry, "{}", Options(converter))
            : new SchemaRegistrySerializer<Payload>(registry, (value, writer) =>
            {
                Write(writer, "outer-");
                callback();
                Write(writer, "-tail");
            }, static () => Schema());
        var serializer = (ISerializer<Payload>)outer;
        var prepared = (IAsyncSerializerPreparationAdmission<Payload>)outer;
        _ = await ((IAsyncSerializerPreparationAdmission<string>)inner).PrepareForSerializationAsync("inner", Context);
        var admission = await prepared.PrepareForSerializationAsync(new Payload("outer"), Context);
        var publicOutput = new ArrayBufferWriter<byte>();
        var preparedOutput = new ArrayBufferWriter<byte>();
        serializer.Serialize(new Payload("outer"), ref publicOutput, Context);
        var publicNested = Text(nested, 5);
        nested.ResetWrittenCount();
        prepared.SerializePrepared(new Payload("outer"), ref preparedOutput, Context, in admission);

        await Assert.That(Text(publicOutput, 5)).IsEqualTo(outerJson ? "{\"value\":\"outer\"}" : "outer--tail");
        await Assert.That(Text(preparedOutput, 5)).IsEqualTo(Text(publicOutput, 5));
        await Assert.That(publicNested).IsEqualTo(innerJson ? "\"inner\"" : "inner");
        await Assert.That(Text(nested, 5)).IsEqualTo(publicNested);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegistryJson_NestedSameTypeAndException_RestoresWriter(bool preparedPath)
    {
        using var registry = new MockSchemaRegistryClient();
        var fail = true;
        var innerConverter = new CallbackConverter(() =>
        {
            if (!fail) return;
            fail = false;
            throw new InvalidOperationException("nested");
        });
        await using var inner = new JsonSchemaRegistrySerializer<Payload>(registry, "{}", Options(innerConverter));
        var nested = new ArrayBufferWriter<byte>();
        var converter = new CallbackConverter(() =>
        {
            inner.Serialize(new Payload("inner"), ref nested, Context);
        });
        await using var outer = new JsonSchemaRegistrySerializer<Payload>(registry, "{}", Options(converter));
        var prepared = (IAsyncSerializerPreparationAdmission<Payload>)outer;
        _ = await inner.PrepareAsync(Context.Topic, new Payload("inner"));
        var admission = await prepared.PrepareForSerializationAsync(new Payload("outer"), Context);
        var output = new ArrayBufferWriter<byte>();
        void Serialize()
        {
            if (preparedPath) prepared.SerializePrepared(new Payload("outer"), ref output, Context, in admission);
            else outer.Serialize(new Payload("outer"), ref output, Context);
        }
        var threw = false;
        try { Serialize(); }
        catch (InvalidOperationException) { threw = true; }
        nested.ResetWrittenCount();
        Serialize();
        await Assert.That(threw).IsTrue();
        await Assert.That(Text(output, 5)).IsEqualTo("{\"value\":\"outer\"}");
        await Assert.That(Text(nested, 5)).IsEqualTo("{\"value\":\"inner\"}");
    }

    [Test]
    [Arguments(0, false, false)]
    [Arguments(0, true, false)]
    [Arguments(1, false, false)]
    [Arguments(1, true, false)]
    [Arguments(2, false, false)]
    [Arguments(2, true, false)]
    [Arguments(1, false, true)]
    [Arguments(1, true, true)]
    [Arguments(2, false, true)]
    [Arguments(2, true, true)]
    public async Task DestinationCallbacks_HoldOwnershipThroughCopy(int kind, bool onAdvance, bool preparedPath)
    {
        using var registry = new MockSchemaRegistryClient();
        var plain = new Dekaf.Serialization.Json.JsonSerializer<Payload>();
        await using var json = new JsonSchemaRegistrySerializer<Payload>(registry, "{}");
        await using var custom = new SchemaRegistrySerializer<Payload>(registry,
            static (value, writer) => Write(writer, value.Value), static () => Schema());
        ISerializer<Payload> serializer = kind switch { 0 => plain, 1 => json, _ => custom };
        var innerOutput = new ArrayBufferWriter<byte>();
        var prepared = serializer as IAsyncSerializerPreparationAdmission<Payload>;
        var admission = prepared is null ? default : await prepared.PrepareForSerializationAsync(new Payload("outer"), Context);
        var fail = true;
        var destination = new CallbackWriter(() =>
        {
            serializer.Serialize(new Payload(new string('i', 8192)), ref innerOutput, Context);
            if (fail) throw new InvalidOperationException("destination");
        }, onAdvance);
        void Serialize()
        {
            if (preparedPath) prepared!.SerializePrepared(new Payload("outer"), ref destination, Context, in admission);
            else serializer.Serialize(new Payload("outer"), ref destination, Context);
        }
        var threw = false;
        try { Serialize(); }
        catch (InvalidOperationException) { threw = true; }
        fail = false;
        innerOutput.ResetWrittenCount();
        Serialize();
        var text = Encoding.UTF8.GetString(destination.Buffer.WrittenSpan[(kind == 0 ? 0 : 5)..]);
        await Assert.That(text).IsEqualTo(kind == 2 ? "outer" : "{\"value\":\"outer\"}");
        await Assert.That(threw).IsTrue();
        await Assert.That(innerOutput.WrittenCount).IsGreaterThan(8192 - 1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RuleCallbacks_KeepPayloadAliveAndRecover(bool json, bool preparedPath)
    {
        using var registry = new MockSchemaRegistryClient();
        var nested = new ArrayBufferWriter<byte>();
        await using var inner = new SchemaRegistrySerializer<string>(registry, static (value, writer) => Write(writer, value), static () => Schema());
        var rule = new CallbackRule(() => inner.Serialize("inner", ref nested, Context));
        await using var outer = json
            ? (IAsyncDisposable)new JsonSchemaRegistrySerializer<Payload>(registry, "{}", jsonOptions: null, ruleExecutor: rule)
            : new SchemaRegistrySerializer<Payload>(registry, static (value, writer) => Write(writer, value.Value), static () => Schema(), ruleExecutor: rule);
        _ = await inner.PrepareAsync(Context.Topic, "inner");
        var prepared = (IAsyncSerializerPreparationAdmission<Payload>)outer;
        var admission = await prepared.PrepareForSerializationAsync(new Payload("outer"), Context);
        var output = new ArrayBufferWriter<byte>();
        void Serialize()
        {
            if (preparedPath) prepared.SerializePrepared(new Payload("outer"), ref output, Context, in admission);
            else ((ISerializer<Payload>)outer).Serialize(new Payload("outer"), ref output, Context);
        }
        rule.Throw = true;
        var threw = false;
        try { Serialize(); }
        catch (InvalidOperationException) { threw = true; }
        rule.Throw = false;
        Serialize();
        await Assert.That(threw).IsTrue();
        await Assert.That(Text(output, 5)).IsEqualTo(json ? "{\"value\":\"outer\"}" : "outer");
    }

    private static Schema Schema() => new() { SchemaType = SchemaType.Json, SchemaString = "{}" };
    private static JsonSerializerOptions Options(CallbackConverter converter) => new() { Converters = { converter } };
    private static string Text(ArrayBufferWriter<byte> buffer, int offset = 0) => Encoding.UTF8.GetString(buffer.WrittenSpan[offset..]);
    private static void Write(IBufferWriter<byte> writer, string value)
    {
        var count = Encoding.UTF8.GetBytes(value.AsSpan(), writer.GetSpan(Encoding.UTF8.GetByteCount(value)));
        writer.Advance(count);
    }

    public sealed record Payload(string Value);

    private sealed class CallbackConverter(Action callback) : JsonConverter<Payload>
    {
        public override Payload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, Payload value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("value");
            callback();
            writer.WriteStringValue(value.Value);
            writer.WriteEndObject();
        }
    }

    private sealed class CallbackWriter(Action callback, bool onAdvance) : IBufferWriter<byte>
    {
        internal ArrayBufferWriter<byte> Buffer { get; } = new();
        public void Advance(int count)
        {
            if (onAdvance) callback();
            Buffer.Advance(count);
        }
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (!onAdvance) callback();
            return Buffer.GetMemory(sizeHint);
        }
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }

    private sealed class CallbackRule(Action callback) : ISchemaRegistryRuleExecutor
    {
        internal bool Throw { get; set; }
        public ReadOnlyMemory<byte> TransformSerializedPayload(ReadOnlyMemory<byte> payload, SchemaRegistryRuleContext context)
        {
            callback();
            if (Throw) throw new InvalidOperationException("rule");
            return payload;
        }
        public ReadOnlyMemory<byte> TransformDeserializedPayload(ReadOnlyMemory<byte> payload, SchemaRegistryRuleContext context) => payload;
    }
}
