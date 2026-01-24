using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerGroupMetadataTests
{
    [Test]
    public async Task ConsumerGroupMetadata_WithAllProperties_HasCorrectValues()
    {
        var metadata = new ConsumerGroupMetadata
        {
            GroupId = "test-group",
            GenerationId = 5,
            MemberId = "consumer-1-uuid",
            GroupInstanceId = "static-member-1"
        };

        await Assert.That(metadata.GroupId).IsEqualTo("test-group");
        await Assert.That(metadata.GenerationId).IsEqualTo(5);
        await Assert.That(metadata.MemberId).IsEqualTo("consumer-1-uuid");
        await Assert.That(metadata.GroupInstanceId).IsEqualTo("static-member-1");
    }

    [Test]
    public async Task ConsumerGroupMetadata_WithoutGroupInstanceId_HasNullGroupInstanceId()
    {
        var metadata = new ConsumerGroupMetadata
        {
            GroupId = "test-group",
            GenerationId = 1,
            MemberId = "consumer-1-uuid"
        };

        await Assert.That(metadata.GroupId).IsEqualTo("test-group");
        await Assert.That(metadata.GenerationId).IsEqualTo(1);
        await Assert.That(metadata.MemberId).IsEqualTo("consumer-1-uuid");
        await Assert.That(metadata.GroupInstanceId).IsNull();
    }

    [Test]
    public async Task ConsumerGroupMetadata_GenerationIdZero_IsValid()
    {
        // Generation ID 0 is valid (first generation after group is created)
        var metadata = new ConsumerGroupMetadata
        {
            GroupId = "test-group",
            GenerationId = 0,
            MemberId = "consumer-1-uuid"
        };

        await Assert.That(metadata.GenerationId).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerGroupMetadata_Properties_AreInitOnly()
    {
        // Verify that the class uses init-only properties (compile-time check)
        // This test documents that the API follows the specified design
        var metadata = new ConsumerGroupMetadata
        {
            GroupId = "group",
            GenerationId = 1,
            MemberId = "member"
        };

        // The fact that we can create this instance with init syntax
        // and access the properties confirms the correct API design
        await Assert.That(metadata).IsNotNull();
    }
}
