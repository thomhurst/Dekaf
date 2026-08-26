namespace Dekaf.SchemaRegistry.Protobuf;

internal static class ProtobufValidationConfiguration
{
    internal static ProtobufInlineRuleExecutor? Create(
        ValidationRulesExecution execution,
        ISchemaRegistryRuleExecutor? ruleExecutor,
        ISchemaRegistryClient schemaRegistry,
        Google.Protobuf.Reflection.MessageDescriptor descriptor)
    {
        if (!Enum.IsDefined(execution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(execution),
                execution,
                "Unsupported inline validation execution mode.");
        }
        if (execution == ValidationRulesExecution.Disabled)
            return null;
        if (ruleExecutor is not null and not SchemaRegistryRuleExecutor &&
            !ReferenceEquals(ruleExecutor, SchemaRegistryMigrationRunner.MarkerRuleExecutor))
        {
            throw new NotSupportedException(
                "Inline validation rules require SchemaRegistryRuleExecutor so domain and encoding rule boundaries are known.");
        }

        return new ProtobufInlineRuleExecutor(schemaRegistry, descriptor);
    }
}
