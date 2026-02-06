namespace Dekaf.Tests.Integration;

public class SupportsKafkaAttribute(int supportedKafkaVersion) : SkipAttribute(string.Empty)
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        var kafkaVersionUsedInTest = GetKafkaVersionUsedInTest(context);

        return Task.FromResult(kafkaVersionUsedInTest < supportedKafkaVersion);
    }

    protected override string GetSkipReason(TestRegisteredContext context)
    {
        var kafkaVersionUsedInTest = GetKafkaVersionUsedInTest(context);
        
        return $"The test requires Kafka {supportedKafkaVersion} or above, but this test is testing {kafkaVersionUsedInTest}";
    }

    private static int GetKafkaVersionUsedInTest(TestRegisteredContext context)
    {
        return context.TestContext
            .Metadata
            .TestDetails
            .TestClassArguments
            .OfType<KafkaTestContainer>()
            .First().Version;
    }
}