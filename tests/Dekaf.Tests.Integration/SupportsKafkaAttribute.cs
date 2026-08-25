namespace Dekaf.Tests.Integration;

public class SupportsKafkaAttribute(int supportedKafkaVersion) : SkipAttribute(string.Empty)
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        var kafkaContainer = GetKafkaContainerUsedInTest(context);

        return Task.FromResult(kafkaContainer is KafkaContainerDefault
            ? !KafkaContainerDefault.SupportsVersion(KafkaContainerDefault.ImageTag, supportedKafkaVersion)
            : kafkaContainer.Version < supportedKafkaVersion);
    }

    protected override string GetSkipReason(TestRegisteredContext context)
    {
        var kafkaContainer = GetKafkaContainerUsedInTest(context);
        var kafkaVersionUsedInTest = kafkaContainer is KafkaContainerDefault
            ? KafkaContainerDefault.ImageTag
            : kafkaContainer.Version.ToString();

        return $"The test requires Kafka {supportedKafkaVersion} or above, but this test is testing {kafkaVersionUsedInTest}";
    }

    private static KafkaTestContainer GetKafkaContainerUsedInTest(TestRegisteredContext context)
    {
        return context.TestContext
            .Metadata
            .TestDetails
            .TestClassArguments
            .OfType<KafkaTestContainer>()
            .First();
    }
}
