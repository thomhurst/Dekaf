using Dekaf.SchemaRegistry.Avro.Poco;
using ApacheSchema = Avro.Schema;
using ApacheSchemaNames = Avro.SchemaNames;

namespace Dekaf.Tests.Unit.SchemaRegistry;

/// <summary>
/// Differential tests that pin <see cref="AvroWriterSchema"/> to Apache.Avro 1.12 behavior, which
/// generated POCO preparation used before it stopped depending on Apache.Avro's parser.
/// </summary>
public sealed class AvroWriterSchemaTests
{
    private static string Record(string fields, string extra = "") =>
        "{\"type\":\"record\",\"name\":\"R\",\"namespace\":\"n\"" + extra + ",\"fields\":[" + fields + "]}";

    private static string IntField(string extra) => Record("{\"name\":\"a\",\"type\":\"int\"" + extra + "}");

    private static string DoubleDefault(string value) =>
        Record("{\"name\":\"a\",\"type\":\"double\",\"default\":" + value + "}");

    private static string MapDefault(string value) =>
        Record("{\"name\":\"a\",\"type\":{\"type\":\"map\",\"values\":\"int\"},\"default\":" + value + "}");

    private const string Recursive =
        """{"type":"record","name":"L","fields":[{"name":"n","type":["null","L"]}]}""";

    private static readonly (string Label, string Left, string Right)[] EqualityCases =
    [
        ("record doc", Record("", ",\"doc\":\"x\""), Record("")),
        ("record aliases", Record("", ",\"aliases\":[\"Q\"]"), Record("")),
        ("record prop", Record("", ",\"p\":1"), Record("")),
        ("record name", Record(""), Record("").Replace("\"R\"", "\"S\"", StringComparison.Ordinal)),
        ("error vs record", """{"type":"error","name":"R","fields":[]}""", """{"type":"record","name":"R","fields":[]}"""),
        ("namespace vs dotted", """{"type":"enum","name":"E","namespace":"a","symbols":["A"]}""", """{"type":"enum","name":"a.E","symbols":["A"]}"""),
        ("field doc", IntField(",\"doc\":\"x\""), IntField("")),
        ("field doc value", IntField(",\"doc\":\"x\""), IntField(",\"doc\":\"y\"")),
        ("field aliases", IntField(",\"aliases\":[\"b\"]"), IntField("")),
        ("field order ascending", IntField(",\"order\":\"ascending\""), IntField("")),
        ("field order descending", IntField(",\"order\":\"descending\""), IntField(",\"order\":\"ascending\"")),
        ("field default", IntField(",\"default\":1"), IntField("")),
        ("field default int vs float", IntField(",\"default\":1"), IntField(",\"default\":1.0")),
        ("field default float digits", DoubleDefault("1.0"), DoubleDefault("1.00")),
        ("field default exponent", DoubleDefault("1e2"), DoubleDefault("100.0")),
        ("field default object order", MapDefault("{\"a\":1,\"b\":2}"), MapDefault("{\"b\":2,\"a\":1}")),
        ("field default null vs missing",
            Record("{\"name\":\"a\",\"type\":[\"null\",\"int\"],\"default\":null}"),
            Record("{\"name\":\"a\",\"type\":[\"null\",\"int\"]}")),
        ("field prop", IntField(",\"p\":\"x\""), IntField("")),
        ("field prop int vs float", IntField(",\"p\":1"), IntField(",\"p\":1.0")),
        ("field prop string vs int", IntField(",\"p\":\"1\""), IntField(",\"p\":1")),
        ("field prop object order", IntField(",\"p\":{\"a\":1,\"b\":2}"), IntField(",\"p\":{\"b\":2,\"a\":1}")),
        ("field prop whitespace", IntField(",\"p\":[1, 2]"), IntField(",\"p\":[1,2]")),
        ("field prop escape", IntField(",\"p\":\"\\u00e9\""), IntField(",\"p\":\"\u00e9\"")),
        ("field prop order", IntField(",\"p\":1,\"q\":2"), IntField(",\"q\":2,\"p\":1")),
        ("field order in record",
            Record("{\"name\":\"a\",\"type\":\"int\"},{\"name\":\"b\",\"type\":\"int\"}"),
            Record("{\"name\":\"b\",\"type\":\"int\"},{\"name\":\"a\",\"type\":\"int\"}")),
        ("enum default", """{"type":"enum","name":"E","symbols":["A","B"],"default":"A"}""", """{"type":"enum","name":"E","symbols":["A","B"]}"""),
        ("enum doc", """{"type":"enum","name":"E","symbols":["A"],"doc":"d"}""", """{"type":"enum","name":"E","symbols":["A"]}"""),
        ("enum aliases", """{"type":"enum","name":"E","symbols":["A"],"aliases":["X"]}""", """{"type":"enum","name":"E","symbols":["A"]}"""),
        ("enum symbol order", """{"type":"enum","name":"E","symbols":["A","B"]}""", """{"type":"enum","name":"E","symbols":["B","A"]}"""),
        ("fixed aliases", """{"type":"fixed","name":"F","size":2,"aliases":["X"]}""", """{"type":"fixed","name":"F","size":2}"""),
        ("fixed size", """{"type":"fixed","name":"F","size":2}""", """{"type":"fixed","name":"F","size":3}"""),
        ("primitive object form", "\"int\"", """{"type":"int"}"""),
        ("primitive prop", """{"type":"int","p":1}""", "\"int\""),
        ("primitive doc", """{"type":"int","doc":"x"}""", "\"int\""),
        ("primitive aliases", """{"type":"int","aliases":["x"]}""", "\"int\""),
        ("primitive default", """{"type":"int","default":1}""", "\"int\""),
        ("primitive tag", "\"int\"", "\"long\""),
        ("logical vs base", """{"type":"int","logicalType":"date"}""", "\"int\""),
        ("logical prop", """{"type":"int","logicalType":"date","p":1}""", """{"type":"int","logicalType":"date"}"""),
        ("logical name ignored", """{"type":"string","logicalType":"foo"}""", """{"type":"string","logicalType":"bar"}"""),
        ("unknown logical vs base", """{"type":"string","logicalType":"foo"}""", "\"string\""),
        ("decimal explicit scale", """{"type":"bytes","logicalType":"decimal","precision":4,"scale":0}""", """{"type":"bytes","logicalType":"decimal","precision":4}"""),
        ("decimal precision",
            """{"type":"bytes","logicalType":"decimal","precision":4,"scale":2}""",
            """{"type":"bytes","logicalType":"decimal","precision":5,"scale":2}"""),
        ("decimal fixed nested",
            """{"type":{"type":"fixed","name":"F","size":4},"logicalType":"decimal","precision":9,"scale":2}""",
            """{"type":"fixed","name":"F","size":4,"logicalType":"decimal","precision":9,"scale":2}"""),
        ("array logical ignored", """{"type":"array","items":"int","logicalType":"x"}""", """{"type":"array","items":"int"}"""),
        ("array prop", """{"type":"array","items":"int","p":1}""", """{"type":"array","items":"int"}"""),
        ("array items", """{"type":"array","items":"int"}""", """{"type":"array","items":"long"}"""),
        ("map values", """{"type":"map","values":"int"}""", """{"type":"map","values":"long"}"""),
        ("union order", """["int","string"]""", """["string","int"]"""),
        ("union count", """["int","string"]""", """["int"]"""),
        ("recursive", Recursive, Recursive),
        ("recursive differs",
            Recursive,
            """{"type":"record","name":"L","fields":[{"name":"n","type":["null","L"],"doc":"x"}]}"""),
    ];

    private static readonly string[] ParseCases =
    [
        """{"type":"string","logicalType":"foo"}""",
        """{"type":"string","logicalType":"date"}""",
        """{"type":"bytes","logicalType":"decimal"}""",
        """{"type":"bytes","logicalType":"decimal","precision":2,"scale":3}""",
        """{"type":"bytes","logicalType":"decimal","precision":0}""",
        """{"type":"bytes","logicalType":"decimal","precision":3,"scale":-1}""",
        """{"type":"bytes","logicalType":"decimal","precision":"2"}""",
        """{"type":"bytes","logicalType":"decimal","precision":3.0}""",
        """{"type":"int","logicalType":"decimal","precision":2}""",
        """{"type":"fixed","name":"F","size":1,"logicalType":"decimal","precision":10}""",
        """{"type":"fixed","name":"F","size":16,"logicalType":"uuid"}""",
        """{"type":"fixed","name":"F","size":4,"logicalType":"date"}""",
        """{"type":"int","logicalType":"timestamp-millis"}""",
        """{"type":"int","logicalType":"time-millis"}""",
        """{"type":"long","logicalType":"local-timestamp-micros"}""",
        """{"type":"long","logicalType":"timestamp-nanos"}""",
        """{"type":"fixed","name":"D","size":12,"logicalType":"duration"}""",
        """{"type":"int","logicalType":5}""",
        """{"type":"record","name":"R","logicalType":"foo","fields":[]}""",
        Record("{\"name\":\"a\",\"type\":{\"type\":\"record\",\"name\":\"In\",\"namespace\":\"\",\"fields\":[]}}"),
        Record("{\"name\":\"a\",\"type\":{\"type\":\"record\",\"name\":\"x.y.In\",\"fields\":[]}},{\"name\":\"b\",\"type\":\"x.y.In\"}"),
        Record("{\"name\":\"a\",\"type\":{\"type\":\"record\",\"name\":\"In\",\"fields\":[]}},{\"name\":\"b\",\"type\":\"In\"}"),
        Record("{\"name\":\"a\",\"type\":{\"type\":\"record\",\"name\":\"In\",\"fields\":[]}},{\"name\":\"b\",\"type\":\"n.In\"}"),
        """{"type":"record","name":"R","fields":[{"name":"a","type":{"type":"enum","name":"E","symbols":["A"]}},{"name":"b","type":"E"}]}""",
        """{"type":"record","name":"a.R","fields":[{"name":"x","type":{"type":"enum","name":"E","namespace":"","symbols":["A"]}},{"name":"y","type":"E"}]}""",
        Record("{\"name\":\"a\",\"type\":\"Nope\"}"),
        Record("{\"name\":\"a\",\"type\":{\"type\":\"enum\",\"name\":\"R\",\"symbols\":[\"A\"]}}"),
        Record("{\"name\":\"a\",\"type\":\"int\"},{\"name\":\"a\",\"type\":\"int\"}"),
        """{"type":"record","name":"R","fields":[{"name":"x","type":"int","aliases":["y"]},{"name":"y","type":"int"}]}""",
        Record("{\"name\":\"a\",\"type\":[\"int\",\"int\"]}"),
        Record("{\"name\":\"a\",\"type\":[\"int\",[\"string\"]]}"),
        """["int",{"type":"int","logicalType":"date"}]""",
        """[{"type":"record","name":"A","fields":[]},{"type":"record","name":"B","fields":[]}]""",
        """[{"type":"array","items":"int"},{"type":"array","items":"string"}]""",
        """{"type":"enum","name":"E","symbols":["1A"]}""",
        """{"type":"enum","name":"E","symbols":["A","A"]}""",
        """{"type":"enum","name":"E","symbols":["A"],"default":"B"}""",
        """{"type":"enum","name":"E"}""",
        """{"type":"enum","name":5,"symbols":["A"]}""",
        """{"type":"enum","name":"E","aliases":"x","symbols":["A"]}""",
        """{"type":"record","name":"1R","fields":[]}""",
        """{"type":"record","name":"R"}""",
        """{"type":"record","name":"R","fields":[{"name":"x"}]}""",
        """{"type":"record","name":"R","fields":[{"type":"int"}]}""",
        """{"type":"record","name":"int","fields":[]}""",
        """{"type":"record","name":"R","fields":[{"name":"a","type":{"type":"R"}}]}""",
        """{"type":"error","name":"Er","fields":[]}""",
        Record("{\"name\":\"a\",\"type\":\"int\",\"order\":\"sideways\"}"),
        Record("{\"name\":\"a\",\"type\":\"int\",\"default\":\"x\"}"),
        """{"type":"string","avro.java.string":"String"}""",
        """{"type":{"type":{"type":"int"}}}""",
        """{"type":["int","null"]}""",
        """{"type":"fixed","name":"F","size":-1}""",
        """{"type":"fixed","name":"F"}""",
        """{"type":"array"}""",
        """{"type":"foo"}""",
        """{"type":""",
        "[]",
        "\"int\"",
        Recursive,
    ];

    [Test]
    public async Task SchemaEquals_MatchesApacheAvro()
    {
        var mismatches = new List<string>();
        foreach (var (label, left, right) in EqualityCases)
        {
            var expected = ApacheSchema.Parse(left).Equals(ApacheSchema.Parse(right));
            var leftModel = AvroWriterSchema.Parse(left);
            var rightModel = AvroWriterSchema.Parse(right);
            var forward = AvroWriterSchemaComparer.SchemaEquals(leftModel, rightModel);
            var backward = AvroWriterSchemaComparer.SchemaEquals(rightModel, leftModel);
            if (forward != expected || backward != expected)
                mismatches.Add($"{label}: Apache.Avro {expected}, model {forward}/{backward}");
            if (!AvroWriterSchemaComparer.SchemaEquals(leftModel, AvroWriterSchema.Parse(left)))
                mismatches.Add($"{label}: model is not reflexive");
        }

        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    public async Task Parse_AcceptsAndRejectsTheSameSchemasAsApacheAvro()
    {
        var mismatches = new List<string>();
        foreach (var json in ParseCases)
        {
            ApacheSchema? expected = null;
            try
            {
                expected = ApacheSchema.Parse(json);
            }
            catch (Exception)
            {
            }

            AvroWriterSchema? actual = null;
            Exception? failure = null;
            try
            {
                actual = AvroWriterSchema.Parse(json);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if ((expected is null) != (actual is null))
            {
                mismatches.Add(
                    $"{json}: Apache.Avro {(expected is null ? "rejects" : "accepts")}, model " +
                    $"{(actual is null ? "rejects: " + failure!.Message : "accepts")}");
                continue;
            }
            if (actual is null)
            {
                if (failure is not (Avro.SchemaParseException or Avro.AvroTypeException))
                    mismatches.Add($"{json}: model threw {failure!.GetType().Name}");
                continue;
            }

            if (!SameShape(expected!, actual))
                mismatches.Add($"{json}: parsed shape differs from Apache.Avro ({expected})");
        }

        await Assert.That(mismatches).IsEmpty();
    }

    [Test]
    public async Task Parse_ResolvesReferencedNames()
    {
        const string referenced = """{"type":"record","name":"Address","namespace":"shop","fields":[{"name":"city","type":"string"}]}""";
        const string schema = """{"type":"record","name":"Order","namespace":"shop","fields":[{"name":"ship","type":"Address"},{"name":"bill","type":"shop.Address"}]}""";

        var names = new AvroWriterSchemaNames();
        _ = AvroWriterSchema.Parse(referenced, names);
        var parsed = AvroWriterSchema.Parse(schema, names);

        await Assert.That(parsed.Fields[0].Schema.Fullname).IsEqualTo("shop.Address");
        await Assert.That(ReferenceEquals(parsed.Fields[0].Schema, parsed.Fields[1].Schema)).IsTrue();

        var apacheNames = new ApacheSchemaNames();
        _ = ApacheSchema.Parse(referenced, apacheNames);
        var apache = ApacheSchema.Parse(schema, apacheNames);
        var fromApacheJson = AvroWriterSchema.Parse(apache.ToString());
        await Assert.That(AvroWriterSchemaComparer.SchemaEquals(parsed, fromApacheJson)).IsTrue();
    }

    [Test]
    public async Task Parse_RejectsUndefinedReferenceWithoutNames()
    {
        const string schema = """{"type":"record","name":"Order","namespace":"shop","fields":[{"name":"ship","type":"Address"}]}""";

        await Assert.That(() => AvroWriterSchema.Parse(schema))
            .Throws<Avro.SchemaParseException>()
            .WithMessageContaining("Undefined name: Address");
    }

    private static bool SameShape(ApacheSchema expected, AvroWriterSchema actual) =>
        SameShape(expected, actual, new HashSet<AvroWriterSchema>(ReferenceEqualityComparer.Instance));

    private static bool SameShape(ApacheSchema expected, AvroWriterSchema actual, HashSet<AvroWriterSchema> visited)
    {
        var expectedTag = expected.Tag == ApacheSchema.Type.Error ? "Record" : expected.Tag.ToString();
        if (!string.Equals(expectedTag, actual.Tag.ToString(), StringComparison.Ordinal))
            return false;
        switch (expected)
        {
            case Avro.RecordSchema record:
                if (!string.Equals(record.Fullname, actual.Fullname, StringComparison.Ordinal) ||
                    record.Count != actual.Fields.Length)
                {
                    return false;
                }
                if (!visited.Add(actual))
                    return true;
                for (var index = 0; index < record.Count; index++)
                {
                    if (!string.Equals(record.Fields[index].Name, actual.Fields[index].Name, StringComparison.Ordinal) ||
                        !SameShape(record.Fields[index].Schema, actual.Fields[index].Schema, visited))
                    {
                        return false;
                    }
                }
                return true;
            case Avro.EnumSchema @enum:
                return string.Equals(@enum.Fullname, actual.Fullname, StringComparison.Ordinal) &&
                       @enum.Symbols.SequenceEqual(actual.Symbols);
            case Avro.FixedSchema @fixed:
                return string.Equals(@fixed.Fullname, actual.Fullname, StringComparison.Ordinal) &&
                       @fixed.Size == actual.Size;
            case Avro.ArraySchema array:
                return SameShape(array.ItemSchema, actual.Child!, visited);
            case Avro.MapSchema map:
                return SameShape(map.ValueSchema, actual.Child!, visited);
            case Avro.UnionSchema union:
                if (union.Count != actual.Branches.Length)
                    return false;
                for (var index = 0; index < union.Count; index++)
                {
                    if (!SameShape(union[index], actual.Branches[index], visited))
                        return false;
                }
                return true;
            case Avro.LogicalSchema logical:
                return string.Equals(logical.LogicalTypeName, actual.LogicalTypeName, StringComparison.Ordinal) &&
                       SameShape(logical.BaseSchema, actual.Child!, visited);
            default:
                return true;
        }
    }
}
