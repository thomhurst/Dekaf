using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dekaf.Consumer;

namespace Dekaf.Diagnostics;

internal sealed class ConsumerFetchBufferStateSource(
    FetchBufferMemoryPool pool,
    string? clientId,
    string? groupId)
{
    public Measurement<long> UsedBytes() => new(pool.UsedBytes, CreateTags());
    public Measurement<long> FreeBytes() => new(pool.FreeBytes, CreateTags());
    public Measurement<double> DepletedPercent() => new(pool.DepletedPercent, CreateTags());
    public Measurement<double> DepletedDuration() => new(pool.DepletedDurationSeconds, CreateTags());

    private TagList CreateTags()
    {
        TagList tags = default;
        if (clientId is not null)
            tags.Add(DekafDiagnostics.MessagingClientId, clientId);
        if (groupId is not null)
            tags.Add(DekafDiagnostics.MessagingConsumerGroupName, groupId);
        return tags;
    }
}
