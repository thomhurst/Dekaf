using System.Buffers;
using System.Diagnostics;
using System.Text;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Diagnostics;

[NotInParallel("ActivityListener")]
public sealed class TraceContextPropagatorTests
{
    [Test]
    public async Task InjectTraceContext_NullActivity_ReturnsOriginalHeaders()
    {
        var headers = new Headers().Add("existing", "value");
        var result = TraceContextPropagator.InjectTraceContext(headers, null);
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    [Test]
    public async Task InjectTraceContext_NullActivityNullHeaders_ReturnsNull()
    {
        var result = TraceContextPropagator.InjectTraceContext(null, null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task InjectTraceContext_WithActivity_AddsTraceparentHeader()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        var result = TraceContextPropagator.InjectTraceContext(null, activity);
        await Assert.That(result).IsNotNull();

        var traceparent = result!.GetFirstAsString("traceparent");
        await Assert.That(traceparent).IsNotNull();
        await Assert.That(traceparent).IsEqualTo(
            $"00-{activity!.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}");
    }

    [Test]
    public async Task InjectTraceContext_DeferredHeaderWritesExpectedWireValue()
    {
        using var activity = new Activity("trace-wire")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var headers = TraceContextPropagator.InjectTraceContext(new Headers(1), activity)!;
        var header = headers[0];
        var output = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(output);

        HeaderProtocol.Write(in header, ref writer);

        await Assert.That(output.WrittenSpan[0]).IsEqualTo((byte)22);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan.Slice(1, 11))).IsEqualTo("traceparent");
        await Assert.That(output.WrittenSpan[12]).IsEqualTo((byte)110);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan[13..])).IsEqualTo(
            $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}");
    }

    [Test]
    public async Task DeferredHeader_WithValue_UsesReplacementValue()
    {
        using var activity = new Activity("trace-replacement")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var deferred = Header.CreateDeferredTraceparent("traceparent", activity);
        var replacement = "replacement"u8.ToArray();

        var replaced = deferred with { Value = replacement };

        await Assert.That(replaced.Value.Span.SequenceEqual(replacement)).IsTrue();
        await Assert.That(replaced.GetValueAsString()).IsEqualTo("replacement");
    }

    [Test]
    public async Task DeferredHeader_WithUnexpectedValue_FailsBeforeWritingMalformedRecord()
    {
        var header = Header.CreateDeferredTraceparent("traceparent", new object());
        var output = new ArrayBufferWriter<byte>();

        void Act()
        {
            var writer = new KafkaProtocolWriter(output);
            HeaderProtocol.Write(in header, ref writer);
        }

        await Assert.That(Act).Throws<InvalidOperationException>()
            .WithMessage("Unsupported deferred header value.");
    }

    [Test]
    public async Task InjectTraceContext_DeferredTracestateWritesExpectedWireValue()
    {
        using var activity = new Activity("tracestate-wire")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=välue";
        var header = TraceContextPropagator.InjectTraceContext(new Headers(2), activity)![1];
        var output = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(output);

        HeaderProtocol.Write(in header, ref writer);
        var encoded = new byte[HeaderProtocol.CalculateSize(in header)];
        var offset = 0;
        HeaderProtocol.Encode(in header, encoded, ref offset);
        var encodedMatchesWriter = encoded.AsSpan().SequenceEqual(output.WrittenSpan);

        await Assert.That(output.WrittenSpan[0]).IsEqualTo((byte)20);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan.Slice(1, 10))).IsEqualTo("tracestate");
        await Assert.That(output.WrittenSpan[11]).IsEqualTo((byte)26);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan[12..])).IsEqualTo("vendor=välue");
        await Assert.That(offset).IsEqualTo(encoded.Length);
        await Assert.That(encodedMatchesWriter).IsTrue();
    }

    [Test]
    public async Task ReturnPooledHeaders_ClearsDeferredActivityReference()
    {
        using var activity = new Activity("trace-retention")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=value";
        var headers = TraceContextPropagator.InjectTraceContext(new Headers(2), activity)!;
        var pooledHeaders = ProducerContainerPools.Headers.Rent(headers.Count);
        for (var i = 0; i < headers.Count; i++)
            pooledHeaders[i] = headers[i];

        RecordAccumulator.ReturnPooledHeaders(pooledHeaders, headers.Count);

        await Assert.That(pooledHeaders[0].HasDeferredTraceparent).IsFalse();
        await Assert.That(pooledHeaders[0]).IsEqualTo(default(Header));
    }

    [Test]
    public async Task ReturnPooledHeaders_ClearsTrackedActivityBeforeAppendedHeaders()
    {
        using var activity = new Activity("trace-retention-with-appended-header")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=value";
        var headers = new Headers(4).Add("existing", "value");
        TraceContextPropagator.InjectTraceContext(headers, activity);
        headers.Add("serializer", "appended");

        KafkaProducer<string, string>.RentAndFillHeaders(
            headers,
            out var pooledHeaders,
            out var headerCount);
        RecordAccumulator.ReturnPooledHeaders(pooledHeaders, headerCount);

        await Assert.That(pooledHeaders[0].Key).IsEqualTo("existing");
        await Assert.That(pooledHeaders[1].Key).IsEqualTo("serializer");
        await Assert.That(pooledHeaders[2]).IsEqualTo(default(Header));
        await Assert.That(pooledHeaders[3].Key).IsEqualTo("tracestate");
    }

    [Test]
    public async Task RemoveDeferredTraceContext_RestoresCallerOwnedHeaders()
    {
        using var firstActivity = new Activity("first-trace")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        firstActivity.TraceStateString = "vendor=value";
        var headers = new Headers(3).Add("existing", "value");

        TraceContextPropagator.InjectTraceContext(headers, firstActivity);
        headers.RemoveDeferredTraceContext();

        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo("existing");
        await Assert.That(headers[0].GetValueAsString()).IsEqualTo("value");
    }

    [Test]
    public async Task RemoveDeferredTraceContext_PreservesHeadersAppendedAfterInjection()
    {
        using var activity = new Activity("trace-with-serializer-header")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=value";
        var headers = new Headers(4).Add("existing", "value");

        TraceContextPropagator.InjectTraceContext(headers, activity);
        headers.Add("serializer", "appended");
        headers.RemoveDeferredTraceContext();

        await Assert.That(headers.Count).IsEqualTo(2);
        await Assert.That(headers[0].Key).IsEqualTo("existing");
        await Assert.That(headers[1].Key).IsEqualTo("serializer");
        await Assert.That(headers[1].GetValueAsString()).IsEqualTo("appended");
    }

    [Test]
    public async Task RemoveDeferredTraceContext_TracksHeadersRemovedBeforeInjectionSlot()
    {
        using var activity = new Activity("trace-with-removed-leading-header")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=value";
        var headers = new Headers(5)
            .Add("remove", "value")
            .Add("existing", "value");

        TraceContextPropagator.InjectTraceContext(headers, activity);
        headers.Remove("remove");
        headers.Add("serializer", "appended");
        headers.RemoveDeferredTraceContext();

        await Assert.That(headers.Count).IsEqualTo(2);
        await Assert.That(headers[0].Key).IsEqualTo("existing");
        await Assert.That(headers[1].Key).IsEqualTo("serializer");
    }

    [Test]
    public async Task RemoveDeferredTraceContext_RemovesTracestateAfterTraceparentRemoval()
    {
        using var activity = new Activity("trace-with-removed-traceparent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.TraceStateString = "vendor=value";
        var headers = TraceContextPropagator.InjectTraceContext(new Headers(2), activity)!;

        headers.Remove("traceparent");
        headers.RemoveDeferredTraceContext();

        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InjectTraceContext_WithTracestate_AddsBothHeaders()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();
        activity!.TraceStateString = "vendor=value";

        var result = TraceContextPropagator.InjectTraceContext(null, activity);
        await Assert.That(result).IsNotNull();

        var traceparent = result!.GetFirstAsString("traceparent");
        var tracestate = result.GetFirstAsString("tracestate");
        await Assert.That(traceparent).IsNotNull();
        await Assert.That(tracestate).IsEqualTo("vendor=value");
    }

    [Test]
    public async Task ExtractTraceContext_NullHeaders_ReturnsNull()
    {
        var result = TraceContextPropagator.ExtractTraceContext(null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ExtractTraceContext_EmptyHeaders_ReturnsNull()
    {
        var headers = new List<Header>();
        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ExtractTraceContext_ValidTraceparent_ReturnsActivityContext()
    {
        var headers = new List<Header>
        {
            new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"u8.ToArray())
        };

        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.TraceId.ToString()).IsEqualTo("4bf92f3577b34da6a3ce929d0e0e4736");
        await Assert.That(result.Value.SpanId.ToString()).IsEqualTo("00f067aa0ba902b7");
        await Assert.That(result.Value.TraceFlags).IsEqualTo(ActivityTraceFlags.Recorded);
        await Assert.That(result.Value.IsRemote).IsTrue();
    }

    [Test]
    public async Task ExtractTraceContext_InvalidTraceparent_ReturnsNull()
    {
        var headers = new List<Header>
        {
            new("traceparent", "invalid"u8.ToArray())
        };

        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ExtractTraceContext_TraceparentWithTracestate_ReturnsBoth()
    {
        var headers = new List<Header>
        {
            new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"u8.ToArray()),
            new("tracestate", "vendor=value"u8.ToArray())
        };

        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.TraceState).IsEqualTo("vendor=value");
    }

    [Test]
    public async Task ExtractTraceContext_UnrecordedFlags_ReturnsNone()
    {
        var headers = new List<Header>
        {
            new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"u8.ToArray())
        };

        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.TraceFlags).IsEqualTo(ActivityTraceFlags.None);
    }

    [Test]
    public async Task RoundTrip_InjectExtract_PreservesTraceIdAndSpanId()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test", ActivityKind.Producer);
        await Assert.That(activity).IsNotNull();

        // Inject
        var headers = TraceContextPropagator.InjectTraceContext(null, activity);
        await Assert.That(headers).IsNotNull();

        // Convert to IReadOnlyList<Header> for extract
        var headerList = headers!.ToList();

        // Extract
        var extracted = TraceContextPropagator.ExtractTraceContext(headerList);
        await Assert.That(extracted).IsNotNull();
        await Assert.That(extracted!.Value.TraceId.ToString()).IsEqualTo(activity!.TraceId.ToString());
        await Assert.That(extracted.Value.SpanId.ToString()).IsEqualTo(activity.SpanId.ToString());
    }

    [Test]
    public async Task ExtractTraceContext_NoTraceparentHeader_ReturnsNull()
    {
        var headers = new List<Header>
        {
            new("other-header", "value"u8.ToArray())
        };

        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task InjectTraceContext_PreservesExistingHeaders()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = DekafDiagnostics.Source.StartActivity("test", ActivityKind.Producer);
        var headers = new Headers().Add("existing", "value");

        var result = TraceContextPropagator.InjectTraceContext(headers, activity);
        await Assert.That(result).IsSameReferenceAs(headers);
        await Assert.That(result!.GetFirstAsString("existing")).IsEqualTo("value");
        await Assert.That(result.GetFirstAsString("traceparent")).IsNotNull();
    }
}
