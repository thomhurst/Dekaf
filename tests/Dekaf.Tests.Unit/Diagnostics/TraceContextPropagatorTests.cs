using System.Diagnostics;
using Dekaf.Diagnostics;
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
        await Assert.That(traceparent!).StartsWith("00-");
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
