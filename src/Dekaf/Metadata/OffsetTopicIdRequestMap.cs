using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Metadata;

/// <summary>
/// Request-local topic name/ID mapping for OffsetCommit and OffsetFetch v10.
/// </summary>
internal sealed class OffsetTopicIdRequestMap
{
    private readonly ClusterMetadata _metadata;
    private readonly ClusterMetadataSnapshot _requestSnapshot;
    private readonly Dictionary<Guid, string> _unmatchedTopics;

    public OffsetTopicIdRequestMap(ClusterMetadata metadata, int topicCount)
    {
        _metadata = metadata;
        _requestSnapshot = metadata.CaptureSnapshot();
        _unmatchedTopics = new Dictionary<Guid, string>(topicCount);
    }

    public Guid AddTopic(string topicName, string operation)
    {
        if (!_requestSnapshot.Topics.TryGetValue(topicName, out var topic)
            || topic.TopicId == Guid.Empty)
        {
            throw CreateUnknownTopicIdException(
                $"{operation} requires a topic ID for '{topicName}', but metadata has no current mapping");
        }

        if (!_unmatchedTopics.TryAdd(topic.TopicId, topicName))
        {
            throw CreateUnknownTopicIdException(
                $"{operation} metadata maps more than one requested topic to ID '{topic.TopicId}'");
        }

        return topic.TopicId;
    }

    public string MatchResponseTopic(
        Guid topicId,
        ClusterMetadataSnapshot responseSnapshot,
        string operation,
        bool responseMismatchIsRetriable)
    {
        if (topicId == Guid.Empty || !_unmatchedTopics.Remove(topicId, out var topicName))
        {
            throw CreateResponseMismatchException(
                $"{operation} response contained an unknown, duplicate, or unrequested topic ID '{topicId}'",
                responseMismatchIsRetriable);
        }

        if (!responseSnapshot.Topics.TryGetValue(topicName, out var currentTopic)
            || currentTopic.TopicId != topicId
            || !responseSnapshot.TopicsById.TryGetValue(topicId, out var currentTopicById)
            || !string.Equals(currentTopicById.Name, topicName, StringComparison.Ordinal))
        {
            throw CreateResponseMismatchException(
                $"{operation} topic ID for '{topicName}' changed while the request was in flight",
                responseMismatchIsRetriable);
        }

        return topicName;
    }

    public ClusterMetadataSnapshot CaptureResponseSnapshot() => _metadata.CaptureSnapshot();

    private static KafkaException CreateUnknownTopicIdException(string message) =>
        new(ErrorCode.UnknownTopicId, message);

    private static KafkaException CreateResponseMismatchException(string message, bool isRetriable) =>
        new(ErrorCode.UnknownTopicId, message, isRetriable);
}
