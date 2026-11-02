using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>
/// Structural schema equality matching Apache.Avro 1.12's <c>Schema.Equals</c>.
/// </summary>
/// <remarks>
/// Record, enum, and fixed schemas compare full names; record and named-type docs and aliases are
/// ignored. Fields compare name, doc, order, default, type, and custom properties. Logical schemas
/// compare their underlying schema and properties, not the logical type name. Custom properties
/// compare as JSON text, so object member order matters; defaults compare as JSON values with
/// unordered object members. Integer and floating-point JSON numbers are never equal to each other.
/// </remarks>
internal static class AvroWriterSchemaComparer
{
    internal static bool SchemaEquals(AvroWriterSchema left, AvroWriterSchema right) =>
        Equals(left, right, new HashSet<(AvroWriterSchema, AvroWriterSchema)>());

    private static bool Equals(
        AvroWriterSchema left,
        AvroWriterSchema right,
        HashSet<(AvroWriterSchema, AvroWriterSchema)> inProgress)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Tag != right.Tag)
            return false;
        if (left.IsNamed && !string.Equals(left.Fullname, right.Fullname, StringComparison.Ordinal))
            return false;

        switch (left.Tag)
        {
            case AvroWriterSchemaTag.Record:
                if (left.Fields.Length != right.Fields.Length)
                    return false;
                // Recursive records compare equal on re-entry, as Apache.Avro does.
                if (!inProgress.Add((left, right)))
                    return true;
                try
                {
                    for (var index = 0; index < left.Fields.Length; index++)
                    {
                        if (!FieldEquals(left.Fields[index], right.Fields[index], inProgress))
                            return false;
                    }
                }
                finally
                {
                    inProgress.Remove((left, right));
                }
                break;
            case AvroWriterSchemaTag.Enumeration:
                if (!left.Symbols.AsSpan().SequenceEqual(right.Symbols))
                    return false;
                break;
            case AvroWriterSchemaTag.Fixed:
                if (left.Size != right.Size)
                    return false;
                break;
            case AvroWriterSchemaTag.Array or AvroWriterSchemaTag.Map or AvroWriterSchemaTag.Logical:
                if (!Equals(left.Child!, right.Child!, inProgress))
                    return false;
                break;
            case AvroWriterSchemaTag.Union:
                if (left.Branches.Length != right.Branches.Length)
                    return false;
                for (var index = 0; index < left.Branches.Length; index++)
                {
                    if (!Equals(left.Branches[index], right.Branches[index], inProgress))
                        return false;
                }
                break;
        }

        return PropsEqual(left.Props, right.Props);
    }

    private static bool FieldEquals(
        AvroWriterField left,
        AvroWriterField right,
        HashSet<(AvroWriterSchema, AvroWriterSchema)> inProgress) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        string.Equals(left.Doc, right.Doc, StringComparison.Ordinal) &&
        string.Equals(left.Order, right.Order, StringComparison.Ordinal) &&
        DefaultEquals(left.DefaultValue, right.DefaultValue) &&
        Equals(left.Schema, right.Schema, inProgress) &&
        PropsEqual(left.Props, right.Props);

    private static bool DefaultEquals(JsonElement? left, JsonElement? right) =>
        left is { } l
            ? right is { } r && JsonEquals(l, r, orderedObjects: false)
            : right is null;

    private static bool PropsEqual(
        (string Name, JsonElement Value)[] left,
        (string Name, JsonElement Value)[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (var index = 0; index < left.Length; index++)
        {
            var found = false;
            for (var candidate = 0; candidate < right.Length; candidate++)
            {
                if (string.Equals(left[index].Name, right[candidate].Name, StringComparison.Ordinal))
                {
                    if (!JsonEquals(left[index].Value, right[candidate].Value, orderedObjects: true))
                        return false;
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    private static bool JsonEquals(JsonElement left, JsonElement right, bool orderedObjects)
    {
        var leftKind = Normalize(left.ValueKind);
        if (leftKind != Normalize(right.ValueKind))
            return false;

        switch (leftKind)
        {
            case JsonValueKind.Null or JsonValueKind.True:
                return left.ValueKind == right.ValueKind;
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return NumberEquals(left.GetRawText(), right.GetRawText());
            case JsonValueKind.Array:
                if (left.GetArrayLength() != right.GetArrayLength())
                    return false;
                var leftItems = left.EnumerateArray();
                var rightItems = right.EnumerateArray();
                while (leftItems.MoveNext() && rightItems.MoveNext())
                {
                    if (!JsonEquals(leftItems.Current, rightItems.Current, orderedObjects))
                        return false;
                }
                return true;
            default:
                return orderedObjects ? OrderedObjectEquals(left, right) : UnorderedObjectEquals(left, right);
        }
    }

    // True and False share one kind so a boolean mismatch compares values, not kinds.
    private static JsonValueKind Normalize(JsonValueKind kind) =>
        kind == JsonValueKind.False ? JsonValueKind.True : kind;

    private static bool OrderedObjectEquals(JsonElement left, JsonElement right)
    {
        var l = left.EnumerateObject();
        var r = right.EnumerateObject();
        while (true)
        {
            var hasLeft = l.MoveNext();
            if (hasLeft != r.MoveNext())
                return false;
            if (!hasLeft)
                return true;
            if (!string.Equals(l.Current.Name, r.Current.Name, StringComparison.Ordinal) ||
                !JsonEquals(l.Current.Value, r.Current.Value, orderedObjects: true))
            {
                return false;
            }
        }
    }

    private static bool UnorderedObjectEquals(JsonElement left, JsonElement right)
    {
        var count = 0;
        foreach (var property in left.EnumerateObject())
        {
            count++;
            if (!right.TryGetProperty(property.Name, out var other) ||
                !JsonEquals(property.Value, other, orderedObjects: false))
            {
                return false;
            }
        }

        foreach (var _ in right.EnumerateObject())
            count--;
        return count == 0;
    }

    private static bool NumberEquals(string left, string right)
    {
        var leftIsFloat = IsFloat(left);
        if (leftIsFloat != IsFloat(right))
            return false;
        if (leftIsFloat)
        {
            return double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture) ==
                   double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        return BigInteger.Parse(left, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) ==
               BigInteger.Parse(right, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    private static bool IsFloat(string number) => number.AsSpan().IndexOfAny('.', 'e', 'E') >= 0;

}
