namespace Dekaf.Tests.Integration;

/// <summary>
/// Skips a test on brokers older than <paramref name="supportedKafkaVersion"/>.
/// When a feature has not shipped in any released broker yet, pass a
/// <paramref name="reason"/> that names the KIP and the issue that tracks it.
/// </summary>
public class SupportsKafkaAttribute(int supportedKafkaVersion, string? reason = null) : SkipAttribute(string.Empty)
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

        var message = $"The test requires Kafka {supportedKafkaVersion} or above, but this test is testing {kafkaVersionUsedInTest}";
        return reason is null ? message : $"{message}: {reason}";
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
