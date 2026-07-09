using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using VerifyTests;
using VerifyTUnit;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class WireFormatSnapshotTests
{
    private const string ApiKeyPropertyName = "ApiKey";
    private const int CorrelationId = 0x12345678;
    private const string HighestSupportedVersionPropertyName = "HighestSupportedVersion";
    private const string LowestSupportedVersionPropertyName = "LowestSupportedVersion";
    private static readonly Guid s_guid = new("12345678-1234-5678-9abc-def012345678");
    private static readonly byte[] s_payload = [0x01, 0x23, 0x45, 0x67, 0x89];
    private static readonly MethodInfo s_serializeRequest = typeof(WireFormatSnapshotTests)
        .GetMethod(nameof(SerializeRequestCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Test]
    public async Task Requests_match_golden_wire_format()
    {
        var requestTypes = typeof(IKafkaMessage).Assembly
            .GetTypes()
            .Where(IsConcreteRequest)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        var snapshots = new List<RequestWireSnapshot>();
        foreach (var requestType in requestTypes)
        {
            var lowestVersion = GetStaticProperty<short>(requestType, LowestSupportedVersionPropertyName);
            var highestVersion = GetStaticProperty<short>(requestType, HighestSupportedVersionPropertyName);

            for (var version = lowestVersion; version <= highestVersion; version++)
            {
                var request = CanonicalValueFactory.Create(requestType, requestType.Name);
                var bytes = SerializeRequest(requestType, request, version);
                snapshots.Add(new RequestWireSnapshot(
                    requestType.Name,
                    $"v{version}",
                    GetStaticProperty<ApiKey>(requestType, ApiKeyPropertyName).ToString(),
                    Convert.ToHexString(bytes)));
            }
        }

        await Assert.That(requestTypes).IsNotEmpty();
        await Verifier.Verify(snapshots, SnapshotSettings());
    }

    [Test]
    public async Task Record_batches_match_golden_wire_format()
    {
        var registry = new CompressionCodecRegistry()
            .AddBrotli()
            .AddLz4()
            .AddSnappy()
            .AddZstd();
        var snapshots = new List<RecordBatchWireSnapshot>();

        foreach (var compression in Enum.GetValues<CompressionType>())
        {
            foreach (var includeHeaders in new[] { false, true })
            {
                using var batch = CreateRecordBatch(RecordBatchAttributes.None, includeHeaders);
                snapshots.Add(EncodeRecordBatch($"{compression}-headers-{includeHeaders}", batch, compression, registry));
            }
        }

        using (var transactional = CreateRecordBatch(RecordBatchAttributes.IsTransactional, includeHeaders: true))
        {
            snapshots.Add(EncodeRecordBatch("transactional", transactional, CompressionType.None, registry));
        }

        using (var control = CreateControlBatch())
        {
            snapshots.Add(EncodeRecordBatch("control-commit-marker", control, CompressionType.None, registry));
        }

        var platform = OperatingSystem.IsWindows() ? "windows" : "unix";
        await Verifier.Verify(snapshots, SnapshotSettings(platform));
    }

    [Test]
    public async Task Decoded_responses_match_golden_object_graphs()
    {
        var apiVersionsBytes = Convert.FromHexString("000002001200030003000000000700");
        var produceBytes = Convert.FromHexString("020D676F6C64656E2D746F70696301000000000900");
        var fetchBytes = Convert.FromHexString("00000005000000000009020D676F6C64656E2D746F706963010000");

        var apiVersions = Decode<ApiVersionsResponse>(apiVersionsBytes, ApiVersionsResponse.Read, version: 3);
        var produce = Decode<ProduceResponse>(produceBytes, ProduceResponse.Read, version: 11);
        var fetch = Decode<FetchResponse>(fetchBytes, FetchResponse.Read, version: 12);

        try
        {
            var snapshots = new[]
            {
                new ResponseWireSnapshot("ApiVersionsResponse-v3", Convert.ToHexString(apiVersionsBytes), apiVersions),
                new ResponseWireSnapshot("ProduceResponse-v11", Convert.ToHexString(produceBytes), Snapshot(produce)),
                new ResponseWireSnapshot("FetchResponse-v12", Convert.ToHexString(fetchBytes), fetch)
            };

            await Verifier.Verify(snapshots, SnapshotSettings());
        }
        finally
        {
            produce.Return();
            fetch.ReturnToPool();
        }
    }

    private static VerifySettings SnapshotSettings(string? parameterText = null)
    {
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        if (parameterText is not null)
            settings.UseTextForParameters(parameterText);
        return settings;
    }

    private static bool IsConcreteRequest(Type type) =>
        !type.IsAbstract &&
        !type.IsInterface &&
        type.GetInterfaces().Any(static candidate =>
            candidate.IsGenericType &&
            candidate.GetGenericTypeDefinition() == typeof(IKafkaRequest<>));

    private static byte[] SerializeRequest(Type requestType, object request, short version)
    {
        var responseType = requestType.GetInterfaces()
            .Single(static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IKafkaRequest<>))
            .GetGenericArguments()[0];

        try
        {
            return (byte[])s_serializeRequest.MakeGenericMethod(responseType)
                .Invoke(null, [requestType, request, version])!;
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                $"Failed to serialize {requestType.Name} v{version}.",
                exception.InnerException);
        }
    }

    private static byte[] SerializeRequestCore<TResponse>(Type requestType, object request, short version)
        where TResponse : IKafkaResponse
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var header = new RequestHeader
        {
            ApiKey = GetStaticProperty<ApiKey>(requestType, ApiKeyPropertyName),
            ApiVersion = version,
            CorrelationId = CorrelationId,
            ClientId = "golden-client",
            HeaderVersion = GetRequestHeaderVersion(requestType, version)
        };

        header.Write(ref writer);
        ((IKafkaRequest<TResponse>)request).Write(ref writer, version);

        var framed = new byte[sizeof(int) + buffer.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(framed, buffer.WrittenCount);
        buffer.WrittenSpan.CopyTo(framed.AsSpan(sizeof(int)));
        return framed;
    }

    private static short GetRequestHeaderVersion(Type requestType, short version)
    {
        var method = requestType.GetMethod(
            "GetRequestHeaderVersion",
            BindingFlags.Public | BindingFlags.Static);
        return method is null ? (short)2 : (short)method.Invoke(null, [version])!;
    }

    private static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static object Snapshot(ProduceResponse response) => new
    {
        Responses = response.Responses
            .Take(response.TopicCount)
            .Select(static topic => new
            {
                topic.Name,
                PartitionResponses = (topic.PartitionResponses ?? [])
                    .Take(topic.PartitionCount)
                    .ToArray(),
                topic.PartitionCount
            })
            .ToArray(),
        response.TopicCount,
        response.ThrottleTimeMs,
        response.NodeEndpoints
    };

    private static RecordBatch CreateRecordBatch(RecordBatchAttributes attributes, bool includeHeaders)
    {
        var headers = includeHeaders
            ? new[]
            {
                new Header("trace-id", "abc123"u8.ToArray()),
                new Header("empty", [])
            }
            : null;

        return new RecordBatch
        {
            BaseOffset = 42,
            PartitionLeaderEpoch = 3,
            Attributes = attributes,
            LastOffsetDelta = 1,
            BaseTimestamp = 1_700_000_000_000,
            MaxTimestamp = 1_700_000_000_001,
            ProducerId = 99,
            ProducerEpoch = 2,
            BaseSequence = 7,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    IsKeyNull = true,
                    Value = "golden-value"u8.ToArray(),
                    Headers = headers,
                    HeaderCount = headers?.Length ?? 0
                },
                new Record
                {
                    OffsetDelta = 1,
                    TimestampDelta = 1,
                    Key = "golden-key"u8.ToArray(),
                    IsValueNull = true
                }
            ]
        };
    }

    private static RecordBatch CreateControlBatch() => new()
    {
        BaseOffset = 43,
        PartitionLeaderEpoch = 3,
        Attributes = RecordBatchAttributes.IsTransactional | RecordBatchAttributes.IsControlBatch,
        BaseTimestamp = 1_700_000_000_002,
        MaxTimestamp = 1_700_000_000_002,
        ProducerId = 99,
        ProducerEpoch = 2,
        BaseSequence = 9,
        Records =
        [
            new Record
            {
                OffsetDelta = 0,
                TimestampDelta = 0,
                Key = new byte[] { 0, 0, 0, 1 },
                Value = new byte[] { 0, 0, 0, 0, 0, 7 }
            }
        ]
    };

    private static RecordBatchWireSnapshot EncodeRecordBatch(
        string scenario,
        RecordBatch batch,
        CompressionType compression,
        CompressionCodecRegistry registry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        batch.Write(buffer, compression, registry);
        return new RecordBatchWireSnapshot(scenario, Convert.ToHexString(buffer.WrittenSpan));
    }

    private delegate IKafkaResponse ResponseReader(ref KafkaProtocolReader reader, short version);

    private static TResponse Decode<TResponse>(byte[] bytes, ResponseReader read, short version)
        where TResponse : IKafkaResponse
    {
        var reader = new KafkaProtocolReader(bytes);
        return (TResponse)read(ref reader, version);
    }

    private sealed record RequestWireSnapshot(string Request, string Version, string ApiKey, string Bytes);

    private sealed record RecordBatchWireSnapshot(string Scenario, string Bytes);

    private sealed record ResponseWireSnapshot(string Response, string Bytes, object Decoded);

    private static class CanonicalValueFactory
    {
        public static object Create(Type type, string memberName)
        {
            if (type == typeof(string))
                return $"golden-{memberName.ToLowerInvariant()}";
            if (type == typeof(byte[]))
                return s_payload.ToArray();
            if (type == typeof(ReadOnlyMemory<byte>))
                return new ReadOnlyMemory<byte>(s_payload);
            if (type == typeof(Memory<byte>))
                return new Memory<byte>(s_payload.ToArray());
            if (type == typeof(Guid))
                return s_guid;
            if (type == typeof(bool))
                return true;
            if (type == typeof(byte))
                return (byte)1;
            if (type == typeof(sbyte))
                return (sbyte)1;
            if (type == typeof(short))
                return (short)2;
            if (type == typeof(ushort))
                return (ushort)9092;
            if (type == typeof(int))
                return 3;
            if (type == typeof(uint))
                return 4u;
            if (type == typeof(long))
                return 5L;
            if (type == typeof(ulong))
                return 6UL;
            if (type == typeof(float))
                return 1.25f;
            if (type == typeof(double))
                return 1.25d;
            if (type == typeof(decimal))
                return 1.25m;
            if (type == typeof(RecordBatch))
                return CreateRecordBatch(RecordBatchAttributes.None, includeHeaders: true);
            if (type == typeof(CompressionCodecRegistry))
                return new CompressionCodecRegistry();
            if (type.IsEnum)
                return Enum.GetValues(type).GetValue(0)!;

            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null)
                return Activator.CreateInstance(type, Create(nullableType, memberName))!;

            if (type.IsArray)
                return CreateArray(type.GetElementType()!, memberName);

            if (type.IsGenericType && IsSupportedCollection(type.GetGenericTypeDefinition()))
                return CreateArray(type.GetGenericArguments()[0], memberName);

            if (type.IsInterface || type.IsAbstract)
                throw new NotSupportedException($"No canonical value for {type} ({memberName}).");

            var instance = Activator.CreateInstance(type, nonPublic: true)
                ?? throw new InvalidOperationException($"Could not create {type}.");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (property.SetMethod is null || property.GetIndexParameters().Length != 0)
                    continue;

                property.SetValue(instance, Create(property.PropertyType, property.Name));
            }

            return instance;
        }

        private static Array CreateArray(Type elementType, string memberName)
        {
            var array = Array.CreateInstance(elementType, 1);
            array.SetValue(Create(elementType, memberName), 0);
            return array;
        }

        private static bool IsSupportedCollection(Type genericType) =>
            genericType == typeof(IReadOnlyList<>) ||
            genericType == typeof(IReadOnlyCollection<>) ||
            genericType == typeof(IEnumerable<>) ||
            genericType == typeof(IList<>) ||
            genericType == typeof(ICollection<>) ||
            genericType == typeof(List<>);
    }
}
