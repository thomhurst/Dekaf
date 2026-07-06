namespace Dekaf.Pipeline.Modules;

public class RunShareConsumerAdminIntegrationTestsModule : RunIntegrationTestsModule
{
    protected override string Category => "ShareConsumerAdmin";
    protected override int? MaximumParallelTests => 1;
}
