using System.Globalization;
using System.Text.Json;
using AvroTypeException = global::Avro.AvroTypeException;
using SchemaParseException = global::Avro.SchemaParseException;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Avro schema tag. Names match Apache.Avro's <c>Schema.Type</c> so diagnostics read the same.</summary>
internal enum AvroWriterSchemaTag : byte
{
    Null,
    Boolean,
    Int,
    Long,
    Float,
    Double,
    Bytes,
    String,
    Record,
    Enumeration,
    Array,
    Map,
    Union,
    Fixed,
    Logical
}

/// <summary>
/// Parsed Avro schema used by generated POCO schema preparation.
/// </summary>
/// <remarks>
/// Apache.Avro's parser and schema objects reach Newtonsoft.Json reflection paths that fail NativeAOT
/// and trimming analysis, so generated POCO preparation parses writer schemas with System.Text.Json.
/// Parsing and equality follow Apache.Avro 1.12 so schemas accepted, rejected, and matched before
/// keep that behavior.
/// </remarks>
internal sealed class AvroWriterSchema
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 64 };

    private AvroWriterSchema(AvroWriterSchemaTag tag, (string Name, JsonElement Value)[] props)
    {
        Tag = tag;
        Props = props;
    }

    internal AvroWriterSchemaTag Tag { get; }

    /// <summary>Custom (non-reserved) properties in declaration order.</summary>
    internal (string Name, JsonElement Value)[] Props { get; }

    /// <summary>Full name of a record, enum, or fixed schema.</summary>
    internal string? Fullname { get; private set; }

    internal AvroWriterField[] Fields { get; private set; } = [];

    internal string[] Symbols { get; private set; } = [];

    internal int Size { get; private set; }

    /// <summary>Array items, map values, or the underlying schema of a logical type.</summary>
    internal AvroWriterSchema? Child { get; private set; }

    internal AvroWriterSchema[] Branches { get; private set; } = [];

    internal string? LogicalTypeName { get; private set; }

    internal bool IsNamed => Tag is AvroWriterSchemaTag.Record or AvroWriterSchemaTag.Enumeration or AvroWriterSchemaTag.Fixed;

    /// <summary>Parses a schema, resolving named references through <paramref name="names"/> and adding the types it defines.</summary>
    internal static AvroWriterSchema Parse(string json, AvroWriterSchemaNames? names = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        names ??= new AvroWriterSchemaNames();
        var trimmed = json.Trim();
        if (TryGetPrimitive(trimmed, out var primitive))
            return new AvroWriterSchema(primitive, []);
        if (trimmed.Length > 0 && trimmed[0] is not ('{' or '[' or '"') && names.TryGet(trimmed, out var named))
            return named;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(trimmed, DocumentOptions);
        }
        catch (JsonException exception)
        {
            throw new SchemaParseException($"Could not parse Avro schema JSON: {exception.Message}", exception);
        }

        using (document)
            return Parse(document.RootElement, names, null, "");
    }

    internal string? GetPropertyText(string name)
    {
        var props = Props;
        for (var index = 0; index < props.Length; index++)
        {
            if (string.Equals(props[index].Name, name, StringComparison.Ordinal))
                return props[index].Value.GetRawText();
        }
        return null;
    }

    public override string ToString() => Fullname ?? LogicalTypeName ?? Tag.ToString();

    private static AvroWriterSchema Parse(
        JsonElement element,
        AvroWriterSchemaNames names,
        string? enclosingNamespace,
        string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return ParseNameReference(element.GetString()!, names, enclosingNamespace, path);
            case JsonValueKind.Array:
                return ParseUnion(element, names, enclosingNamespace, path);
            case JsonValueKind.Object:
                return ParseObject(element, names, enclosingNamespace, path);
            default:
                throw new SchemaParseException($"Invalid Avro schema JSON token {element.ValueKind} at '{path}'.");
        }
    }

    private static AvroWriterSchema ParseNameReference(
        string name,
        AvroWriterSchemaNames names,
        string? enclosingNamespace,
        string path)
    {
        if (TryGetPrimitive(name, out var primitive))
            return new AvroWriterSchema(primitive, []);
        if (names.TryGet(ResolveFullname(name, null, enclosingNamespace), out var named))
            return named;
        throw new SchemaParseException($"Undefined name: {name} at '{path}'");
    }

    private static AvroWriterSchema ParseUnion(
        JsonElement element,
        AvroWriterSchemaNames names,
        string? enclosingNamespace,
        string path)
    {
        var branches = new AvroWriterSchema[element.GetArrayLength()];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var itemPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var branch = Parse(item, names, enclosingNamespace, itemPath);
            if (branch.Tag == AvroWriterSchemaTag.Union)
                throw new SchemaParseException($"Unions may not immediately contain other unions at '{itemPath}'");
            var key = GetUnionBranchKey(branch);
            if (!seen.Add(key))
                throw new SchemaParseException($"Duplicate type in union: {key} at '{itemPath}'");
            branches[index++] = branch;
        }

        return new AvroWriterSchema(AvroWriterSchemaTag.Union, []) { Branches = branches };
    }

    private static string GetUnionBranchKey(AvroWriterSchema branch)
    {
        while (branch.Tag == AvroWriterSchemaTag.Logical)
            branch = branch.Child!;
        return branch.Fullname ?? GetTypeName(branch.Tag);
    }

    private static AvroWriterSchema ParseObject(
        JsonElement element,
        AvroWriterSchemaNames names,
        string? enclosingNamespace,
        string path)
    {
        if (!element.TryGetProperty("type", out var typeElement))
            throw new SchemaParseException($"Property 'type' is missing from Avro schema at '{path}'");
        var hasLogicalType = element.TryGetProperty("logicalType", out var logicalTypeElement);
        if (typeElement.ValueKind != JsonValueKind.String)
        {
            var nested = Parse(typeElement, names, enclosingNamespace, path);
            return hasLogicalType
                ? CreateLogical(nested, logicalTypeElement, GetProps(element), path)
                : nested;
        }

        var type = typeElement.GetString()!;
        var props = GetProps(element);
        switch (type)
        {
            case "array":
                if (!element.TryGetProperty("items", out var items))
                    throw new AvroTypeException($"Array does not have 'items' at '{path}'");
                return new AvroWriterSchema(AvroWriterSchemaTag.Array, props)
                {
                    Child = Parse(items, names, enclosingNamespace, path + ".items")
                };
            case "map":
                if (!element.TryGetProperty("values", out var values))
                    throw new AvroTypeException($"Map does not have 'values' at '{path}'");
                return new AvroWriterSchema(AvroWriterSchemaTag.Map, props)
                {
                    Child = Parse(values, names, enclosingNamespace, path + ".values")
                };
        }

        if (hasLogicalType)
        {
            var baseSchema = IsNamedTypeKeyword(type)
                ? ParseNamed(element, type, props, names, enclosingNamespace, path)
                : Parse(typeElement, names, enclosingNamespace, path);
            return CreateLogical(baseSchema, logicalTypeElement, props, path);
        }

        if (TryGetPrimitive(type, out var primitive))
            return new AvroWriterSchema(primitive, props);
        if (IsNamedTypeKeyword(type))
            return ParseNamed(element, type, props, names, enclosingNamespace, path);
        return ParseNameReference(type, names, enclosingNamespace, path);
    }

    private static AvroWriterSchema CreateLogical(
        AvroWriterSchema baseSchema,
        JsonElement logicalTypeElement,
        (string Name, JsonElement Value)[] props,
        string path)
    {
        if (logicalTypeElement.ValueKind != JsonValueKind.String)
            throw new SchemaParseException($"Field logicalType is not a string at '{path}'");
        var logical = new AvroWriterSchema(AvroWriterSchemaTag.Logical, props)
        {
            Child = baseSchema,
            LogicalTypeName = logicalTypeElement.GetString()
        };
        ValidateLogicalType(logical, path);
        return logical;
    }

    private static AvroWriterSchema ParseNamed(
        JsonElement element,
        string type,
        (string Name, JsonElement Value)[] props,
        AvroWriterSchemaNames names,
        string? enclosingNamespace,
        string path)
    {
        var name = GetRequiredString(element, "name", path);
        var declaredNamespace = GetOptionalString(element, "namespace", path);
        var fullname = ResolveFullname(name, declaredNamespace, enclosingNamespace);
        ValidateName(fullname, path);
        _ = GetAliases(element, path);
        var tag = type switch
        {
            "record" or "error" => AvroWriterSchemaTag.Record,
            "enum" => AvroWriterSchemaTag.Enumeration,
            _ => AvroWriterSchemaTag.Fixed
        };
        var schema = new AvroWriterSchema(tag, props) { Fullname = fullname };
        if (!names.TryAdd(schema))
            throw new SchemaParseException($"Duplicate schema name {fullname} at '{path}'");

        switch (tag)
        {
            case AvroWriterSchemaTag.Record:
                schema.Fields = ParseFields(element, fullname, names, GetNamespace(fullname), path);
                break;
            case AvroWriterSchemaTag.Enumeration:
                schema.Symbols = ParseSymbols(element, fullname, path);
                break;
            default:
                if (!element.TryGetProperty("size", out var size))
                    throw new SchemaParseException($"No \"size\" JSON field at '{path}'");
                if (size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out var sizeValue))
                    throw new SchemaParseException($"Fixed size must be an integer at '{path}'");
                if (sizeValue <= 0)
                    throw new SchemaParseException($"size must be greater than zero. at '{path}'");
                schema.Size = sizeValue;
                break;
        }

        return schema;
    }

    private static AvroWriterField[] ParseFields(
        JsonElement element,
        string recordName,
        AvroWriterSchemaNames names,
        string? recordNamespace,
        string path)
    {
        if (!element.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind == JsonValueKind.Null)
            throw new SchemaParseException($"'fields' cannot be null for record at '{path}'");
        if (fieldsElement.ValueKind != JsonValueKind.Array)
            throw new SchemaParseException($"'fields' must be an array for record {recordName} at '{path}'");

        var fields = new AvroWriterField[fieldsElement.GetArrayLength()];
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var fieldElement in fieldsElement.EnumerateArray())
        {
            var fieldPath = path + ".fields[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (fieldElement.ValueKind != JsonValueKind.Object)
                throw new SchemaParseException($"Field must be a JSON object at '{fieldPath}'");
            var name = GetRequiredString(fieldElement, "name", fieldPath);
            if (!fieldElement.TryGetProperty("type", out var typeElement))
                throw new SchemaParseException($"'type' was not found for field: {name} at '{fieldPath}'");
            var aliases = GetAliases(fieldElement, fieldPath);
            if (!fieldNames.Add(name))
                throw new SchemaParseException($"field or alias {name} is a duplicate name at '{fieldPath}'");
            for (var aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                if (!fieldNames.Add(aliases[aliasIndex]))
                {
                    throw new SchemaParseException(
                        $"field or alias {aliases[aliasIndex]} is a duplicate name at '{fieldPath}'");
                }
            }

            var order = GetOptionalString(fieldElement, "order", fieldPath);
            if (order is not (null or "ascending" or "descending" or "ignore"))
                throw new SchemaParseException($"Invalid field order '{order}' at '{fieldPath}'");
            fields[index] = new AvroWriterField(
                name,
                Parse(typeElement, names, recordNamespace, fieldPath + ".type"),
                GetOptionalString(fieldElement, "doc", fieldPath),
                fieldElement.TryGetProperty("default", out var defaultValue) ? defaultValue.Clone() : null,
                order,
                GetFieldProps(fieldElement));
            index++;
        }

        return fields;
    }

    private static string[] ParseSymbols(JsonElement element, string enumName, string path)
    {
        if (!element.TryGetProperty("symbols", out var symbolsElement) ||
            symbolsElement.ValueKind != JsonValueKind.Array)
        {
            throw new SchemaParseException($"Enum has no symbols: {enumName} at '{path}'");
        }

        var symbols = new string[symbolsElement.GetArrayLength()];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var symbol in symbolsElement.EnumerateArray())
        {
            if (symbol.ValueKind != JsonValueKind.String)
                throw new SchemaParseException($"Enum symbols must be strings: {enumName} at '{path}'");
            var value = symbol.GetString()!;
            if (!seen.Add(value))
                throw new SchemaParseException($"Duplicate symbol: {value} at '{path}'");
            symbols[index++] = value;
        }

        var defaultSymbol = GetOptionalString(element, "default", path);
        if (defaultSymbol is not null && !seen.Contains(defaultSymbol))
            throw new SchemaParseException($"Default symbol: {defaultSymbol} not found in symbols at '{path}'");
        return symbols;
    }

    private static void ValidateLogicalType(AvroWriterSchema logical, string path)
    {
        var baseTag = logical.Child!.Tag;
        var logicalTypeName = logical.LogicalTypeName!;
        switch (logicalTypeName)
        {
            case "date" or "time-millis":
                RequireBase(baseTag == AvroWriterSchemaTag.Int, logicalTypeName, "int");
                break;
            case "time-micros" or "timestamp-millis" or "timestamp-micros" or "timestamp-nanos" or
                "local-timestamp-millis" or "local-timestamp-micros" or "local-timestamp-nanos":
                RequireBase(baseTag == AvroWriterSchemaTag.Long, logicalTypeName, "long");
                break;
            case "uuid":
                RequireBase(baseTag == AvroWriterSchemaTag.String, logicalTypeName, "string");
                break;
            case "decimal":
                if (baseTag is not (AvroWriterSchemaTag.Bytes or AvroWriterSchemaTag.Fixed))
                {
                    throw new AvroTypeException(
                        $"'decimal' can only be used with an underlying bytes or fixed type at '{path}'");
                }
                var precisionText = logical.GetPropertyText("precision")
                    ?? throw new AvroTypeException($"'decimal' requires a 'precision' property at '{path}'");
                var precision = ParseDecimalProperty(precisionText, "precision", path);
                if (precision <= 0)
                {
                    throw new AvroTypeException(
                        $"'decimal' requires a 'precision' property that is greater than zero at '{path}'");
                }
                var scaleText = logical.GetPropertyText("scale");
                var scale = scaleText is null ? 0 : ParseDecimalProperty(scaleText, "scale", path);
                if (scale < 0 || scale > precision)
                {
                    throw new AvroTypeException(
                        "'decimal' requires a 'scale' property that is zero or less than or equal to " +
                        $"'precision' at '{path}'");
                }
                break;
        }
    }

    private static int ParseDecimalProperty(string text, string name, string path) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AvroTypeException($"'decimal' property '{name}' must be an integer at '{path}'");

    private static void RequireBase(bool valid, string logicalTypeName, string baseType)
    {
        if (!valid)
            throw new AvroTypeException($"'{logicalTypeName}' can only be used with an underlying {baseType} type");
    }

    private static bool TryGetPrimitive(string name, out AvroWriterSchemaTag tag)
    {
        switch (name)
        {
            case "null": tag = AvroWriterSchemaTag.Null; return true;
            case "boolean": tag = AvroWriterSchemaTag.Boolean; return true;
            case "int": tag = AvroWriterSchemaTag.Int; return true;
            case "long": tag = AvroWriterSchemaTag.Long; return true;
            case "float": tag = AvroWriterSchemaTag.Float; return true;
            case "double": tag = AvroWriterSchemaTag.Double; return true;
            case "bytes": tag = AvroWriterSchemaTag.Bytes; return true;
            case "string": tag = AvroWriterSchemaTag.String; return true;
            default: tag = default; return false;
        }
    }

    private static string GetTypeName(AvroWriterSchemaTag tag) => tag switch
    {
        AvroWriterSchemaTag.Null => "null",
        AvroWriterSchemaTag.Boolean => "boolean",
        AvroWriterSchemaTag.Int => "int",
        AvroWriterSchemaTag.Long => "long",
        AvroWriterSchemaTag.Float => "float",
        AvroWriterSchemaTag.Double => "double",
        AvroWriterSchemaTag.Bytes => "bytes",
        AvroWriterSchemaTag.String => "string",
        AvroWriterSchemaTag.Array => "array",
        AvroWriterSchemaTag.Map => "map",
        _ => tag.ToString()
    };

    private static bool IsNamedTypeKeyword(string type) =>
        type is "record" or "error" or "enum" or "fixed";

    private static string ResolveFullname(string name, string? declaredNamespace, string? enclosingNamespace)
    {
        if (name.Contains('.', StringComparison.Ordinal))
            return name;
        // Apache.Avro treats an empty namespace as absent, so it inherits the enclosing namespace.
        var space = string.IsNullOrEmpty(declaredNamespace) ? enclosingNamespace : declaredNamespace;
        return string.IsNullOrEmpty(space) ? name : space + "." + name;
    }

    private static string? GetNamespace(string fullname)
    {
        var index = fullname.LastIndexOf('.');
        return index < 0 ? null : fullname[..index];
    }

    private static void ValidateName(string fullname, string path)
    {
        var name = fullname[(fullname.LastIndexOf('.') + 1)..];
        var valid = name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_');
        for (var index = 1; valid && index < name.Length; index++)
            valid = char.IsAsciiLetterOrDigit(name[index]) || name[index] == '_';
        if (!valid)
            throw new SchemaParseException($"Invalid schema name: \"{name}\" at '{path}'");
    }

    private static string GetRequiredString(JsonElement element, string property, string path)
    {
        if (!element.TryGetProperty(property, out var value))
            throw new SchemaParseException($"No \"{property}\" JSON field at '{path}'");
        if (value.ValueKind != JsonValueKind.String)
            throw new SchemaParseException($"Field {property} is not a string at '{path}'");
        return value.GetString()!;
    }

    private static string? GetOptionalString(JsonElement element, string property, string path)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new SchemaParseException($"Field {property} is not a string at '{path}'");
        return value.GetString();
    }

    private static string[] GetAliases(JsonElement element, string path)
    {
        if (!element.TryGetProperty("aliases", out var aliasesElement) ||
            aliasesElement.ValueKind == JsonValueKind.Null)
        {
            return [];
        }
        if (aliasesElement.ValueKind != JsonValueKind.Array)
            throw new SchemaParseException($"Aliases must be of format JSON array of strings at '{path}'");

        var aliases = new string[aliasesElement.GetArrayLength()];
        var index = 0;
        foreach (var alias in aliasesElement.EnumerateArray())
        {
            if (alias.ValueKind != JsonValueKind.String)
                throw new SchemaParseException($"Aliases must be of format JSON array of strings at '{path}'");
            aliases[index++] = alias.GetString()!;
        }
        return aliases;
    }

    private static (string Name, JsonElement Value)[] GetProps(JsonElement element) =>
        CollectProps(element, IsReservedSchemaProperty);

    private static (string Name, JsonElement Value)[] GetFieldProps(JsonElement element) =>
        CollectProps(element, IsReservedFieldProperty);

    private static (string Name, JsonElement Value)[] CollectProps(JsonElement element, Func<string, bool> isReserved)
    {
        List<(string Name, JsonElement Value)>? props = null;
        foreach (var property in element.EnumerateObject())
        {
            if (!isReserved(property.Name))
                (props ??= []).Add((property.Name, property.Value.Clone()));
        }
        return props?.ToArray() ?? [];
    }

    private static bool IsReservedSchemaProperty(string name) => name is
        "type" or "name" or "namespace" or "fields" or "items" or "size" or "symbols" or "values" or
        "aliases" or "order" or "doc" or "default" or "logicalType";

    private static bool IsReservedFieldProperty(string name) => name is
        "default" or "doc" or "name" or "order" or "type" or "aliases";
}

internal sealed class AvroWriterField(
    string name,
    AvroWriterSchema schema,
    string? doc,
    JsonElement? defaultValue,
    string? order,
    (string Name, JsonElement Value)[] props)
{
    internal string Name { get; } = name;

    internal AvroWriterSchema Schema { get; } = schema;

    internal string? Doc { get; } = doc;

    internal JsonElement? DefaultValue { get; } = defaultValue;

    internal string? Order { get; } = order;

    internal (string Name, JsonElement Value)[] Props { get; } = props;
}

/// <summary>Named types known while parsing, including types defined by schema references.</summary>
internal sealed class AvroWriterSchemaNames
{
    private readonly Dictionary<string, AvroWriterSchema> _names = new(StringComparer.Ordinal);

    internal bool TryGet(string fullname, out AvroWriterSchema schema) =>
        _names.TryGetValue(fullname, out schema!);

    internal bool TryAdd(AvroWriterSchema schema) => _names.TryAdd(schema.Fullname!, schema);
}
