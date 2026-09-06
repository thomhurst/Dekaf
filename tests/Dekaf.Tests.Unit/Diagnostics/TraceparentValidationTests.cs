using System.Diagnostics;
using System.Text;
using Dekaf.Diagnostics;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Diagnostics;

public sealed class TraceparentValidationTests
{
    private const string Suffix = "-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    [Test]
    [Arguments("00-00000000000000000000000000000000-00f067aa0ba902b7-01")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01")]
    [Arguments("00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00F067AA0BA902B7-01")]
    [Arguments("ff" + Suffix)]
    [Arguments("zz" + Suffix)]
    [Arguments("0A" + Suffix)]
    [Arguments("00" + Suffix + "-extra")]
    [Arguments("00" + Suffix + "-")]
    [Arguments("01" + Suffix + "extra")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-0F")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-0z")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-0")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736_00f067aa0ba902b7-01")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7_01")]
    [Arguments("00_4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")]
    [Arguments(" 00" + Suffix)]
    [Arguments("00" + Suffix + " ")]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e473é-00f067aa0ba902b7-01")]
    [Arguments("")]
    public async Task MalformedTraceparent_IsIgnored(string value)
    {
        var headers = new Headers().Add("traceparent", value).Add("tracestate", "vendor=value");
        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNull();
    }

    [Test]
    [Arguments("00" + Suffix)]
    [Arguments("01" + Suffix)]
    [Arguments("0a" + Suffix)]
    [Arguments("fe" + Suffix)]
    [Arguments("01" + Suffix + "-")]
    [Arguments("01" + Suffix + "-future-fields")]
    public async Task ValidVersion_PreservesContextAndTracestate(string value)
    {
        var headers = new Headers().Add("traceparent", value).Add("tracestate", "vendor=value");
        var result = TraceContextPropagator.ExtractTraceContext(headers);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.TraceId.ToHexString()).IsEqualTo("4bf92f3577b34da6a3ce929d0e0e4736");
        await Assert.That(result.Value.SpanId.ToHexString()).IsEqualTo("00f067aa0ba902b7");
        await Assert.That(result.Value.TraceFlags).IsEqualTo(ActivityTraceFlags.Recorded);
        await Assert.That(result.Value.TraceState).IsEqualTo("vendor=value");
        await Assert.That(result.Value.IsRemote).IsTrue();
    }

    [Test]
    public async Task MinimalNonzeroIds_AreValid()
    {
        var result = TraceContextPropagator.ExtractTraceContext(new Headers().Add("traceparent",
            "00-00000000000000000000000000000001-0000000000000001-00"));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.TraceFlags).IsEqualTo(ActivityTraceFlags.None);
    }

    [Test]
    public async Task NullAndInvalidUtf8Values_AreIgnored()
    {
        await Assert.That(TraceContextPropagator.ExtractTraceContext(new Headers().Add("traceparent", (byte[]?)null))).IsNull();
        var bytes = Encoding.UTF8.GetBytes("00" + Suffix);
        bytes[3] = 0xff;
        await Assert.That(TraceContextPropagator.ExtractTraceContext(new Headers().Add("traceparent", bytes))).IsNull();
    }
}
