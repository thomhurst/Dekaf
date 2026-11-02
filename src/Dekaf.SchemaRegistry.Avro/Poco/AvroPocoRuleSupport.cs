using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>
/// NativeAOT support for rule features of generated POCO serializers. Rule processing parses schemas with
/// Apache.Avro, which uses Newtonsoft.Json reflection. Rule-only code is guarded by
/// <see cref="RuntimeFeature.IsDynamicCodeSupported"/>, which NativeAOT compiles as <see langword="false"/>,
/// so the compiler removes those branches and the default path reaches no Apache.Avro code.
/// </summary>
internal static class AvroPocoRuleSupport
{
    internal static void ThrowIfConfigured(
        ISchemaRegistryRuleExecutor? ruleExecutor,
        ValidationRulesExecution validationRulesExecution,
        bool migrations)
    {
        if (ruleExecutor is not null)
            throw CreateNotSupportedException("RuleExecutor");
        if (validationRulesExecution != ValidationRulesExecution.Disabled)
            throw CreateNotSupportedException("ValidationRulesExecution");
        if (migrations)
            throw CreateNotSupportedException("UseLatestVersion");
    }

    internal static NotSupportedException CreateNotSupportedException(string setting = "Schema Registry rules") =>
        new($"{setting} is not supported by generated Avro POCO serializers under NativeAOT: rules, inline " +
            "validation and migrations parse schemas with Apache.Avro, which requires dynamic code. " +
            "Remove the setting or run with the JIT.");
}
