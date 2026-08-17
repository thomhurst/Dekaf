using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

internal static class SchemaRegistryRuleContextSnapshot
{
    public static SchemaRegistryRuleContext Capture(SchemaRegistryRuleContext context) =>
        new()
        {
            Topic = context.Topic,
            Component = context.Component,
            SchemaId = context.SchemaId,
            Subject = context.Subject,
            Schema = context.Schema,
            PayloadFormat = context.PayloadFormat
        };
}
