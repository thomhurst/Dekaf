using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro.Poco;

namespace Dekaf.Tests.Unit.SchemaRegistry;

// NativeAOT runs these checks from the POCO constructors; the JIT never reaches them there.
public sealed class AvroPocoRuleSupportTests
{
    [Test]
    public async Task ThrowIfConfigured_NoRuleFeatures_DoesNotThrow()
    {
        await Assert.That(() =>
                AvroPocoRuleSupport.ThrowIfConfigured(null, ValidationRulesExecution.Disabled, migrations: false))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfConfigured_RuleExecutor_ThrowsNotSupported()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            AvroPocoRuleSupport.ThrowIfConfigured(
                new SchemaRegistryRuleExecutor([]),
                ValidationRulesExecution.Disabled,
                migrations: false));

        await Assert.That(exception.Message).Contains("RuleExecutor");
        await Assert.That(exception.Message).Contains("NativeAOT");
    }

    [Test]
    public async Task ThrowIfConfigured_InlineValidation_ThrowsNotSupported()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            AvroPocoRuleSupport.ThrowIfConfigured(null, ValidationRulesExecution.AfterDomainRules, migrations: false));

        await Assert.That(exception.Message).Contains("ValidationRulesExecution");
    }

    [Test]
    public async Task ThrowIfConfigured_Migrations_ThrowsNotSupported()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            AvroPocoRuleSupport.ThrowIfConfigured(null, ValidationRulesExecution.Disabled, migrations: true));

        await Assert.That(exception.Message).Contains("UseLatestVersion");
    }
}
