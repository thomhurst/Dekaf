using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>
/// Trimming and NativeAOT support for rule features of generated POCO serializers. Rule processing parses
/// schemas with Apache.Avro, which uses Newtonsoft.Json reflection. Rule-only code is guarded by
/// <see cref="IsSupported"/>, so a trimmed or NativeAOT publish removes those branches and the default path
/// reaches no Apache.Avro code.
/// </summary>
internal static class AvroPocoRuleSupport
{
    /// <summary>
    /// Runtime configuration switch. The package's build targets set it to <see langword="false"/> when
    /// <c>PublishTrimmed</c> is <see langword="true"/> (including NativeAOT), unless the app sets the
    /// <c>DekafAvroPocoRulesSupport</c> MSBuild property.
    /// </summary>
    internal const string SwitchName = "Dekaf.SchemaRegistry.Avro.Poco.RulesSupport.IsSupported";

    private static readonly bool s_switchEnabled =
        !AppContext.TryGetSwitch(SwitchName, out var enabled) || enabled;

    /// <summary>
    /// Whether generated POCO serializers may run rules, inline validation and migrations. The trimmer
    /// substitutes this property when <see cref="SwitchName"/> is set at publish time, and NativeAOT compiles
    /// <see cref="RuntimeFeature.IsDynamicCodeSupported"/> as <see langword="false"/>.
    /// </summary>
    [FeatureSwitchDefinition(SwitchName)]
    internal static bool IsSupported => RuntimeFeature.IsDynamicCodeSupported && s_switchEnabled;

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
        new($"{setting} is not supported by generated Avro POCO serializers in trimmed or NativeAOT apps: " +
            "rules, inline validation and migrations parse schemas with Apache.Avro, which uses Newtonsoft.Json " +
            "reflection. Remove the setting, or publish untrimmed with the JIT. A trimmed JIT app can set the " +
            "DekafAvroPocoRulesSupport MSBuild property to true to keep rule support, accepting the trim warnings.");
}
