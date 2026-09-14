using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class SharedClientTelemetryTests
{
    [Test]
    public async Task SharedConnection_AttributesRequestsToTheirLogicalClient()
    {
        var first = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        var second = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        await using var connection = new KafkaConnection("localhost", 9092);
        var fetch = new FetchRequest();
        var target = (IRequestWriteSequenceTarget)fetch;
        target.WriteSequenceSource = new Source(first);
        await Assert.That(connection.GetTelemetryMetricCollector(fetch)).IsSameReferenceAs(first);
        target.WriteSequenceSource = new Source(second);
        await Assert.That(connection.GetTelemetryMetricCollector(fetch)).IsSameReferenceAs(second);
        target.WriteSequenceSource = null;
        await Assert.That(connection.GetTelemetryMetricCollector(fetch)).IsNull();
        var offsets = new ListOffsetsRequest { Topics = [] };
        ((IRequestWriteSequenceTarget)offsets).WriteSequenceSource = new Source(second);
        await Assert.That(connection.GetTelemetryMetricCollector(offsets)).IsSameReferenceAs(second);
        ((IRequestWriteSequenceTarget)offsets).WriteSequenceSource = null;
        await Assert.That(connection.GetTelemetryMetricCollector(offsets)).IsNull();
        var produce = new ProduceRequest { TelemetryMetricCollector = first };
        await Assert.That(connection.GetTelemetryMetricCollector(produce)).IsSameReferenceAs(first);
        produce.TelemetryMetricCollector = null;
        await Assert.That(connection.GetTelemetryMetricCollector(produce)).IsNull();
        var commit = new OffsetCommitRequest { GroupId = "group", Topics = [] };
        await Assert.That(connection.GetTelemetryMetricCollector(commit, second)).IsSameReferenceAs(second);
        await Assert.That(connection.GetTelemetryMetricCollector(commit)).IsNull();
    }

    [Test]
    public async Task OwnedConnection_PreservesItsCollector()
    {
        var owner = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var other = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        await using var connection = new KafkaConnection("localhost", 9092, null, null, null,
            ResponseBufferPool.Default, owner);
        var request = new ProduceRequest { TelemetryMetricCollector = other };
        await Assert.That(connection.GetTelemetryMetricCollector(request, other)).IsSameReferenceAs(owner);
    }

    private sealed class Source(ClientTelemetryMetricCollector collector) : IRequestWriteSequenceSource, IClientTelemetrySource
    {
        public ClientTelemetryMetricCollector? TelemetryMetricCollector => collector;
        public long NextRequestWriteSequence() => 0;
    }
}
