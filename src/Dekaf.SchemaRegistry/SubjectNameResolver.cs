using System.Text.Json;

namespace Dekaf.SchemaRegistry;

internal static class SubjectNameResolver
{
    internal static string GetSubjectName(
        SubjectNameStrategy strategy,
        string topic,
        string? recordName,
        bool isKey,
        bool useLegacySubjectNames)
    {
        var suffix = isKey ? "-key" : "-value";
        return strategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => useLegacySubjectNames
                ? recordName + suffix
                : RequireRecordName(recordName, strategy),
            SubjectNameStrategy.TopicRecordName => useLegacySubjectNames
                ? $"{topic}-{recordName}{suffix}"
                : $"{topic}-{RequireRecordName(recordName, strategy)}",
            _ => topic + suffix
        };
    }

    internal static string GetRecordName(Schema schema, string fallback)
    {
        if (schema.SchemaType is not (SchemaType.Avro or SchemaType.Json))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(schema.SchemaString);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return fallback;

            if (schema.SchemaType == SchemaType.Json &&
                root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(title.GetString()))
            {
                return title.GetString()!;
            }

            if (schema.SchemaType == SchemaType.Avro &&
                root.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(name.GetString()))
            {
                var recordName = name.GetString()!;
                if (recordName.Contains('.'))
                    return recordName;

                if (root.TryGetProperty("namespace", out var @namespace) &&
                    @namespace.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(@namespace.GetString()))
                {
                    return $"{@namespace.GetString()}.{recordName}";
                }

                return recordName;
            }
        }
        catch (JsonException)
        {
            // Schema Registry will report malformed schemas during lookup or registration.
        }

        return fallback;
    }

    private static string RequireRecordName(string? recordName, SubjectNameStrategy strategy)
    {
        if (!string.IsNullOrEmpty(recordName))
            return recordName;

        throw new InvalidOperationException($"{strategy} requires a fully-qualified record name.");
    }
}
