namespace Dekaf.Pipeline.Modules;

public class RunSerializationIntegrationTestsModule : RunIntegrationTestsModule
{
    protected override string Category => "Serialization";
    protected override int? MaximumParallelTests => 1;
}
