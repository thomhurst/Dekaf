using System.Buffers;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Shared helpers for consumer unit tests.
/// </summary>
internal static class ConsumerTestHelpers
{
    /// <summary>
    /// Builds a consumer protocol assignment byte array containing the given topic-partitions.
    /// Uses the same wire format as ConsumerCoordinator.BuildAssignmentData.
    /// </summary>
    internal static byte[] BuildAssignmentData(string topic, int[] partitions)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // Version
        writer.WriteInt16(0);

        // Topics array
        var topicAssignments = new List<(string Topic, int[] Partitions)> { (topic, partitions) };
        writer.WriteArray(
            topicAssignments,
            (ref KafkaProtocolWriter w, (string Topic, int[] Partitions) tp) =>
            {
                w.WriteString(tp.Topic);
                w.WriteArray(
                    tp.Partitions,
                    (ref KafkaProtocolWriter w2, int partition) => w2.WriteInt32(partition));
            });

        // User data
        writer.WriteBytes([]);

        return buffer.WrittenSpan.ToArray();
    }
}
