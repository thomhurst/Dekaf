using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Compression;
using CompressionType = Dekaf.Protocol.Records.CompressionType;

namespace Dekaf.Telemetry;

internal sealed class ClientTelemetryPayloadProvider : IClientTelemetryPayloadProvider
{
    private const int WireVarint = 0;
    private const int WireFixed64 = 1;
    private const int WireLengthDelimited = 2;
    private const int AggregationTemporalityDelta = 1;
    private const int AggregationTemporalityCumulative = 2;
    private const long UnixEpochTicks = 621355968000000000L;

    private readonly CompressionCodecRegistry _compressionCodecs;
    private readonly Func<long> _unixNanoClock;
    private readonly long _startTimeUnixNano;
    private long _lastCollectionTimeUnixNano;

    public ClientTelemetryPayloadProvider(
        CompressionCodecRegistry? compressionCodecs = null,
        Func<long>? unixNanoClock = null)
    {
        _compressionCodecs = compressionCodecs ?? CompressionCodecRegistry.Default;
        _unixNanoClock = unixNanoClock ?? GetUnixTimeNanoseconds;
        _startTimeUnixNano = Math.Max(1, _unixNanoClock());
        _lastCollectionTimeUnixNano = _startTimeUnixNano;
    }

    public ReadOnlyMemory<byte> Collect(
        ClientTelemetrySubscription subscription,
        ClientTelemetryMetricSnapshot metrics,
        bool terminating)
    {
        var now = Math.Max(1, _unixNanoClock());
        var startTime = metrics.DeltaTemporality
            ? Interlocked.Exchange(ref _lastCollectionTimeUnixNano, now)
            : _startTimeUnixNano;

        if (metrics.Metrics.Count == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var payload = EncodeMetricsData(metrics, (ulong)Math.Max(1, startTime), (ulong)now);
        var compressionType = (CompressionType)subscription.CompressionType;
        if (compressionType == CompressionType.None)
        {
            return payload.WrittenMemory;
        }

        var compressed = new ArrayBufferWriter<byte>();
        _compressionCodecs.GetCodec(compressionType)
            .Compress(new ReadOnlySequence<byte>(payload.WrittenMemory), compressed);
        return compressed.WrittenMemory;
    }

    private static ArrayBufferWriter<byte> EncodeMetricsData(
        ClientTelemetryMetricSnapshot snapshot,
        ulong startTimeUnixNano,
        ulong timeUnixNano)
    {
        var buffer = new ArrayBufferWriter<byte>();
        WriteMessage(buffer, 1, resourceMetrics =>
        {
            WriteMessage(resourceMetrics, 2, scopeMetrics =>
            {
                foreach (var metric in snapshot.Metrics)
                {
                    WriteMetric(scopeMetrics, metric, startTimeUnixNano, timeUnixNano, snapshot.DeltaTemporality);
                }
            });
        });
        return buffer;
    }

    private static void WriteMetric(
        IBufferWriter<byte> writer,
        ClientTelemetryMetric metric,
        ulong startTimeUnixNano,
        ulong timeUnixNano,
        bool deltaTemporality)
    {
        WriteMessage(writer, 2, metricWriter =>
        {
            WriteString(metricWriter, 1, metric.Name);

            if (metric.Kind == ClientTelemetryMetricKind.Counter)
            {
                WriteMessage(metricWriter, 7, sum =>
                {
                    WriteMessage(sum, 1, point =>
                        WriteNumberDataPoint(point, metric, startTimeUnixNano, timeUnixNano));
                    WriteVarintField(
                        sum,
                        2,
                        deltaTemporality ? AggregationTemporalityDelta : AggregationTemporalityCumulative);
                    WriteVarintField(sum, 3, 1);
                });
            }
            else
            {
                WriteMessage(metricWriter, 5, gauge =>
                {
                    WriteMessage(gauge, 1, point =>
                        WriteNumberDataPoint(point, metric, startTimeUnixNano, timeUnixNano));
                });
            }
        });
    }

    private static void WriteNumberDataPoint(
        IBufferWriter<byte> writer,
        ClientTelemetryMetric metric,
        ulong startTimeUnixNano,
        ulong timeUnixNano)
    {
        WriteFixed64Field(writer, 2, startTimeUnixNano);
        WriteFixed64Field(writer, 3, timeUnixNano);
        WriteDoubleField(writer, 4, metric.Value);

        foreach (var attribute in metric.Attributes)
        {
            WriteMessage(writer, 7, keyValue =>
            {
                WriteString(keyValue, 1, attribute.Name);
                WriteMessage(keyValue, 2, anyValue => WriteString(anyValue, 1, attribute.Value));
            });
        }
    }

    private static void WriteMessage(
        IBufferWriter<byte> writer,
        int fieldNumber,
        Action<IBufferWriter<byte>> writePayload)
    {
        var payload = new ArrayBufferWriter<byte>();
        writePayload(payload);
        WriteTag(writer, fieldNumber, WireLengthDelimited);
        WriteVarint(writer, (ulong)payload.WrittenCount);
        WriteBytes(writer, payload.WrittenSpan);
    }

    private static void WriteString(IBufferWriter<byte> writer, int fieldNumber, string value)
    {
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
        WriteTag(writer, fieldNumber, WireLengthDelimited);
        WriteVarint(writer, (ulong)byteCount);
        var span = writer.GetSpan(byteCount);
        var written = System.Text.Encoding.UTF8.GetBytes(value, span);
        writer.Advance(written);
    }

    private static void WriteVarintField(IBufferWriter<byte> writer, int fieldNumber, int value)
    {
        WriteTag(writer, fieldNumber, WireVarint);
        WriteVarint(writer, (ulong)value);
    }

    private static void WriteFixed64Field(IBufferWriter<byte> writer, int fieldNumber, ulong value)
    {
        WriteTag(writer, fieldNumber, WireFixed64);
        var span = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        writer.Advance(sizeof(ulong));
    }

    private static void WriteDoubleField(IBufferWriter<byte> writer, int fieldNumber, double value)
    {
        WriteFixed64Field(writer, fieldNumber, (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    private static void WriteTag(IBufferWriter<byte> writer, int fieldNumber, int wireType) =>
        WriteVarint(writer, (ulong)((fieldNumber << 3) | wireType));

    private static void WriteVarint(IBufferWriter<byte> writer, ulong value)
    {
        while (value >= 0x80)
        {
            WriteByte(writer, (byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }

        WriteByte(writer, (byte)value);
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    private static void WriteBytes(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var span = writer.GetSpan(value.Length);
        value.CopyTo(span);
        writer.Advance(value.Length);
    }

    private static long GetUnixTimeNanoseconds()
    {
        var ticksSinceUnixEpoch = DateTimeOffset.UtcNow.UtcTicks - UnixEpochTicks;
        return checked(ticksSinceUnixEpoch * 100);
    }
}
