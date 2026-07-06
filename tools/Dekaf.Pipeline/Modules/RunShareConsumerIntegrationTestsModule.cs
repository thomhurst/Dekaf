namespace Dekaf.Pipeline.Modules;

public class RunShareConsumerIntegrationTestsModule : RunIntegrationTestsModule
{
    protected override string Category => "ShareConsumer";
    protected override int? MaximumParallelTests => 1;
}
