using Dekaf.SchemaRegistry;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaIdentityFramingTests
{
    private static readonly Guid SchemaGuid = Guid.Parse("89791762-2336-4186-9674-299b90a802e2");

    private static readonly byte[] GuidVector =
    [
        0x01, 0x89, 0x79, 0x17, 0x62, 0x23, 0x36, 0x41, 0x86,
        0x96, 0x74, 0x29, 0x9b, 0x90, 0xa8, 0x02, 0xe2
    ];

    [Test]
    public async Task IdPrefix_MatchesConfluentWireVector()
    {
        var destination = new byte[SchemaIdentityFraming.SchemaIdFrameSize];

        var written = SchemaIdentityFraming.WriteSchemaId(destination, 1);
        var parsed = SchemaIdentityFraming.ReadPrefix(destination, out var payloadOffset);

        await Assert.That(written).IsEqualTo(5);
        await Assert.That(destination).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 1 });
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(1));
        await Assert.That(payloadOffset).IsEqualTo(5);
    }

    [Test]
    public async Task GuidIdentity_MatchesConfluentNetworkOrderVector()
    {
        var destination = new byte[SchemaIdentityFraming.SchemaGuidFrameSize];

        var written = SchemaIdentityFraming.WriteSchemaGuid(destination, SchemaGuid);
        var parsed = SchemaIdentityFraming.ReadPrefix(destination, out var payloadOffset);

        await Assert.That(written).IsEqualTo(17);
        await Assert.That(destination).IsEquivalentTo(GuidVector);
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
        await Assert.That(payloadOffset).IsEqualTo(17);
    }

    [Test]
    [Arguments(SerializationComponent.Key, SchemaIdentityHeaderNames.Key)]
    [Arguments(SerializationComponent.Value, SchemaIdentityHeaderNames.Value)]
    public async Task HeaderStrategy_UsesConfluentHeaderName(
        SerializationComponent component,
        string expectedName)
    {
        var headers = new Headers(1);
        var encoded = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);

        SchemaIdentityFraming.AddSchemaGuidHeader(headers, component, encoded);
        var parsed = SchemaIdentityFraming.ReadHeader(headers, component, out _);

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo(expectedName);
        await Assert.That(headers[0].Value.ToArray()).IsEquivalentTo(GuidVector);
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
    }

    [Test]
    public async Task HeaderStrategy_PreservesTrailingProtobufIndexes()
    {
        var headerValue = new byte[GuidVector.Length + 3];
        GuidVector.CopyTo(headerValue, 0);
        headerValue[^3] = 0x04;
        headerValue[^2] = 0x02;
        headerValue[^1] = 0x00;
        var headers = Headers.Create(SchemaIdentityHeaderNames.Value, headerValue);

        _ = SchemaIdentityFraming.ReadHeader(
            headers,
            SerializationComponent.Value,
            out var trailingHeaderData);

        await Assert.That(trailingHeaderData.ToArray()).IsEquivalentTo(new byte[] { 4, 2, 0 });
    }

    [Test]
    public async Task DualStrategy_HeaderWinsAndPayloadRemainsUnchanged()
    {
        var headers = Headers.Create(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        byte[] payloadWithDifferentPrefix = [0, 0, 0, 0, 42, 7, 8];

        var parsed = SchemaIdentityFraming.Read(
            payloadWithDifferentPrefix,
            headers,
            SerializationComponent.Value,
            SchemaIdDeserializerStrategy.Dual,
            out var payloadOffset,
            out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
        await Assert.That(payloadOffset).IsEqualTo(0);
    }

    [Test]
    public async Task DualStrategy_MissingHeaderFallsBackToPrefix()
    {
        byte[] payload = [0, 0, 0, 0, 42, 7, 8];

        var parsed = SchemaIdentityFraming.Read(
            payload,
            headers: (Headers?)null,
            SerializationComponent.Value,
            SchemaIdDeserializerStrategy.Dual,
            out var payloadOffset,
            out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(42));
        await Assert.That(payloadOffset).IsEqualTo(5);
    }

    [Test]
    public void DualStrategy_MalformedHeader_DoesNotFallBackToPrefix()
    {
        var headers = Headers.Create(SchemaIdentityHeaderNames.Value, new byte[] { 1, 2, 3 });
        byte[] validPrefix = [0, 0, 0, 0, 42, 7, 8];

        Assert.Throws<InvalidDataException>(() => SchemaIdentityFraming.Read(
            validPrefix,
            headers,
            SerializationComponent.Value,
            SchemaIdDeserializerStrategy.Dual,
            out _,
            out _));
    }

    [Test]
    public async Task HeaderStrategy_LastDuplicateWins()
    {
        var headers = new Headers(2)
            .Add(SchemaIdentityHeaderNames.Value, SchemaIdentityFraming.CreateSchemaGuidFrame(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")))
            .Add(SchemaIdentityHeaderNames.Value, SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));

        var parsed = SchemaIdentityFraming.ReadHeader(headers, SerializationComponent.Value, out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
    }

    [Test]
    public async Task PooledHeaderSpan_LastDuplicateWinsWithoutMaterialization()
    {
        Header[] headers =
        [
            new("unrelated", Array.Empty<byte>()),
            new(SchemaIdentityHeaderNames.Value, SchemaIdentityFraming.CreateSchemaGuidFrame(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"))),
            new(SchemaIdentityHeaderNames.Value, SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid)),
            default
        ];

        var parsed = SchemaIdentityFraming.ReadHeader(
            headers.AsSpan(0, 3),
            SerializationComponent.Value,
            out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
    }

    [Test]
    [Arguments(SchemaIdDeserializerStrategy.Prefix, 5)]
    [Arguments(SchemaIdDeserializerStrategy.Header, 0)]
    public async Task ExplicitStrategy_ReturnsExpectedPayloadOffset(
        SchemaIdDeserializerStrategy strategy,
        int expectedOffset)
    {
        var headers = Headers.Create(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        byte[] payload = [0, 0, 0, 0, 42, 7, 8];

        _ = SchemaIdentityFraming.Read(
            payload,
            headers,
            SerializationComponent.Value,
            strategy,
            out var payloadOffset,
            out _);

        await Assert.That(payloadOffset).IsEqualTo(expectedOffset);
    }

    [Test]
    public void ReadPrefix_UnknownMagicByte_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() => SchemaIdentityFraming.ReadPrefix([2, 0, 0, 0, 1], out _));

    [Test]
    public void ReadPrefix_TruncatedIdentity_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() => SchemaIdentityFraming.ReadPrefix([0, 0, 0, 1], out _));

    [Test]
    public void ReadHeader_MissingHeader_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
            SchemaIdentityFraming.ReadHeader(new Headers(), SerializationComponent.Value, out _));

    [Test]
    public void ReadHeader_NullValue_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
            SchemaIdentityFraming.ReadHeader(
                Headers.Create(SchemaIdentityHeaderNames.Value, (byte[]?)null),
                SerializationComponent.Value,
                out _));

    [Test]
    public void ReadHeader_MalformedValue_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
            SchemaIdentityFraming.ReadHeader(
                Headers.Create(SchemaIdentityHeaderNames.Value, new byte[] { 1, 2, 3 }),
                SerializationComponent.Value,
                out _));

    [Test]
    public void WriteSchemaId_NegativeId_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchemaIdentityFraming.WriteSchemaId(new byte[5], -1));

    [Test]
    public void SchemaIdentity_EmptyGuid_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => _ = new SchemaIdentity(Guid.Empty));

    [Test]
    [Arguments(null, false, true, 0)]
    [Arguments(null, false, false, 1)]
    [Arguments(null, true, false, 2)]
    [Arguments(42, false, true, 3)]
    [Arguments(42, true, false, 3)]
    public async Task ResolveSelection_UsesDocumentedPrecedence(
        int? useSchemaId,
        bool useLatestVersion,
        bool autoRegisterSchemas,
        int expected)
    {
        var actual = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId,
            useLatestVersion,
            autoRegisterSchemas);

        await Assert.That((int)actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments(42)]
    public void ResolveSelection_LatestAndAutoRegister_ThrowsArgumentException(int? useSchemaId) =>
        Assert.Throws<ArgumentException>(() =>
            SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
                useSchemaId,
                useLatestVersion: true,
                autoRegisterSchemas: true));

    [Test]
    public void ResolveSelection_NegativeExplicitId_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
                useSchemaId: -1,
                useLatestVersion: false,
                autoRegisterSchemas: false));
}
