using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ApiKeyTests
{
    [Test]
    public async Task StreamsGroupApiKeys_HaveKafkaAssignedValues()
    {
        var heartbeat = Enum.Parse<ApiKey>(nameof(ApiKey.StreamsGroupHeartbeat));
        var describe = Enum.Parse<ApiKey>(nameof(ApiKey.StreamsGroupDescribe));

        await Assert.That((short)heartbeat).IsEqualTo((short)88);
        await Assert.That((short)describe).IsEqualTo((short)89);
    }
}
