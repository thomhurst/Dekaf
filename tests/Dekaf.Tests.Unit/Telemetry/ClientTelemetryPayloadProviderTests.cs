using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Dekaf.Compression;
using Dekaf.Telemetry;
using CompressionType = Dekaf.Protocol.Records.CompressionType;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ClientTelemetryPayloadProviderTests
{
    [Test]
    public async Task Collect_CumulativeMetrics_EncodesOtlpMetricsData()
    {
        var clock = new SequenceClock(10, 20);
        var provider = new ClientTelemetryPayloadProvider(unixNanoClock: clock.Next);
        var snapshot = new ClientTelemetryMetricSnapshot(
            DeltaTemporality: false,
            Metrics:
            [
                new ClientTelemetryMetric(
                    ClientTelemetryMetricNames.ProducerConnectionCreationTotal,
                    ClientTelemetryMetricKind.Counter,
                    2.0,
                    []),
                new ClientTelemetryMetric(
                    ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg,
                    ClientTelemetryMetricKind.Gauge,
                    12.5,
                    [new ClientTelemetryMetricAttribute("node_id", "3")])
            ]);

        var payload = provider.Collect(Subscription(deltaTemporality: false), snapshot, terminating: false);

        var metrics = DecodeMetricsData(payload);
        var counter = metrics.Single(m => m.Name == ClientTelemetryMetricNames.ProducerConnectionCreationTotal);
        var gauge = metrics.Single(m => m.Name == ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg);
        var sum = counter.Sum ?? throw new InvalidOperationException("Counter metric was not encoded as a sum.");
        var counterPoint = sum.DataPoints.Single();
        var gaugePoint = (gauge.Gauge ?? throw new InvalidOperationException("Gauge metric was not encoded as a gauge."))
            .DataPoints.Single();

        await Assert.That(counter.Gauge).IsNull();
        await Assert.That(sum.Temporality).IsEqualTo(2);
        await Assert.That(sum.IsMonotonic).IsTrue();
        await Assert.That(counterPoint.StartTimeUnixNano).IsEqualTo((ulong)10);
        await Assert.That(counterPoint.TimeUnixNano).IsEqualTo((ulong)20);
        await Assert.That(counterPoint.Value).IsEqualTo(2.0);

        await Assert.That(gauge.Sum).IsNull();
        await Assert.That(gaugePoint.StartTimeUnixNano).IsEqualTo((ulong)10);
        await Assert.That(gaugePoint.TimeUnixNano).IsEqualTo((ulong)20);
        await Assert.That(gaugePoint.Value).IsEqualTo(12.5);
        await Assert.That(gaugePoint.Attributes["node_id"]).IsEqualTo("3");
    }

    [Test]
    public async Task Collect_DeltaMetrics_UsesDeltaTemporalityAndPreviousCollectionStart()
    {
        var clock = new SequenceClock(100, 200, 300);
        var provider = new ClientTelemetryPayloadProvider(unixNanoClock: clock.Next);
        var snapshot = new ClientTelemetryMetricSnapshot(
            DeltaTemporality: true,
            Metrics:
            [
                new ClientTelemetryMetric(
                    ClientTelemetryMetricNames.ConsumerConnectionCreationTotal,
                    ClientTelemetryMetricKind.Counter,
                    1.0,
                    [])
            ]);

        var first = DecodeMetricsData(provider.Collect(Subscription(deltaTemporality: true), snapshot, terminating: false))
            .Single().Sum ?? throw new InvalidOperationException("First counter metric was not encoded as a sum.");
        var second = DecodeMetricsData(provider.Collect(Subscription(deltaTemporality: true), snapshot, terminating: false))
            .Single().Sum ?? throw new InvalidOperationException("Second counter metric was not encoded as a sum.");

        await Assert.That(first.Temporality).IsEqualTo(1);
        await Assert.That(first.DataPoints.Single().StartTimeUnixNano).IsEqualTo((ulong)100);
        await Assert.That(first.DataPoints.Single().TimeUnixNano).IsEqualTo((ulong)200);
        await Assert.That(second.DataPoints.Single().StartTimeUnixNano).IsEqualTo((ulong)200);
        await Assert.That(second.DataPoints.Single().TimeUnixNano).IsEqualTo((ulong)300);
    }

    [Test]
    public async Task Collect_GzipCompression_CompressesMetricsData()
    {
        var clock = new SequenceClock(10, 20);
        var compressionCodecs = new CompressionCodecRegistry();
        var provider = new ClientTelemetryPayloadProvider(compressionCodecs, clock.Next);
        var snapshot = new ClientTelemetryMetricSnapshot(
            DeltaTemporality: false,
            Metrics:
            [
                new ClientTelemetryMetric(
                    ClientTelemetryMetricNames.ProducerConnectionCreationTotal,
                    ClientTelemetryMetricKind.Counter,
                    3.0,
                    [])
            ]);

        var payload = provider.Collect(
            Subscription(deltaTemporality: false, compressionType: (sbyte)CompressionType.Gzip),
            snapshot,
            terminating: false);

        var decompressed = new ArrayBufferWriter<byte>();
        compressionCodecs.GetCodec(CompressionType.Gzip)
            .Decompress(new ReadOnlySequence<byte>(payload), decompressed);
        var metric = DecodeMetricsData(decompressed.WrittenMemory).Single();

        await Assert.That(payload.Length).IsGreaterThan(0);
        await Assert.That(payload.Span[0]).IsEqualTo((byte)0x1f);
        await Assert.That(metric.Name).IsEqualTo(ClientTelemetryMetricNames.ProducerConnectionCreationTotal);
        await Assert.That((metric.Sum ?? throw new InvalidOperationException("Counter metric was not encoded as a sum."))
            .DataPoints.Single().Value).IsEqualTo(3.0);
    }

    private static ClientTelemetrySubscription Subscription(
        bool deltaTemporality,
        sbyte compressionType = 0) =>
        new(
            ClientInstanceId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SubscriptionId: 1,
            CompressionType: compressionType,
            PushIntervalMs: 60000,
            TelemetryMaxBytes: 65536,
            DeltaTemporality: deltaTemporality,
            RequestedMetrics: [string.Empty]);

    private static IReadOnlyList<OtlpMetric> DecodeMetricsData(ReadOnlyMemory<byte> payload)
    {
        var metrics = new List<OtlpMetric>();
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                ReadResourceMetrics(reader.ReadLengthDelimited(), metrics);
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return metrics;
    }

    private static void ReadResourceMetrics(ReadOnlyMemory<byte> payload, List<OtlpMetric> metrics)
    {
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 2 && wireType == ProtoReader.WireLengthDelimited)
            {
                ReadScopeMetrics(reader.ReadLengthDelimited(), metrics);
            }
            else
            {
                reader.Skip(wireType);
            }
        }
    }

    private static void ReadScopeMetrics(ReadOnlyMemory<byte> payload, List<OtlpMetric> metrics)
    {
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 2 && wireType == ProtoReader.WireLengthDelimited)
            {
                metrics.Add(ReadMetric(reader.ReadLengthDelimited()));
            }
            else
            {
                reader.Skip(wireType);
            }
        }
    }

    private static OtlpMetric ReadMetric(ReadOnlyMemory<byte> payload)
    {
        var name = string.Empty;
        OtlpGauge? gauge = null;
        OtlpSum? sum = null;
        var reader = new ProtoReader(payload);

        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                name = reader.ReadString();
            }
            else if (fieldNumber == 5 && wireType == ProtoReader.WireLengthDelimited)
            {
                gauge = ReadGauge(reader.ReadLengthDelimited());
            }
            else if (fieldNumber == 7 && wireType == ProtoReader.WireLengthDelimited)
            {
                sum = ReadSum(reader.ReadLengthDelimited());
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpMetric(name, gauge, sum);
    }

    private static OtlpGauge ReadGauge(ReadOnlyMemory<byte> payload)
    {
        var dataPoints = new List<OtlpNumberDataPoint>();
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                dataPoints.Add(ReadNumberDataPoint(reader.ReadLengthDelimited()));
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpGauge(dataPoints);
    }

    private static OtlpSum ReadSum(ReadOnlyMemory<byte> payload)
    {
        var dataPoints = new List<OtlpNumberDataPoint>();
        var temporality = 0;
        var isMonotonic = false;
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                dataPoints.Add(ReadNumberDataPoint(reader.ReadLengthDelimited()));
            }
            else if (fieldNumber == 2 && wireType == ProtoReader.WireVarint)
            {
                temporality = checked((int)reader.ReadVarint());
            }
            else if (fieldNumber == 3 && wireType == ProtoReader.WireVarint)
            {
                isMonotonic = reader.ReadVarint() != 0;
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpSum(dataPoints, temporality, isMonotonic);
    }

    private static OtlpNumberDataPoint ReadNumberDataPoint(ReadOnlyMemory<byte> payload)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        ulong startTimeUnixNano = 0;
        ulong timeUnixNano = 0;
        double value = 0;
        var reader = new ProtoReader(payload);

        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 2 && wireType == ProtoReader.WireFixed64)
            {
                startTimeUnixNano = reader.ReadFixed64();
            }
            else if (fieldNumber == 3 && wireType == ProtoReader.WireFixed64)
            {
                timeUnixNano = reader.ReadFixed64();
            }
            else if (fieldNumber == 4 && wireType == ProtoReader.WireFixed64)
            {
                value = reader.ReadDouble();
            }
            else if (fieldNumber == 7 && wireType == ProtoReader.WireLengthDelimited)
            {
                var attribute = ReadKeyValue(reader.ReadLengthDelimited());
                attributes[attribute.Key] = attribute.Value;
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpNumberDataPoint(startTimeUnixNano, timeUnixNano, value, attributes);
    }

    private static KeyValuePair<string, string> ReadKeyValue(ReadOnlyMemory<byte> payload)
    {
        var key = string.Empty;
        var value = string.Empty;
        var reader = new ProtoReader(payload);

        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                key = reader.ReadString();
            }
            else if (fieldNumber == 2 && wireType == ProtoReader.WireLengthDelimited)
            {
                value = ReadAnyValue(reader.ReadLengthDelimited());
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new KeyValuePair<string, string>(key, value);
    }

    private static string ReadAnyValue(ReadOnlyMemory<byte> payload)
    {
        var reader = new ProtoReader(payload);
        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            if (fieldNumber == 1 && wireType == ProtoReader.WireLengthDelimited)
            {
                return reader.ReadString();
            }

            reader.Skip(wireType);
        }

        return string.Empty;
    }

    private sealed record OtlpMetric(string Name, OtlpGauge? Gauge, OtlpSum? Sum);

    private sealed record OtlpGauge(IReadOnlyList<OtlpNumberDataPoint> DataPoints);

    private sealed record OtlpSum(
        IReadOnlyList<OtlpNumberDataPoint> DataPoints,
        int Temporality,
        bool IsMonotonic);

    private sealed record OtlpNumberDataPoint(
        ulong StartTimeUnixNano,
        ulong TimeUnixNano,
        double Value,
        IReadOnlyDictionary<string, string> Attributes);

    private sealed class SequenceClock
    {
        private readonly long[] _values;
        private int _index;

        public SequenceClock(params long[] values)
        {
            _values = values;
        }

        public long Next()
        {
            var index = Interlocked.Increment(ref _index) - 1;
            return _values[Math.Min(index, _values.Length - 1)];
        }
    }

    private sealed class ProtoReader
    {
        public const int WireVarint = 0;
        public const int WireFixed64 = 1;
        public const int WireLengthDelimited = 2;

        private readonly ReadOnlyMemory<byte> _data;
        private int _position;

        public ProtoReader(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public bool TryReadField(out int fieldNumber, out int wireType)
        {
            if (_position >= _data.Length)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            var tag = ReadVarint();
            if (tag == 0)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            fieldNumber = checked((int)(tag >> 3));
            wireType = (int)(tag & 0x7);
            return true;
        }

        public ReadOnlyMemory<byte> ReadLengthDelimited()
        {
            var length = checked((int)ReadVarint());
            EnsureAvailable(length);
            var value = _data.Slice(_position, length);
            _position += length;
            return value;
        }

        public string ReadString() =>
            Encoding.UTF8.GetString(ReadLengthDelimited().Span);

        public ulong ReadFixed64()
        {
            EnsureAvailable(sizeof(ulong));
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Span.Slice(_position, sizeof(ulong)));
            _position += sizeof(ulong);
            return value;
        }

        public double ReadDouble() =>
            BitConverter.Int64BitsToDouble((long)ReadFixed64());

        public ulong ReadVarint()
        {
            ulong value = 0;
            var shift = 0;
            while (shift < 64)
            {
                EnsureAvailable(1);
                var next = _data.Span[_position++];
                value |= (ulong)(next & 0x7f) << shift;
                if ((next & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }

            throw new InvalidDataException("Invalid protobuf varint.");
        }

        public void Skip(int wireType)
        {
            switch (wireType)
            {
                case WireVarint:
                    _ = ReadVarint();
                    break;
                case WireFixed64:
                    EnsureAvailable(sizeof(ulong));
                    _position += sizeof(ulong);
                    break;
                case WireLengthDelimited:
                    var length = checked((int)ReadVarint());
                    EnsureAvailable(length);
                    _position += length;
                    break;
                case 5:
                    EnsureAvailable(sizeof(uint));
                    _position += sizeof(uint);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported protobuf wire type {wireType}.");
            }
        }

        private void EnsureAvailable(int length)
        {
            if (length < 0 || _position + length > _data.Length)
            {
                throw new InvalidDataException("Unexpected end of protobuf payload.");
            }
        }
    }
}
