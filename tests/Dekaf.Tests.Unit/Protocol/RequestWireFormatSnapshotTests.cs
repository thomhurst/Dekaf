using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Dekaf.Compression;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using VerifyTUnit;

namespace Dekaf.Tests.Unit.Protocol;

public class RequestWireFormatSnapshotTests
{
    private const int CorrelationId = 0x10203040;
    private const string ClientId = "dekaf-snapshot";

    private static readonly IReadOnlyDictionary<string, Type> RequestTypes = typeof(IKafkaMessage).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract && GetRequestInterface(type) is not null)
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToDictionary(static type => type.Name, StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(RequestVersions))]
    public async Task SerializedRequestFrame_MatchesGoldenSnapshot(string requestType, short version)
    {
        var type = RequestTypes[requestType];
        var request = DeterministicRequestFactory.Create(type);
        var serialized = KafkaRequestSerializer.Serialize(type, request, version);
        var apiKey = GetStaticProperty<ApiKey>(type, "ApiKey");

        var snapshot = $$"""
            Request: {{type.FullName}}
            API key: {{apiKey}} ({{(short)apiKey}})
            Version: {{version}}
            Header version: {{serialized.HeaderVersion}}
            Correlation ID: {{CorrelationId}} (0x{{CorrelationId:X8}})
            Client ID: {{ClientId}}
            Frame length: {{serialized.Bytes.Length}} bytes ({{serialized.Bytes.Length - sizeof(int)}}-byte payload)

            {{HexDump.Format(serialized.Bytes)}}
            """;

        var settings = new VerifyTests.VerifySettings();
        settings.UseDirectory("Snapshots");
        settings.UseFileName($"{requestType}.v{version}");
        settings.IgnoreParameters(nameof(requestType), nameof(version));
        settings.DisableRequireUniquePrefix();

        await Verifier.Verify(snapshot, settings);
    }

    [Test]
    public async Task DeterministicProduceRequest_IncludesEncodedRecord()
    {
        var request = (ProduceRequest)DeterministicRequestFactory.Create(typeof(ProduceRequest));
        var partition = request.TopicData.Single().PartitionData.Single();

        await Assert.That(partition.Records.Count).IsEqualTo(1);
        await Assert.That(partition.Compression).IsEqualTo(CompressionType.None);

        var record = partition.Records[0].Records.Single();
        await Assert.That(record.Key.IsEmpty).IsFalse();
        await Assert.That(record.Value.IsEmpty).IsFalse();
        await Assert.That(record.HeaderCount).IsEqualTo(1);
    }

    public static IEnumerable<(string RequestType, short Version)> RequestVersions()
    {
        foreach (var (name, type) in RequestTypes)
        {
            var lowestVersion = GetStaticProperty<short>(type, "LowestSupportedVersion");
            var highestVersion = GetStaticProperty<short>(type, "HighestSupportedVersion");

            for (var version = lowestVersion; version <= highestVersion; version++)
            {
                yield return (name, version);
            }
        }
    }

    private static Type? GetRequestInterface(Type type) => type.GetInterfaces()
        .SingleOrDefault(static implemented =>
            implemented.IsGenericType &&
            implemented.GetGenericTypeDefinition() == typeof(IKafkaRequest<>));

    private static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)(type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException($"{type.FullName} has no static {propertyName} property."));

    private static class KafkaRequestSerializer
    {
        private static readonly MethodInfo SerializeCoreMethod = typeof(KafkaRequestSerializer)
            .GetMethod(nameof(SerializeCore), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static SerializedRequest Serialize(Type requestType, object request, short version)
        {
            var requestInterface = GetRequestInterface(requestType)
                ?? throw new InvalidOperationException($"{requestType.FullName} is not a Kafka request.");
            var responseType = requestInterface.GetGenericArguments()[0];

            try
            {
                return (SerializedRequest)SerializeCoreMethod
                    .MakeGenericMethod(requestType, responseType)
                    .Invoke(null, [request, version])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static SerializedRequest SerializeCore<TRequest, TResponse>(object request, short version)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var payload = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(payload);
            var headerVersion = KafkaMessageMetadata<TRequest, TResponse>.GetRequestHeaderVersion(version);
            var header = new RequestHeader
            {
                ApiKey = KafkaMessageMetadata<TRequest, TResponse>.ApiKey,
                ApiVersion = version,
                CorrelationId = CorrelationId,
                ClientId = ClientId,
                HeaderVersion = headerVersion
            };

            header.Write(ref writer);
            ((TRequest)request).Write(ref writer, version);

            var frame = new byte[sizeof(int) + payload.WrittenCount];
            BinaryPrimitives.WriteInt32BigEndian(frame, payload.WrittenCount);
            payload.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int)));
            return new SerializedRequest(frame, headerVersion);
        }

        public readonly record struct SerializedRequest(byte[] Bytes, short HeaderVersion);
    }

    private static class DeterministicRequestFactory
    {
        private static readonly Guid FixedGuid = new("00112233-4455-6677-8899-aabbccddeeff");

        public static object Create(Type requestType) =>
            CreateValue(requestType, requestType.Name, depth: 0, [])
            ?? throw new InvalidOperationException($"Could not create deterministic {requestType.FullName} data.");

        private static object? CreateValue(Type type, string memberName, int depth, HashSet<Type> ancestors)
        {
            if (type == typeof(string))
            {
                return CreateString(memberName);
            }

            if (type == typeof(byte[]))
            {
                return new byte[] { 0x01, 0x23, 0x45, 0x67 };
            }

            if (type == typeof(ReadOnlyMemory<byte>))
            {
                return new ReadOnlyMemory<byte>([0x01, 0x23, 0x45, 0x67]);
            }

            if (type == typeof(Memory<byte>))
            {
                return new Memory<byte>([0x01, 0x23, 0x45, 0x67]);
            }

            if (type == typeof(Guid))
            {
                return FixedGuid;
            }

            if (type == typeof(bool))
            {
                return true;
            }

            if (type == typeof(byte))
            {
                return (byte)2;
            }

            if (type == typeof(sbyte))
            {
                return (sbyte)1;
            }

            if (type == typeof(short))
            {
                return (short)(memberName.Contains("Acks", StringComparison.OrdinalIgnoreCase) ? 1 : 7);
            }

            if (type == typeof(ushort))
            {
                return (ushort)7;
            }

            if (type == typeof(int))
            {
                return CreateInt32(memberName);
            }

            if (type == typeof(uint))
            {
                return 17U;
            }

            if (type == typeof(long))
            {
                return CreateInt64(memberName);
            }

            if (type == typeof(ulong))
            {
                return 1_234_567_890UL;
            }

            if (type == typeof(double))
            {
                return 42.5D;
            }

            if (type == typeof(float))
            {
                return 42.5F;
            }

            if (type == typeof(decimal))
            {
                return 42.5M;
            }

            if (type == typeof(CompressionType))
            {
                // Keep ProduceRequest records readable in the golden hex dump and stable
                // across runtime-specific compression implementations.
                return CompressionType.None;
            }

            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.Length > 1 ? values.GetValue(1) : values.GetValue(0);
            }

            if (Nullable.GetUnderlyingType(type) is { } nullableType)
            {
                return CreateValue(nullableType, memberName, depth, ancestors);
            }

            if (type.IsArray)
            {
                return CreateArray(type.GetElementType()!, memberName, depth, ancestors);
            }

            if (TryGetListElementType(type) is { } elementType)
            {
                return CreateArray(elementType, memberName, depth, ancestors);
            }

            if (type == typeof(CompressionCodecRegistry))
            {
                return null;
            }

            if (depth >= 12 || !ancestors.Add(type))
            {
                return null;
            }

            try
            {
                var instance = CreateInstance(type);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    var setter = property.GetSetMethod(nonPublic: true);
                    if (setter is null || property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    var value = CreateValue(property.PropertyType, property.Name, depth + 1, ancestors);
                    setter.Invoke(instance, [value]);
                }

                return instance;
            }
            finally
            {
                ancestors.Remove(type);
            }
        }

        private static object CreateInstance(Type type) =>
            Activator.CreateInstance(type, nonPublic: true)
            ?? throw new InvalidOperationException($"Activator returned null for {type.FullName}.");

        private static Array CreateArray(Type elementType, string memberName, int depth, HashSet<Type> ancestors)
        {
            if (elementType == typeof(RecordBatch))
            {
                return new[] { CreateRecordBatch() };
            }

            var array = Array.CreateInstance(elementType, 1);
            array.SetValue(CreateValue(elementType, memberName, depth + 1, ancestors), 0);
            return array;
        }

        private static RecordBatch CreateRecordBatch() => new()
        {
            BaseOffset = 0,
            PartitionLeaderEpoch = -1,
            LastOffsetDelta = 0,
            BaseTimestamp = 1_700_000_000_000L,
            MaxTimestamp = 1_700_000_000_000L,
            ProducerId = -1,
            ProducerEpoch = -1,
            BaseSequence = -1,
            Records =
            [
                new Record
                {
                    OffsetDelta = 0,
                    TimestampDelta = 0,
                    Key = "wire-key"u8.ToArray(),
                    Value = "wire-value"u8.ToArray(),
                    Headers = [new Header("wire-header", "wire-header-value"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };

        private static Type? TryGetListElementType(Type type)
        {
            if (!type.IsGenericType)
            {
                return null;
            }

            var definition = type.GetGenericTypeDefinition();
            return definition == typeof(IReadOnlyList<>) ||
                   definition == typeof(IList<>) ||
                   definition == typeof(ICollection<>) ||
                   definition == typeof(IEnumerable<>)
                ? type.GetGenericArguments()[0]
                : null;
        }

        private static string CreateString(string memberName) => memberName switch
        {
            var name when name.Contains("Topic", StringComparison.OrdinalIgnoreCase) => "wire-topic",
            var name when name.Contains("Group", StringComparison.OrdinalIgnoreCase) => "wire-group",
            var name when name.Contains("Transactional", StringComparison.OrdinalIgnoreCase) => "wire-transaction",
            var name when name.Contains("ClientSoftwareName", StringComparison.OrdinalIgnoreCase) => "dekaf",
            var name when name.Contains("ClientSoftwareVersion", StringComparison.OrdinalIgnoreCase) => "1.0.0",
            var name when name.Contains("Mechanism", StringComparison.OrdinalIgnoreCase) => "PLAIN",
            var name when name.Contains("Host", StringComparison.OrdinalIgnoreCase) => "broker.example",
            var name when name.Contains("Rack", StringComparison.OrdinalIgnoreCase) => "rack-a",
            var name when name.Contains("Cluster", StringComparison.OrdinalIgnoreCase) => "wire-cluster",
            var name when name.Contains("Protocol", StringComparison.OrdinalIgnoreCase) => "consumer",
            _ => $"wire-{memberName.ToLowerInvariant()}"
        };

        private static int CreateInt32(string memberName) => memberName switch
        {
            var name when name.Contains("Timeout", StringComparison.OrdinalIgnoreCase) => 30_000,
            var name when name.Contains("Partition", StringComparison.OrdinalIgnoreCase) => 2,
            var name when name.Contains("Epoch", StringComparison.OrdinalIgnoreCase) => 3,
            var name when name.Contains("Port", StringComparison.OrdinalIgnoreCase) => 9_092,
            _ => 17
        };

        private static long CreateInt64(string memberName) =>
            memberName.Contains("Timestamp", StringComparison.OrdinalIgnoreCase)
                ? 1_700_000_000_000L
                : memberName.Contains("Offset", StringComparison.OrdinalIgnoreCase)
                    ? 42L
                    : 1_234_567_890L;
    }

    private static class HexDump
    {
        private const int BytesPerLine = 16;

        public static string Format(ReadOnlySpan<byte> bytes)
        {
            var builder = new StringBuilder();
            for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
            {
                var line = bytes.Slice(offset, Math.Min(BytesPerLine, bytes.Length - offset));
                builder.Append(offset.ToString("X4")).Append(": ");

                for (var index = 0; index < BytesPerLine; index++)
                {
                    if (index < line.Length)
                    {
                        builder.Append(line[index].ToString("X2"));
                    }
                    else
                    {
                        builder.Append("  ");
                    }

                    builder.Append(index == 7 ? "  " : " ");
                }

                builder.Append('|');
                foreach (var value in line)
                {
                    builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
                }

                builder.AppendLine("|");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
