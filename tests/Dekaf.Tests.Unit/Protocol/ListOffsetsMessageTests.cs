using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ListOffsetsMessageTests
{
    [Test]
    [Arguments((short)9, false)]
    [Arguments((short)10, true)]
    [Arguments((short)11, true)]
    public async Task Request_WritesRemoteReadTimeoutFromV10(short version, bool hasTimeout)
    {
        var request = new ListOffsetsRequest
        {
            Topics = [],
            TimeoutMs = 12_345
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        request.Write(ref writer, version);

        var reader = new KafkaProtocolReader(buffer.WrittenSpan);
        var replicaId = reader.ReadInt32();
        var isolationLevel = reader.ReadInt8();
        var topicsLength = reader.ReadUnsignedVarInt();
        var timeoutMs = hasTimeout ? reader.ReadInt32() : 0;
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;

        await Assert.That(replicaId).IsEqualTo(-1);
        await Assert.That(isolationLevel).IsEqualTo((sbyte)IsolationLevel.ReadUncommitted);
        await Assert.That(topicsLength).IsEqualTo(1);
        if (hasTimeout)
            await Assert.That(timeoutMs).IsEqualTo(12_345);
        await Assert.That(remaining).IsEqualTo(0);
    }
}
