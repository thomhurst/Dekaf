namespace Dekaf.Pipeline.Modules;

public class RunStressTestsUnitTestsModule : TestBaseModule
{
    protected override IEnumerable<string> TestableFrameworks
    {
        get
        {
            // The stress harness targets net10 only, but the unit-test pipeline also has a net8 lane.
            if (string.Equals(GetTargetFramework(), "net10.0", StringComparison.OrdinalIgnoreCase))
                yield return "net10.0";
        }
    }

    protected override string ProjectFileName => "Dekaf.StressTests.Tests.csproj";
}
