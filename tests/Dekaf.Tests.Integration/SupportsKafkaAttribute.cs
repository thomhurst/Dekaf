namespace Dekaf.Tests.Integration;

public class SupportsKafkaAttribute(string supportedKafkaVersion) : SkipAttribute(string.Empty)
{
    private readonly Version _supportedKafkaVersion = Version.Parse(supportedKafkaVersion);

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        var kafkaVersionUsedInTest = GetKafkaVersionUsedInTest(context);

        return Task.FromResult(kafkaVersionUsedInTest < _supportedKafkaVersion);
    }

    protected override string GetSkipReason(TestRegisteredContext context)
    {
        var kafkaVersionUsedInTest = GetKafkaVersionUsedInTest(context);

        return $"The test requires Kafka {_supportedKafkaVersion} or above, but this test is testing {kafkaVersionUsedInTest}";
    }

    private static Version GetKafkaVersionUsedInTest(TestRegisteredContext context)
    {
        return context.TestContext
            .Metadata
            .TestDetails
            .TestClassArguments
            .OfType<KafkaTestContainer>()
            .First().Version;
    }
}
