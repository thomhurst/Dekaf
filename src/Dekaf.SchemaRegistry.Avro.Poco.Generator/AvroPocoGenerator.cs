using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dekaf.SchemaRegistry.Avro.Poco.Generator;

[Generator(LanguageNames.CSharp)]
internal sealed class AvroPocoGenerator : IIncrementalGenerator
{
    private const string RecordAttribute = "Dekaf.SchemaRegistry.Avro.Poco.AvroRecordAttribute";
    private const string FieldAttribute = "Dekaf.SchemaRegistry.Avro.Poco.AvroFieldAttribute";
    private const string IgnoreAttribute = "Dekaf.SchemaRegistry.Avro.Poco.AvroIgnoreAttribute";

    private static readonly DiagnosticDescriptor PartialRequired = new(
        "DKAVRO001",
        "Avro POCO must be partial",
        "Type '{0}' must be partial to receive its generated Avro codec",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedType = new(
        "DKAVRO002",
        "Unsupported Avro POCO member",
        "Member '{0}' has unsupported Avro type '{1}': {2}",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOrder = new(
        "DKAVRO003",
        "Duplicate Avro field order",
        "Type '{0}' uses Avro field order {1} more than once",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidDefault = new(
        "DKAVRO004",
        "Invalid Avro field default",
        "Member '{0}' has invalid DefaultJson: {1}",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RecursiveShape = new(
        "DKAVRO005",
        "Recursive Avro POCO shape",
        "Type '{0}' contains a recursive by-value record path; recursive generated POCO records are not supported",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "DKAVRO006",
        "Avro POCO member cannot be generated",
        "Member '{0}' must have a readable value and an assignable field, setter, or init accessor",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedDeclaration = new(
        "DKAVRO007",
        "Unsupported Avro POCO declaration",
        "Type '{0}' must be a non-generic, top-level class, record, or struct",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateFieldName = new(
        "DKAVRO008",
        "Duplicate Avro field name",
        "Type '{0}' maps more than one member or alias to Avro field name '{1}'",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AmbiguousUnion = new(
        "DKAVRO009",
        "Ambiguous Avro union",
        "Member '{0}' has ambiguous Avro union branches: {1}",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidAvroName = new(
        "DKAVRO010",
        "Invalid Avro name",
        "'{0}' is not a valid Avro {1}",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedConstruction = new(
        "DKAVRO011",
        "Avro POCO cannot be constructed",
        "Type '{0}' must be concrete and provide a parameterless constructor for generated deserialization",
        "Dekaf.Avro",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var records = context.SyntaxProvider.ForAttributeWithMetadataName(
            RecordAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol);

        context.RegisterSourceOutput(records, static (productionContext, symbol) =>
            Generate(productionContext, symbol));
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol symbol)
    {
        if (symbol.IsGenericType || symbol.ContainingType is not null ||
            symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedDeclaration,
                symbol.Locations.FirstOrDefault(),
                symbol.ToDisplayString()));
            return;
        }

        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Class &&
            !symbol.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnsupportedConstruction,
                symbol.Locations.FirstOrDefault(),
                symbol.ToDisplayString()));
            return;
        }

        if (!symbol.DeclaringSyntaxReferences.Any(static syntax =>
                syntax.GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PartialRequired,
                symbol.Locations.FirstOrDefault(),
                symbol.ToDisplayString()));
            return;
        }

        var builder = new ModelBuilder(context);
        var model = builder.BuildRecord(symbol);
        if (model is null || builder.HasErrors)
            return;

        var emitter = new CodecEmitter(model);
        var source = emitter.Emit();
        var hintName = Sanitize(symbol.ToDisplayString()) + ".AvroPoco.g.cs";
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        return builder.ToString();
    }

    private sealed class ModelBuilder
    {
        private readonly SourceProductionContext _context;
        private readonly Dictionary<INamedTypeSymbol, RecordModel> _records =
            new(SymbolEqualityComparer.Default);
        private readonly HashSet<INamedTypeSymbol> _building = new(SymbolEqualityComparer.Default);

        internal ModelBuilder(SourceProductionContext context) => _context = context;

        internal bool HasErrors { get; private set; }

        internal RecordModel? BuildRecord(INamedTypeSymbol symbol)
        {
            if (_records.TryGetValue(symbol, out var existing))
                return existing;
            if (!_building.Add(symbol))
            {
                Error(RecursiveShape, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString());
                return null;
            }

            var recordAttribute = GetAttribute(symbol, RecordAttribute)!;
            var avroName = GetNamedString(recordAttribute, "Name") ?? symbol.Name;
            var avroNamespace = GetNamedString(recordAttribute, "Namespace") ??
                (symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString());
            if (!IsAvroName(avroName) || !IsAvroNamespace(avroNamespace))
            {
                Error(
                    InvalidAvroName,
                    symbol.Locations.FirstOrDefault(),
                    !IsAvroName(avroName) ? avroName : avroNamespace,
                    !IsAvroName(avroName) ? "record name" : "namespace");
                _building.Remove(symbol);
                return null;
            }
            var fullName = string.IsNullOrEmpty(avroNamespace)
                ? avroName
                : avroNamespace + "." + avroName;

            var members = new List<MemberModel>();
            foreach (var member in symbol.GetMembers())
            {
                if (member.IsStatic || GetAttribute(member, IgnoreAttribute) is not null)
                    continue;

                ITypeSymbol? memberType = null;
                var sourceOrder = int.MaxValue;
                switch (member)
                {
                    case IPropertySymbol property when !property.IsIndexer && !property.IsImplicitlyDeclared:
                        if (property.GetMethod is null || property.SetMethod is null)
                        {
                            Error(InvalidMember, property.Locations.FirstOrDefault(), property.Name);
                            continue;
                        }
                        memberType = property.Type;
                        sourceOrder = GetSourceOrder(property);
                        break;
                    case IFieldSymbol field when !field.IsImplicitlyDeclared && !field.IsConst:
                        if (field.IsReadOnly)
                        {
                            Error(InvalidMember, field.Locations.FirstOrDefault(), field.Name);
                            continue;
                        }
                        memberType = field.Type;
                        sourceOrder = GetSourceOrder(field);
                        break;
                }

                if (memberType is null)
                    continue;

                var attribute = GetAttribute(member, FieldAttribute);
                var unionTypes = GetNamedTypes(attribute, "UnionTypes");
                var precision = GetNamedInt(attribute, "Precision");
                var scale = GetNamedInt(attribute, "Scale");
                var logicalType = GetNamedString(attribute, "LogicalType");
                var type = BuildType(memberType, unionTypes, logicalType, precision, scale, member);
                if (type is null)
                    continue;

                var defaultJson = GetNamedString(attribute, "DefaultJson");
                if (defaultJson is not null)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(defaultJson);
                        if (!TryValidateDefault(type, document.RootElement, out var defaultError))
                        {
                            Error(InvalidDefault, member.Locations.FirstOrDefault(), member.Name, defaultError);
                            continue;
                        }
                    }
                    catch (JsonException exception)
                    {
                        Error(InvalidDefault, member.Locations.FirstOrDefault(), member.Name, exception.Message);
                        continue;
                    }
                }

                var avroFieldName = GetNamedString(attribute, "Name") ?? member.Name;
                var aliases = GetNamedStrings(attribute, "Aliases");
                if (!IsAvroName(avroFieldName))
                {
                    Error(InvalidAvroName, member.Locations.FirstOrDefault(), avroFieldName, "field name");
                    continue;
                }
                var hasInvalidAlias = false;
                foreach (var alias in aliases)
                {
                    if (IsAvroName(alias))
                        continue;
                    Error(InvalidAvroName, member.Locations.FirstOrDefault(), alias ?? "<null>", "field alias");
                    hasInvalidAlias = true;
                }
                if (hasInvalidAlias)
                    continue;

                members.Add(new MemberModel(
                    member.Name,
                    avroFieldName,
                    aliases,
                    defaultJson,
                    GetNamedInt(attribute, "Order", -1),
                    sourceOrder,
                    GetClrType(memberType),
                    type));
            }

            if (members.Count == 0)
            {
                Error(UnsupportedType, symbol.Locations.FirstOrDefault(), symbol.Name, symbol.Name,
                    "record has no assignable instance members");
                _building.Remove(symbol);
                return null;
            }

            var explicitOrders = new HashSet<int>();
            var avroNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                if (member.Order >= 0 && !explicitOrders.Add(member.Order))
                    Error(DuplicateOrder, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString(), member.Order);
                if (!avroNames.Add(member.AvroName))
                    Error(DuplicateFieldName, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString(), member.AvroName);
                foreach (var alias in member.Aliases)
                {
                    if (!avroNames.Add(alias))
                        Error(DuplicateFieldName, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString(), alias);
                }
            }

            members.Sort(static (left, right) =>
            {
                var leftOrder = left.Order >= 0 ? left.Order : int.MaxValue;
                var rightOrder = right.Order >= 0 ? right.Order : int.MaxValue;
                var order = leftOrder.CompareTo(rightOrder);
                if (order != 0)
                    return order;
                var sourceOrder = left.SourceOrder.CompareTo(right.SourceOrder);
                return sourceOrder != 0
                    ? sourceOrder
                    : string.CompareOrdinal(left.ClrName, right.ClrName);
            });

            var model = new RecordModel(
                symbol,
                avroName,
                avroNamespace,
                fullName,
                members.ToImmutableArray());
            _records.Add(symbol, model);
            _building.Remove(symbol);
            return model;
        }

        private TypeModel? BuildType(
            ITypeSymbol symbol,
            ImmutableArray<ITypeSymbol> unionTypes,
            string? logicalType,
            int precision,
            int scale,
            ISymbol member)
        {
            if (!unionTypes.IsDefaultOrEmpty)
            {
                if (symbol.SpecialType != SpecialType.System_Object && symbol.TypeKind != TypeKind.Interface)
                {
                    Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name,
                        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        "explicit unions require an object or interface member");
                    return null;
                }
                var branches = ImmutableArray.CreateBuilder<TypeModel>();
                if (symbol.NullableAnnotation == NullableAnnotation.Annotated)
                    branches.Add(TypeModel.Primitive(TypeKindModel.Null));
                foreach (var unionType in unionTypes)
                {
                    if (unionType is null)
                    {
                        Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name,
                            "<null>", "union branch types cannot be null");
                        return null;
                    }
                    if (symbol.TypeKind == TypeKind.Interface &&
                        !SymbolEqualityComparer.Default.Equals(unionType, symbol) &&
                        !unionType.AllInterfaces.Any(candidate =>
                            SymbolEqualityComparer.Default.Equals(candidate, symbol)))
                    {
                        Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name,
                            unionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            $"union branch does not implement '{symbol.ToDisplayString()}'");
                        return null;
                    }
                    var branch = BuildType(unionType, default, null, 0, 0, member);
                    if (branch is null)
                        return null;
                    branches.Add(branch.WithoutNullable());
                }
                var identities = new HashSet<string>(StringComparer.Ordinal);
                foreach (var branch in branches)
                {
                    var identity = GetUnionIdentity(branch);
                    if (!identities.Add(identity))
                    {
                        Error(AmbiguousUnion, member.Locations.FirstOrDefault(), member.Name,
                            $"duplicate Avro branch '{identity}'");
                        return null;
                    }
                }
                return TypeModel.Union(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), branches.ToImmutable());
            }

            if (symbol is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var inner = BuildType(named.TypeArguments[0], default, logicalType, precision, scale, member);
                return inner is null ? null : TypeModel.Nullable(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), inner);
            }

            var display = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            TypeModel? model = symbol.SpecialType switch
            {
                SpecialType.System_Boolean => TypeModel.Primitive(TypeKindModel.Boolean, display),
                SpecialType.System_Int32 => TypeModel.Primitive(TypeKindModel.Int, display),
                SpecialType.System_Int64 => TypeModel.Primitive(TypeKindModel.Long, display),
                SpecialType.System_Single => TypeModel.Primitive(TypeKindModel.Float, display),
                SpecialType.System_Double => TypeModel.Primitive(TypeKindModel.Double, display),
                SpecialType.System_String => TypeModel.Primitive(TypeKindModel.String, display),
                SpecialType.System_Decimal when precision is > 0 and <= 29 && scale >= 0 && scale <= 28 && scale <= precision =>
                    TypeModel.Decimal(display, precision, scale),
                _ => null
            };

            if (model is null && symbol is IArrayTypeSymbol array)
            {
                if (array.ElementType.SpecialType == SpecialType.System_Byte)
                    model = TypeModel.Primitive(TypeKindModel.Bytes, display);
                else
                {
                    var item = BuildType(array.ElementType, default, null, 0, 0, member);
                    if (item is not null)
                        model = TypeModel.Collection(TypeKindModel.Array, display, item);
                }
            }

            if (model is null && symbol is INamedTypeSymbol generic && generic.IsGenericType)
            {
                var genericName = generic.OriginalDefinition.ToDisplayString();
                if (genericName == "System.Collections.Generic.List<T>")
                {
                    var item = BuildType(generic.TypeArguments[0], default, null, 0, 0, member);
                    if (item is not null)
                        model = TypeModel.Collection(TypeKindModel.List, display, item);
                }
                else if (genericName == "System.Collections.Generic.Dictionary<TKey, TValue>" &&
                         generic.TypeArguments[0].SpecialType == SpecialType.System_String)
                {
                    var item = BuildType(generic.TypeArguments[1], default, null, 0, 0, member);
                    if (item is not null)
                        model = TypeModel.Collection(TypeKindModel.Map, display, item);
                }
            }

            if (model is null && symbol.TypeKind == TypeKind.Enum && symbol is INamedTypeSymbol enumSymbol)
            {
                var symbols = enumSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(static field => field.HasConstantValue)
                    .OrderBy(GetSourceOrder)
                    .Select(static field => field.Name)
                    .ToImmutableArray();
                model = TypeModel.Enum(display, GetFullName(enumSymbol), symbols);
            }

            if (model is null && symbol is INamedTypeSymbol recordSymbol &&
                GetAttribute(recordSymbol, RecordAttribute) is not null)
            {
                var nested = BuildRecord(recordSymbol);
                if (nested is not null)
                    model = TypeModel.CreateRecord(display, nested);
            }

            if (model is null)
            {
                model = display switch
                {
                    "global::System.Guid" => TypeModel.Primitive(TypeKindModel.Uuid, display),
                    "global::System.DateOnly" => TypeModel.Primitive(TypeKindModel.Date, display),
                    "global::System.TimeOnly" => TypeModel.Primitive(TypeKindModel.TimeMicroseconds, display),
                    "global::System.TimeSpan" => TypeModel.Primitive(TypeKindModel.TimeMicroseconds, display),
                    "global::System.DateTime" => TypeModel.Primitive(TypeKindModel.TimestampMicroseconds, display),
                    "global::System.DateTimeOffset" => TypeModel.Primitive(TypeKindModel.TimestampMicroseconds, display),
                    "decimal" when precision is > 0 and <= 29 && scale >= 0 && scale <= 28 && scale <= precision =>
                        TypeModel.Decimal(display, precision, scale),
                    _ => null
                };
            }

            if (model is null)
            {
                Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name, display,
                    symbol.SpecialType == SpecialType.System_Decimal
                        ? "decimal fields require Precision between 1 and 29 and Scale between 0 and min(Precision, 28)"
                        : "use a supported primitive, enum, array, List<T>, Dictionary<string,T>, union, or [AvroRecord] type");
                return null;
            }

            if (model.Kind != TypeKindModel.Decimal && (precision != 0 || scale != 0))
            {
                Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name, display,
                    "Precision and Scale are supported only for decimal members");
                return null;
            }

            if (!string.IsNullOrEmpty(logicalType) && !string.Equals(model.LogicalTypeName, logicalType, StringComparison.Ordinal))
            {
                Error(UnsupportedType, member.Locations.FirstOrDefault(), member.Name, display,
                    $"logical type '{logicalType}' does not match its CLR mapping");
                return null;
            }

            if (symbol.NullableAnnotation == NullableAnnotation.Annotated && symbol.IsReferenceType)
                return TypeModel.Nullable(display, model);
            return model;
        }

        private void Error(DiagnosticDescriptor descriptor, Location? location, params object[] arguments)
        {
            HasErrors = true;
            _context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));
        }

        private static int GetSourceOrder(ISymbol symbol) =>
            symbol.DeclaringSyntaxReferences.FirstOrDefault()?.Span.Start ?? int.MaxValue;

        private static bool IsAvroNamespace(string value)
        {
            if (value.Length == 0)
                return true;
            var segments = value.Split('.');
            for (var index = 0; index < segments.Length; index++)
            {
                if (!IsAvroName(segments[index]))
                    return false;
            }
            return true;
        }

        private static bool IsAvroName(string? value)
        {
            if (value is null || value.Length == 0 || !IsAvroNameStart(value[0]))
                return false;
            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAvroNameStart(character) && character is not (>= '0' and <= '9'))
                    return false;
            }
            return true;
        }

        private static bool IsAvroNameStart(char value) =>
            value is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

        private static string GetClrType(ITypeSymbol symbol)
        {
            var display = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return symbol.IsReferenceType && symbol.NullableAnnotation == NullableAnnotation.Annotated
                ? display + "?"
                : display;
        }

        private static string GetUnionIdentity(TypeModel type) => type.Kind switch
        {
            TypeKindModel.Null => "null",
            TypeKindModel.Boolean => "boolean",
            TypeKindModel.Int or TypeKindModel.Date => "int",
            TypeKindModel.Long or TypeKindModel.TimeMicroseconds or TypeKindModel.TimestampMicroseconds => "long",
            TypeKindModel.Float => "float",
            TypeKindModel.Double => "double",
            TypeKindModel.Bytes or TypeKindModel.Decimal => "bytes",
            TypeKindModel.String or TypeKindModel.Uuid => "string",
            TypeKindModel.Record or TypeKindModel.Enum => type.FullName!,
            TypeKindModel.Array or TypeKindModel.List => "array",
            TypeKindModel.Map => "map",
            _ => type.Kind.ToString()
        };

        private static bool TryValidateDefault(TypeModel type, JsonElement value, out string error)
        {
            if (type.Kind is TypeKindModel.Nullable or TypeKindModel.Union)
                return TryValidateDefault(type.Branches[0], value, out error);

            var valid = type.Kind switch
            {
                TypeKindModel.Null => value.ValueKind == JsonValueKind.Null,
                TypeKindModel.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                TypeKindModel.Int => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                TypeKindModel.Long => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                TypeKindModel.Float => value.ValueKind == JsonValueKind.Number &&
                    value.TryGetSingle(out var single) && !float.IsInfinity(single) && !float.IsNaN(single),
                TypeKindModel.Double => value.ValueKind == JsonValueKind.Number &&
                    value.TryGetDouble(out var @double) && !double.IsInfinity(@double) && !double.IsNaN(@double),
                TypeKindModel.String => value.ValueKind == JsonValueKind.String,
                TypeKindModel.Bytes => value.ValueKind == JsonValueKind.String && IsValidBytesDefault(value.GetString()!),
                TypeKindModel.Enum => value.ValueKind == JsonValueKind.String && value.GetString() is { } symbol &&
                    type.Symbols.Contains(symbol, StringComparer.Ordinal),
                _ => false
            };
            error = valid
                ? string.Empty
                : $"value is incompatible with the first Avro branch '{GetUnionIdentity(type)}' or uses an unsupported complex default";
            return valid;
        }

        private static bool IsValidBytesDefault(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] > byte.MaxValue)
                    return false;
            }
            return true;
        }

        private static string GetFullName(INamedTypeSymbol symbol)
        {
            var attribute = GetAttribute(symbol, RecordAttribute);
            var name = GetNamedString(attribute, "Name") ?? symbol.Name;
            var @namespace = GetNamedString(attribute, "Namespace") ??
                (symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString());
            return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
        }
    }

    private sealed class CodecEmitter
    {
        private readonly RecordModel _record;
        private int _localId;

        internal CodecEmitter(RecordModel record) => _record = record;

        internal string Emit()
        {
            var schema = BuildSchema(_record);
            var parsingCanonicalForm = BuildParsingCanonicalForm(_record);
            var parsingFingerprint64 = ComputeParsingFingerprint64(parsingCanonicalForm);
            var code = new StringBuilder(32_768);
            code.AppendLine("// <auto-generated />");
            code.AppendLine("#nullable enable");
            if (!_record.Symbol.ContainingNamespace.IsGlobalNamespace)
            {
                code.Append("namespace ").Append(_record.Symbol.ContainingNamespace.ToDisplayString())
                    .AppendLine(";").AppendLine();
            }

            code.Append(GetAccessibility(_record.Symbol)).Append(' ')
                .Append(GetDeclaration(_record.Symbol)).Append(' ')
                .Append(EscapeIdentifier(_record.Symbol.Name)).AppendLine();
            code.AppendLine("{");
            code.AppendLine("    /// <summary>Generated allocation-safe Avro codec.</summary>");
            code.Append("    public readonly struct AvroCodec : global::Dekaf.SchemaRegistry.Avro.Poco.IAvroPocoCodec<")
                .Append(_record.TypeName).AppendLine(">");
            code.AppendLine("    {");
            EmitMetadata(code, schema, parsingFingerprint64);
            EmitWrite(code);
            EmitRead(code);
            code.AppendLine("    }");
            EmitFactories(code);
            code.AppendLine("}");
            EmitExtensions(code);
            return code.ToString();
        }

        private void EmitMetadata(StringBuilder code, string schema, long parsingFingerprint64)
        {
            code.AppendLine("        private static readonly global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoField[] s_fields =");
            code.AppendLine("        [");
            foreach (var member in _record.Members)
            {
                code.Append("            new(")
                    .Append(Literal(member.AvroName)).Append(", ")
                    .Append(EmitStringArray(member.Aliases)).Append(", ")
                    .Append(member.DefaultJson is null ? "null" : Literal(member.DefaultJson)).Append(", ")
                    .Append(EmitTypeMetadata(member.Type)).AppendLine("),");
            }
            code.AppendLine("        ];");
            code.Append("        public static string SchemaJson => ").Append(Literal(schema)).AppendLine(";");
            code.Append("        public static global::System.ReadOnlySpan<byte> SchemaUtf8 => ")
                .Append(Literal(schema)).AppendLine("u8;");
            code.Append("        public static long ParsingFingerprint64 => unchecked((long)0x")
                .Append(unchecked((ulong)parsingFingerprint64).ToString("X16", CultureInfo.InvariantCulture))
                .AppendLine("UL);");
            code.Append("        public static string FullName => ").Append(Literal(_record.FullName)).AppendLine(";");
            code.AppendLine("        public static global::System.ReadOnlyMemory<global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoField> Fields => s_fields;");
            code.AppendLine();
        }

        private void EmitWrite(StringBuilder code)
        {
            code.Append("        public static void Write(ref global::Dekaf.SchemaRegistry.Avro.Poco.AvroValueWriter writer, ")
                .Append(_record.TypeName).AppendLine(" value)");
            code.AppendLine("        {");
            if (_record.Symbol.IsReferenceType)
                code.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(value);");
            for (var index = 0; index < _record.Members.Length; index++)
            {
                var member = _record.Members[index];
                var local = "__value" + index.ToString(CultureInfo.InvariantCulture);
                code.Append("            var ").Append(local).Append(" = value.")
                    .Append(EscapeIdentifier(member.ClrName)).AppendLine(";");
                EmitWriteValue(code, member.Type, local, "            ");
            }
            code.AppendLine("        }");
            code.AppendLine();
        }

        private void EmitRead(StringBuilder code)
        {
            code.Append("        public static ").Append(_record.TypeName)
                .AppendLine(" Read(ref global::Dekaf.SchemaRegistry.Avro.Poco.AvroValueReader reader, global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoReaderPlan plan)");
            code.AppendLine("        {");
            for (var index = 0; index < _record.Members.Length; index++)
            {
                code.Append("            ").Append(_record.Members[index].ClrType)
                    .Append(" __field").Append(index).AppendLine(" = default!;");
                if (_record.Members[index].DefaultJson is not null)
                    code.Append("            var __seen").Append(index).AppendLine(" = false;");
            }
            code.AppendLine("            for (var __writerIndex = 0; __writerIndex < plan.WriterFieldCount; __writerIndex++)");
            code.AppendLine("            {");
            code.AppendLine("                var __operation = plan.GetOperation(__writerIndex);");
            code.AppendLine("                switch (__operation.ReaderFieldIndex)");
            code.AppendLine("                {");
            for (var index = 0; index < _record.Members.Length; index++)
            {
                code.Append("                    case ").Append(index).AppendLine(":");
                EmitReadValue(code, _record.Members[index].Type, "__operation.WriterType", "__field" + index, "                        ");
                if (_record.Members[index].DefaultJson is not null)
                    code.Append("                        __seen").Append(index).AppendLine(" = true;");
                code.AppendLine("                        break;");
            }
            code.AppendLine("                    default:");
            code.AppendLine("                        reader.Skip(__operation.WriterType);");
            code.AppendLine("                        break;");
            code.AppendLine("                }");
            code.AppendLine("            }");
            for (var index = 0; index < _record.Members.Length; index++)
            {
                var member = _record.Members[index];
                if (member.DefaultJson is null)
                    continue;
                code.Append("            if (!__seen").Append(index).AppendLine(")");
                code.Append("                __field").Append(index).Append(" = ")
                    .Append(EmitDefault(member.Type, member.DefaultJson)).AppendLine(";");
            }
            code.Append("            return new ").Append(_record.TypeName).AppendLine();
            code.AppendLine("            {");
            for (var index = 0; index < _record.Members.Length; index++)
            {
                code.Append("                ").Append(EscapeIdentifier(_record.Members[index].ClrName))
                    .Append(" = __field").Append(index).AppendLine(",");
            }
            code.AppendLine("            };");
            code.AppendLine("        }");
        }

        private void EmitFactories(StringBuilder code)
        {
            code.AppendLine();
            code.AppendLine("    /// <summary>Creates a Schema Registry serializer using the generated codec.</summary>");
            code.Append("    public static global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoSchemaRegistrySerializer<")
                .Append(_record.TypeName).Append(", AvroCodec> CreateAvroSerializer(")
                .AppendLine("global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroSerializerConfig? config = null, bool ownsClient = false) =>");
            code.Append("        new(schemaRegistry, config, ownsClient);").AppendLine();
            code.AppendLine();
            code.AppendLine("    /// <summary>Creates a Schema Registry deserializer using the generated codec.</summary>");
            code.Append("    public static global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoSchemaRegistryDeserializer<")
                .Append(_record.TypeName).Append(", AvroCodec> CreateAvroDeserializer(")
                .AppendLine("global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroDeserializerConfig? config = null, bool ownsClient = false) =>");
            code.Append("        new(schemaRegistry, config, ownsClient);").AppendLine();
        }

        private void EmitExtensions(StringBuilder code)
        {
            var extensionName = Sanitize(_record.Symbol.ToDisplayString()) + "AvroPocoExtensions";
            code.AppendLine();
            code.Append(GetAccessibility(_record.Symbol)).Append(" static class ")
                .Append(extensionName).AppendLine();
            code.AppendLine("{");
            code.Append("    public static global::Dekaf.ProducerBuilder<TKey, ").Append(_record.TypeName)
                .Append("> UseAvroPocoSchemaRegistry<TKey>(this global::Dekaf.ProducerBuilder<TKey, ")
                .Append(_record.TypeName).AppendLine("> builder, global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroSerializerConfig? config = null) =>");
            code.Append("        builder.WithValueSerializer(").Append(_record.TypeName).AppendLine(".CreateAvroSerializer(schemaRegistry, config));");
            code.AppendLine();
            code.Append("    public static global::Dekaf.ConsumerBuilder<TKey, ").Append(_record.TypeName)
                .Append("> UseAvroPocoSchemaRegistry<TKey>(this global::Dekaf.ConsumerBuilder<TKey, ")
                .Append(_record.TypeName).AppendLine("> builder, global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroDeserializerConfig? config = null) =>");
            code.Append("        builder.WithValueDeserializer(").Append(_record.TypeName).AppendLine(".CreateAvroDeserializer(schemaRegistry, config));");
            code.AppendLine();
            code.Append("    public static global::Dekaf.ProducerBuilder<").Append(_record.TypeName)
                .Append(", TValue> UseAvroPocoSchemaRegistryKey<TValue>(this global::Dekaf.ProducerBuilder<")
                .Append(_record.TypeName).AppendLine(", TValue> builder, global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroSerializerConfig? config = null) =>");
            code.Append("        builder.WithKeySerializer(").Append(_record.TypeName).AppendLine(".CreateAvroSerializer(schemaRegistry, config));");
            code.AppendLine();
            code.Append("    public static global::Dekaf.ConsumerBuilder<").Append(_record.TypeName)
                .Append(", TValue> UseAvroPocoSchemaRegistryKey<TValue>(this global::Dekaf.ConsumerBuilder<")
                .Append(_record.TypeName).AppendLine(", TValue> builder, global::Dekaf.SchemaRegistry.ISchemaRegistryClient schemaRegistry, global::Dekaf.SchemaRegistry.Avro.AvroDeserializerConfig? config = null) =>");
            code.Append("        builder.WithKeyDeserializer(").Append(_record.TypeName).AppendLine(".CreateAvroDeserializer(schemaRegistry, config));");
            code.AppendLine("}");
        }

        private void EmitWriteValue(StringBuilder code, TypeModel type, string value, string indent)
        {
            if (type.Kind == TypeKindModel.Nullable || type.Kind == TypeKindModel.Union)
            {
                var branches = type.Branches;
                var nullIndex = FindKind(branches, TypeKindModel.Null);
                code.Append(indent).Append("if (").Append(value).AppendLine(" is null)");
                code.Append(indent).AppendLine("{");
                if (nullIndex >= 0)
                    code.Append(indent).Append("    writer.WriteIndex(").Append(nullIndex).AppendLine(");");
                else
                    code.Append(indent).AppendLine("    throw new global::System.InvalidOperationException(\"Non-nullable POCO union value cannot be null.\");");
                code.Append(indent).AppendLine("}");
                code.Append(indent).AppendLine("else");
                code.Append(indent).AppendLine("{");
                if (type.Kind == TypeKindModel.Nullable)
                {
                    var nonNullIndex = nullIndex == 0 ? 1 : 0;
                    code.Append(indent).Append("    writer.WriteIndex(").Append(nonNullIndex).AppendLine(");");
                    var innerValue = type.SymbolType.EndsWith("?", StringComparison.Ordinal) &&
                                     type.Branches[nonNullIndex].IsValueType
                        ? value + ".Value"
                        : value;
                    EmitWriteValue(code, type.Branches[nonNullIndex], innerValue, indent + "    ");
                }
                else
                {
                    for (var index = 0; index < branches.Length; index++)
                    {
                        if (branches[index].Kind == TypeKindModel.Null)
                            continue;
                        code.Append(indent).Append(index == (nullIndex == 0 ? 1 : 0) ? "    if" : "    else if")
                            .Append(" (").Append(value).Append(" is ").Append(branches[index].SymbolType)
                            .Append(" __union").Append(_localId).AppendLine(")");
                        code.Append(indent).AppendLine("    {");
                        code.Append(indent).Append("        writer.WriteIndex(").Append(index).AppendLine(");");
                        EmitWriteValue(code, branches[index], "__union" + _localId, indent + "        ");
                        code.Append(indent).AppendLine("    }");
                        _localId++;
                    }
                    code.Append(indent).AppendLine("    else");
                    code.Append(indent).AppendLine("    {");
                    code.Append(indent).AppendLine("        throw new global::System.InvalidOperationException(\"POCO union value has no configured Avro branch.\");");
                    code.Append(indent).AppendLine("    }");
                }
                code.Append(indent).AppendLine("}");
                return;
            }

            switch (type.Kind)
            {
                case TypeKindModel.Null:
                    code.Append(indent).AppendLine("global::Dekaf.SchemaRegistry.Avro.Poco.AvroValueWriter.WriteNull();");
                    break;
                case TypeKindModel.Boolean:
                    code.Append(indent).Append("writer.WriteBoolean(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Int:
                    code.Append(indent).Append("writer.WriteInt32(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Long:
                    code.Append(indent).Append("writer.WriteInt64(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Float:
                    code.Append(indent).Append("writer.WriteSingle(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Double:
                    code.Append(indent).Append("writer.WriteDouble(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Bytes:
                    code.Append(indent).Append("global::System.ArgumentNullException.ThrowIfNull(")
                        .Append(value).AppendLine(");");
                    code.Append(indent).Append("writer.WriteBytes(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.String:
                    code.Append(indent).Append("writer.WriteString(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Enum:
                    EmitWriteEnum(code, type, value, indent);
                    break;
                case TypeKindModel.Record:
                    code.Append(indent).Append(type.SymbolType).Append(".AvroCodec.Write(ref writer, ")
                        .Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Array:
                case TypeKindModel.List:
                    EmitWriteCollection(code, type, value, indent);
                    break;
                case TypeKindModel.Map:
                    EmitWriteMap(code, type, value, indent);
                    break;
                case TypeKindModel.Date:
                    code.Append(indent).Append("writer.WriteInt32(").Append(value)
                        .AppendLine(".DayNumber - global::System.DateOnly.FromDateTime(global::System.DateTime.UnixEpoch).DayNumber);");
                    break;
                case TypeKindModel.TimeMicroseconds:
                    code.Append(indent).Append("writer.WriteInt64(").Append(value).AppendLine(".Ticks / 10L);");
                    break;
                case TypeKindModel.TimestampMicroseconds:
                    if (type.SymbolType == "global::System.DateTimeOffset")
                        code.Append(indent).Append("writer.WriteInt64((").Append(value)
                            .AppendLine(".UtcTicks - global::System.DateTimeOffset.UnixEpoch.UtcTicks) / 10L);");
                    else
                        code.Append(indent).Append("writer.WriteInt64((").Append(value)
                            .AppendLine(".ToUniversalTime().Ticks - global::System.DateTime.UnixEpoch.Ticks) / 10L);");
                    break;
                case TypeKindModel.Uuid:
                    code.Append(indent).Append("writer.WriteUuid(").Append(value).AppendLine(");");
                    break;
                case TypeKindModel.Decimal:
                    code.Append(indent).Append("global::Dekaf.SchemaRegistry.Avro.Poco.AvroDecimalCodec.Write(ref writer, ")
                        .Append(value).Append(", ").Append(type.Precision).Append(", ").Append(type.Scale).AppendLine(");");
                    break;
                default:
                    throw new InvalidOperationException("Unsupported generated write type.");
            }
        }

        private void EmitReadValue(StringBuilder code, TypeModel type, string node, string target, string indent)
        {
            if (type.Kind is TypeKindModel.Nullable or TypeKindModel.Union)
            {
                var branch = "__branch" + _localId++;
                code.Append(indent).Append("var ").Append(branch).Append(" = ").Append(node).AppendLine(";");
                code.Append(indent).Append("if (").Append(branch).AppendLine(".Kind == global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind.Union)");
                code.Append(indent).AppendLine("{");
                var branchIndex = "__branchIndex" + _localId++;
                code.Append(indent).Append("    var ").Append(branchIndex).Append(" = reader.ReadIndex(")
                    .Append(branch).AppendLine(".Branches.Length);");
                code.Append(indent).Append("    ").Append(branch).Append(" = ").Append(branch)
                    .Append(".Branches.Span[").Append(branchIndex).AppendLine("];");
                code.Append(indent).AppendLine("}");
                code.Append(indent).Append("if (").Append(branch)
                    .AppendLine(".Kind == global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind.Null)");
                code.Append(indent).AppendLine("{");
                code.Append(indent).Append("    ").Append(target).AppendLine(" = default!;");
                code.Append(indent).AppendLine("}");
                code.Append(indent).AppendLine("else");
                code.Append(indent).AppendLine("{");
                var nonNull = type.Branches.First(static branchType => branchType.Kind != TypeKindModel.Null);
                if (type.Kind == TypeKindModel.Nullable)
                {
                    var temp = "__nullable" + _localId++;
                    code.Append(indent).Append("    ").Append(nonNull.SymbolType).Append(' ').Append(temp).AppendLine(" = default!;");
                    EmitReadValue(code, nonNull, branch, temp, indent + "    ");
                    code.Append(indent).Append("    ").Append(target).Append(" = ").Append(temp).AppendLine(";");
                }
                else
                {
                    var first = true;
                    foreach (var unionBranch in type.Branches)
                    {
                        if (unionBranch.Kind == TypeKindModel.Null)
                            continue;
                        var readerBranchIndex = FindBranchIndex(type.Branches, unionBranch);
                        code.Append(indent).Append(first ? "    if" : "    else if").Append(" (")
                            .Append(branch).Append(".ReaderUnionBranchIndex == ").Append(readerBranchIndex).AppendLine(")");
                        code.Append(indent).AppendLine("    {");
                        var temp = "__unionRead" + _localId++;
                        code.Append(indent).Append("        ").Append(unionBranch.SymbolType).Append(' ')
                            .Append(temp).AppendLine(" = default!;");
                        EmitReadValue(code, unionBranch, branch, temp, indent + "        ");
                        code.Append(indent).Append("        ").Append(target).Append(" = ").Append(temp).AppendLine(";");
                        code.Append(indent).AppendLine("    }");
                        first = false;
                    }
                    code.Append(indent).AppendLine("    else");
                    code.Append(indent).AppendLine("    {");
                    code.Append(indent).AppendLine("        throw new global::System.IO.InvalidDataException(\"Writer union branch has no generated POCO target.\");");
                    code.Append(indent).AppendLine("    }");
                }
                code.Append(indent).AppendLine("}");
                return;
            }

            switch (type.Kind)
            {
                case TypeKindModel.Boolean:
                    Assign(code, target, "reader.ReadBoolean()", indent);
                    break;
                case TypeKindModel.Int:
                    Assign(code, target, "reader.ReadInt32()", indent);
                    break;
                case TypeKindModel.Long:
                    Assign(code, target, "reader.ReadInt64()", indent);
                    break;
                case TypeKindModel.Float:
                    Assign(code, target, node + ".Kind == global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind.Float ? reader.ReadSingle() : (float)reader.ReadInt64()", indent);
                    break;
                case TypeKindModel.Double:
                    Assign(code, target, node + ".Kind switch { global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind.Double => reader.ReadDouble(), global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind.Float => reader.ReadSingle(), _ => reader.ReadInt64() }", indent);
                    break;
                case TypeKindModel.Bytes:
                    Assign(code, target, "reader.ReadBytes()", indent);
                    break;
                case TypeKindModel.String:
                    Assign(code, target, "reader.ReadString()", indent);
                    break;
                case TypeKindModel.Enum:
                    EmitReadEnum(code, type, node, target, indent);
                    break;
                case TypeKindModel.Record:
                    Assign(code, target, type.SymbolType + ".AvroCodec.Read(ref reader, " + node + ".RecordPlan!)", indent);
                    break;
                case TypeKindModel.Array:
                case TypeKindModel.List:
                    EmitReadCollection(code, type, node, target, indent);
                    break;
                case TypeKindModel.Map:
                    EmitReadMap(code, type, node, target, indent);
                    break;
                case TypeKindModel.Date:
                    Assign(code, target, "global::System.DateOnly.FromDateTime(global::System.DateTime.UnixEpoch).AddDays(reader.ReadInt32())", indent);
                    break;
                case TypeKindModel.TimeMicroseconds:
                    Assign(code, target, type.SymbolType == "global::System.TimeOnly"
                        ? "new global::System.TimeOnly(checked(reader.ReadInt64() * 10L))"
                        : "global::System.TimeSpan.FromTicks(checked(reader.ReadInt64() * 10L))", indent);
                    break;
                case TypeKindModel.TimestampMicroseconds:
                    Assign(code, target, type.SymbolType == "global::System.DateTimeOffset"
                        ? "global::System.DateTimeOffset.UnixEpoch.AddTicks(checked(reader.ReadInt64() * 10L))"
                        : "global::System.DateTime.UnixEpoch.AddTicks(checked(reader.ReadInt64() * 10L))", indent);
                    break;
                case TypeKindModel.Uuid:
                    Assign(code, target, "reader.ReadUuid()", indent);
                    break;
                case TypeKindModel.Decimal:
                    Assign(code, target, "global::Dekaf.SchemaRegistry.Avro.Poco.AvroDecimalCodec.Read(ref reader, " + type.Precision + ", " + type.Scale + ")", indent);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported generated read type.");
            }
        }

        private static void EmitWriteEnum(StringBuilder code, TypeModel type, string value, string indent)
        {
            code.Append(indent).AppendLine("writer.WriteIndex(" + value + " switch");
            code.Append(indent).AppendLine("{");
            for (var index = 0; index < type.Symbols.Length; index++)
            {
                code.Append(indent).Append("    ").Append(type.SymbolType).Append('.')
                    .Append(EscapeIdentifier(type.Symbols[index])).Append(" => ").Append(index).AppendLine(",");
            }
            code.Append(indent).AppendLine("    _ => throw new global::System.InvalidOperationException(\"Enum value is not declared in the generated Avro schema.\")");
            code.Append(indent).AppendLine("});");
        }

        private void EmitReadEnum(StringBuilder code, TypeModel type, string node, string target, string indent)
        {
            var writerIndex = "__enumWriter" + _localId++;
            var readerIndex = "__enumReader" + _localId++;
            code.Append(indent).Append("var ").Append(writerIndex).Append(" = reader.ReadIndex(")
                .Append(node).AppendLine(".EnumMap.Length);");
            code.Append(indent).Append("var ").Append(readerIndex).Append(" = ").Append(node)
                .Append(".EnumMap.Span[").Append(writerIndex).AppendLine("];");
            code.Append(indent).Append(target).Append(" = ").Append(readerIndex).AppendLine(" switch");
            code.Append(indent).AppendLine("{");
            for (var index = 0; index < type.Symbols.Length; index++)
            {
                code.Append(indent).Append("    ").Append(index).Append(" => ").Append(type.SymbolType)
                    .Append('.').Append(EscapeIdentifier(type.Symbols[index])).AppendLine(",");
            }
            code.Append(indent).AppendLine("    _ => throw new global::System.IO.InvalidDataException(\"Enum index is out of range.\")");
            code.Append(indent).AppendLine("};");
        }

        private void EmitWriteCollection(StringBuilder code, TypeModel type, string value, string indent)
        {
            var index = "__index" + _localId++;
            var count = type.Kind == TypeKindModel.Array ? ".Length" : ".Count";
            code.Append(indent).Append("if (").Append(value).Append(count).AppendLine(" != 0)");
            code.Append(indent).AppendLine("{");
            code.Append(indent).Append("    writer.WriteBlockCount(").Append(value).Append(count).AppendLine(");");
            code.Append(indent).Append("    for (var ").Append(index).Append(" = 0; ").Append(index)
                .Append(" < ").Append(value).Append(count).Append("; ").Append(index).AppendLine("++)");
            code.Append(indent).AppendLine("    {");
            EmitWriteValue(code, type.Item!, value + "[" + index + "]", indent + "        ");
            code.Append(indent).AppendLine("    }");
            code.Append(indent).AppendLine("}");
            code.Append(indent).AppendLine("writer.WriteBlockEnd();");
        }

        private void EmitWriteMap(StringBuilder code, TypeModel type, string value, string indent)
        {
            var pair = "__pair" + _localId++;
            code.Append(indent).Append("if (").Append(value).AppendLine(".Count != 0)");
            code.Append(indent).AppendLine("{");
            code.Append(indent).Append("    writer.WriteBlockCount(").Append(value).AppendLine(".Count);");
            code.Append(indent).Append("    foreach (var ").Append(pair).Append(" in ").Append(value).AppendLine(")");
            code.Append(indent).AppendLine("    {");
            code.Append(indent).Append("        writer.WriteString(").Append(pair).AppendLine(".Key);");
            EmitWriteValue(code, type.Item!, pair + ".Value", indent + "        ");
            code.Append(indent).AppendLine("    }");
            code.Append(indent).AppendLine("}");
            code.Append(indent).AppendLine("writer.WriteBlockEnd();");
        }

        private void EmitReadCollection(StringBuilder code, TypeModel type, string node, string target, string indent)
        {
            var count = "__count" + _localId++;
            var result = "__collection" + _localId++;
            var offset = "__offset" + _localId++;
            var block = "__block" + _localId++;
            var index = "__index" + _localId++;
            code.Append(indent).Append("var ").Append(block).AppendLine(" = reader.ReadBlockCount();");
            code.Append(indent).Append("var ").Append(count).Append(" = ").Append(block).AppendLine(";");
            code.Append(indent).Append("var ").Append(result).Append(" = ")
                .Append(type.Kind == TypeKindModel.Array
                    ? "new " + type.Item!.SymbolType + "[" + count + "]"
                    : "new " + type.SymbolType + "(" + count + ")")
                .AppendLine(";");
            if (type.Kind == TypeKindModel.Array)
                code.Append(indent).Append("var ").Append(offset).AppendLine(" = 0;");
            code.Append(indent).Append("while (").Append(block).AppendLine(" != 0)");
            code.Append(indent).AppendLine("{");
            if (type.Kind == TypeKindModel.Array)
            {
                code.Append(indent).Append("    if (").Append(block).Append(" > ").Append(result)
                    .Append(".Length - ").Append(offset).AppendLine(")");
                code.Append(indent).Append("        global::System.Array.Resize(ref ").Append(result)
                    .Append(", checked(").Append(offset).Append(" + ").Append(block).AppendLine("));");
            }
            code.Append(indent).Append("    for (var ").Append(index).Append(" = 0; ").Append(index)
                .Append(" < ").Append(block).Append("; ").Append(index).AppendLine("++)");
            code.Append(indent).AppendLine("    {");
            var item = "__item" + _localId++;
            code.Append(indent).Append("        ").Append(type.Item!.SymbolType).Append(' ').Append(item).AppendLine(" = default!;");
            EmitReadValue(code, type.Item, node + ".Item!", item, indent + "        ");
            if (type.Kind == TypeKindModel.Array)
                code.Append(indent).Append("        ").Append(result).Append('[').Append(offset).Append("++] = ").Append(item).AppendLine(";");
            else
                code.Append(indent).Append("        ").Append(result).Append(".Add(").Append(item).AppendLine(");");
            code.Append(indent).AppendLine("    }");
            code.Append(indent).Append("    ").Append(block).AppendLine(" = reader.ReadBlockCount();");
            code.Append(indent).AppendLine("}");
            code.Append(indent).Append(target).Append(" = ").Append(result).AppendLine(";");
        }

        private void EmitReadMap(StringBuilder code, TypeModel type, string node, string target, string indent)
        {
            var count = "__count" + _localId++;
            var result = "__map" + _localId++;
            var block = "__block" + _localId++;
            var index = "__index" + _localId++;
            code.Append(indent).Append("var ").Append(block).AppendLine(" = reader.ReadBlockCount();");
            code.Append(indent).Append("var ").Append(count).Append(" = ").Append(block).AppendLine(";");
            code.Append(indent).Append("var ").Append(result).Append(" = new ").Append(type.SymbolType)
                .Append('(').Append(count).AppendLine(");");
            code.Append(indent).Append("while (").Append(block).AppendLine(" != 0)");
            code.Append(indent).AppendLine("{");
            code.Append(indent).Append("    for (var ").Append(index).Append(" = 0; ").Append(index)
                .Append(" < ").Append(block).Append("; ").Append(index).AppendLine("++)");
            code.Append(indent).AppendLine("    {");
            code.Append(indent).AppendLine("        var __key = reader.ReadString();");
            var item = "__item" + _localId++;
            code.Append(indent).Append("        ").Append(type.Item!.SymbolType).Append(' ').Append(item).AppendLine(" = default!;");
            EmitReadValue(code, type.Item, node + ".Item!", item, indent + "        ");
            code.Append(indent).Append("        ").Append(result).Append(".Add(__key, ").Append(item).AppendLine(");");
            code.Append(indent).AppendLine("    }");
            code.Append(indent).Append("    ").Append(block).AppendLine(" = reader.ReadBlockCount();");
            code.Append(indent).AppendLine("}");
            code.Append(indent).Append(target).Append(" = ").Append(result).AppendLine(";");
        }

        private static void Assign(StringBuilder code, string target, string expression, string indent) =>
            code.Append(indent).Append(target).Append(" = ").Append(expression).AppendLine(";");

        private static int FindKind(ImmutableArray<TypeModel> branches, TypeKindModel kind)
        {
            for (var index = 0; index < branches.Length; index++)
            {
                if (branches[index].Kind == kind)
                    return index;
            }
            return -1;
        }

        private static int FindBranchIndex(ImmutableArray<TypeModel> branches, TypeModel branch)
        {
            for (var index = 0; index < branches.Length; index++)
            {
                if (ReferenceEquals(branches[index], branch))
                    return index;
            }

            return -1;
        }

        private static string BuildSchema(RecordModel root)
        {
            var builder = new StringBuilder();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            AppendRecordSchema(builder, root, emitted);
            return builder.ToString();
        }

        private static string BuildParsingCanonicalForm(RecordModel root)
        {
            var builder = new StringBuilder();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            AppendCanonicalRecord(builder, root, emitted);
            return builder.ToString();
        }

        private static void AppendCanonicalRecord(StringBuilder builder, RecordModel record, HashSet<string> emitted)
        {
            if (!emitted.Add(record.FullName))
            {
                AppendJsonString(builder, record.FullName);
                return;
            }

            builder.Append("{\"name\":");
            AppendJsonString(builder, record.FullName);
            builder.Append(",\"type\":\"record\",\"fields\":[");
            for (var index = 0; index < record.Members.Length; index++)
            {
                if (index != 0)
                    builder.Append(',');
                builder.Append("{\"name\":");
                AppendJsonString(builder, record.Members[index].AvroName);
                builder.Append(",\"type\":");
                AppendCanonicalType(builder, record.Members[index].Type, emitted);
                builder.Append('}');
            }
            builder.Append("]}");
        }

        private static void AppendCanonicalType(StringBuilder builder, TypeModel type, HashSet<string> emitted)
        {
            switch (type.Kind)
            {
                case TypeKindModel.Null: builder.Append("\"null\""); return;
                case TypeKindModel.Boolean: builder.Append("\"boolean\""); return;
                case TypeKindModel.Int:
                case TypeKindModel.Date: builder.Append("\"int\""); return;
                case TypeKindModel.Long:
                case TypeKindModel.TimeMicroseconds:
                case TypeKindModel.TimestampMicroseconds: builder.Append("\"long\""); return;
                case TypeKindModel.Float: builder.Append("\"float\""); return;
                case TypeKindModel.Double: builder.Append("\"double\""); return;
                case TypeKindModel.Bytes:
                case TypeKindModel.Decimal: builder.Append("\"bytes\""); return;
                case TypeKindModel.String:
                case TypeKindModel.Uuid: builder.Append("\"string\""); return;
                case TypeKindModel.Array:
                case TypeKindModel.List:
                    builder.Append("{\"type\":\"array\",\"items\":");
                    AppendCanonicalType(builder, type.Item!, emitted);
                    builder.Append('}');
                    return;
                case TypeKindModel.Map:
                    builder.Append("{\"type\":\"map\",\"values\":");
                    AppendCanonicalType(builder, type.Item!, emitted);
                    builder.Append('}');
                    return;
                case TypeKindModel.Nullable:
                case TypeKindModel.Union:
                    builder.Append('[');
                    for (var index = 0; index < type.Branches.Length; index++)
                    {
                        if (index != 0)
                            builder.Append(',');
                        AppendCanonicalType(builder, type.Branches[index], emitted);
                    }
                    builder.Append(']');
                    return;
                case TypeKindModel.Record:
                    AppendCanonicalRecord(builder, type.Record!, emitted);
                    return;
                case TypeKindModel.Enum:
                    if (!emitted.Add(type.FullName!))
                    {
                        AppendJsonString(builder, type.FullName!);
                        return;
                    }
                    builder.Append("{\"name\":");
                    AppendJsonString(builder, type.FullName!);
                    builder.Append(",\"type\":\"enum\",\"symbols\":[");
                    for (var index = 0; index < type.Symbols.Length; index++)
                    {
                        if (index != 0)
                            builder.Append(',');
                        AppendJsonString(builder, type.Symbols[index]);
                    }
                    builder.Append("]}");
                    return;
                default:
                    throw new InvalidOperationException("Unsupported canonical schema type.");
            }
        }

        private static long ComputeParsingFingerprint64(string parsingCanonicalForm)
        {
            const ulong empty = 0xC15D213AA4D7A795UL;
            Span<ulong> table = stackalloc ulong[256];
            for (var index = 0; index < table.Length; index++)
            {
                var value = (ulong)index;
                for (var bit = 0; bit < 8; bit++)
                    value = (value >> 1) ^ (empty & unchecked(0UL - (value & 1)));
                table[index] = value;
            }

            var fingerprint = empty;
            foreach (var value in Encoding.UTF8.GetBytes(parsingCanonicalForm))
                fingerprint = (fingerprint >> 8) ^ table[(byte)(fingerprint ^ value)];
            return unchecked((long)fingerprint);
        }

        private static void AppendRecordSchema(StringBuilder builder, RecordModel record, HashSet<string> emitted)
        {
            if (!emitted.Add(record.FullName))
            {
                AppendJsonString(builder, record.FullName);
                return;
            }

            builder.Append("{\"type\":\"record\",\"name\":");
            AppendJsonString(builder, record.AvroName);
            if (!string.IsNullOrEmpty(record.AvroNamespace))
            {
                builder.Append(",\"namespace\":");
                AppendJsonString(builder, record.AvroNamespace);
            }
            builder.Append(",\"fields\":[");
            for (var index = 0; index < record.Members.Length; index++)
            {
                if (index != 0)
                    builder.Append(',');
                var member = record.Members[index];
                builder.Append("{\"name\":");
                AppendJsonString(builder, member.AvroName);
                builder.Append(",\"type\":");
                AppendTypeSchema(builder, member.Type, emitted);
                if (!member.Aliases.IsDefaultOrEmpty)
                {
                    builder.Append(",\"aliases\":[");
                    for (var aliasIndex = 0; aliasIndex < member.Aliases.Length; aliasIndex++)
                    {
                        if (aliasIndex != 0)
                            builder.Append(',');
                        AppendJsonString(builder, member.Aliases[aliasIndex]);
                    }
                    builder.Append(']');
                }
                if (member.DefaultJson is not null)
                    builder.Append(",\"default\":").Append(member.DefaultJson);
                builder.Append('}');
            }
            builder.Append("]}");
        }

        private static void AppendTypeSchema(StringBuilder builder, TypeModel type, HashSet<string> emitted)
        {
            switch (type.Kind)
            {
                case TypeKindModel.Boolean: builder.Append("\"boolean\""); return;
                case TypeKindModel.Int: builder.Append("\"int\""); return;
                case TypeKindModel.Long: builder.Append("\"long\""); return;
                case TypeKindModel.Float: builder.Append("\"float\""); return;
                case TypeKindModel.Double: builder.Append("\"double\""); return;
                case TypeKindModel.Bytes: builder.Append("\"bytes\""); return;
                case TypeKindModel.String: builder.Append("\"string\""); return;
                case TypeKindModel.Null: builder.Append("\"null\""); return;
                case TypeKindModel.Array:
                case TypeKindModel.List:
                    builder.Append("{\"type\":\"array\",\"items\":");
                    AppendTypeSchema(builder, type.Item!, emitted);
                    builder.Append('}');
                    return;
                case TypeKindModel.Map:
                    builder.Append("{\"type\":\"map\",\"values\":");
                    AppendTypeSchema(builder, type.Item!, emitted);
                    builder.Append('}');
                    return;
                case TypeKindModel.Nullable:
                case TypeKindModel.Union:
                    builder.Append('[');
                    for (var index = 0; index < type.Branches.Length; index++)
                    {
                        if (index != 0)
                            builder.Append(',');
                        AppendTypeSchema(builder, type.Branches[index], emitted);
                    }
                    builder.Append(']');
                    return;
                case TypeKindModel.Record:
                    AppendRecordSchema(builder, type.Record!, emitted);
                    return;
                case TypeKindModel.Enum:
                    if (!emitted.Add(type.FullName!))
                    {
                        AppendJsonString(builder, type.FullName!);
                        return;
                    }
                    var separator = type.FullName!.LastIndexOf('.');
                    var enumName = separator < 0 ? type.FullName : type.FullName.Substring(separator + 1);
                    var enumNamespace = separator < 0 ? string.Empty : type.FullName.Substring(0, separator);
                    builder.Append("{\"type\":\"enum\",\"name\":");
                    AppendJsonString(builder, enumName!);
                    if (enumNamespace.Length != 0)
                    {
                        builder.Append(",\"namespace\":");
                        AppendJsonString(builder, enumNamespace);
                    }
                    builder.Append(",\"symbols\":[");
                    for (var index = 0; index < type.Symbols.Length; index++)
                    {
                        if (index != 0)
                            builder.Append(',');
                        AppendJsonString(builder, type.Symbols[index]);
                    }
                    builder.Append("]}");
                    return;
                case TypeKindModel.Date:
                    builder.Append("{\"type\":\"int\",\"logicalType\":\"date\"}"); return;
                case TypeKindModel.TimeMicroseconds:
                    builder.Append("{\"type\":\"long\",\"logicalType\":\"time-micros\"}"); return;
                case TypeKindModel.TimestampMicroseconds:
                    builder.Append("{\"type\":\"long\",\"logicalType\":\"timestamp-micros\"}"); return;
                case TypeKindModel.Uuid:
                    builder.Append("{\"type\":\"string\",\"logicalType\":\"uuid\"}"); return;
                case TypeKindModel.Decimal:
                    builder.Append("{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":")
                        .Append(type.Precision).Append(",\"scale\":").Append(type.Scale).Append('}');
                    return;
                default:
                    throw new InvalidOperationException("Unsupported schema type.");
            }
        }

        private static string EmitTypeMetadata(TypeModel type)
        {
            var builder = new StringBuilder();
            builder.Append("new global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoType(")
                .Append(KindExpression(type.Kind));
            if (type.Item is not null)
                builder.Append(", item: ").Append(EmitTypeMetadata(type.Item));
            if (!type.Branches.IsDefaultOrEmpty)
            {
                builder.Append(", branches: new global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoType[] { ");
                foreach (var branch in type.Branches)
                    builder.Append(EmitTypeMetadata(branch)).Append(", ");
                builder.Append('}');
            }
            if (type.FullName is not null)
                builder.Append(", fullName: ").Append(Literal(type.FullName));
            if (type.Record is not null)
                builder.Append(", fields: ").Append(type.SymbolType).Append(".AvroCodec.Fields");
            if (!type.Symbols.IsDefaultOrEmpty)
                builder.Append(", symbols: ").Append(EmitStringArray(type.Symbols));
            if (type.Kind == TypeKindModel.Decimal)
                builder.Append(", precision: ").Append(type.Precision).Append(", scale: ").Append(type.Scale);
            builder.Append(')');
            return builder.ToString();
        }

        private static string KindExpression(TypeKindModel kind)
        {
            var runtimeKind = kind switch
            {
                TypeKindModel.List => "Array",
                TypeKindModel.Nullable => "Union",
                _ => kind.ToString()
            };
            return "global::Dekaf.SchemaRegistry.Avro.Poco.AvroPocoTypeKind." + runtimeKind;
        }

        private static string EmitDefault(TypeModel type, string json)
        {
            using var document = JsonDocument.Parse(json);
            return EmitDefault(type, document.RootElement);
        }

        private static string EmitDefault(TypeModel type, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return "default!";
            if (type.Kind is TypeKindModel.Nullable or TypeKindModel.Union)
                return EmitDefault(type.Branches[0], value);
            return type.Kind switch
            {
                TypeKindModel.Boolean => value.GetBoolean() ? "true" : "false",
                TypeKindModel.Int => value.GetInt32().ToString(CultureInfo.InvariantCulture),
                TypeKindModel.Long => value.GetInt64().ToString(CultureInfo.InvariantCulture) + "L",
                TypeKindModel.Float => value.GetSingle().ToString("R", CultureInfo.InvariantCulture) + "F",
                TypeKindModel.Double => value.GetDouble().ToString("R", CultureInfo.InvariantCulture) + "D",
                TypeKindModel.String => Literal(value.GetString()!),
                TypeKindModel.Bytes => EmitBytesDefault(value.GetString()!),
                TypeKindModel.Enum => type.SymbolType + "." + EscapeIdentifier(value.GetString()!),
                _ => throw new InvalidOperationException("Collection, record, union, and logical defaults require an explicit supported scalar default.")
            };
        }

        private static string EmitBytesDefault(string value)
        {
            var builder = new StringBuilder("new byte[] { ");
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] > byte.MaxValue)
                    throw new InvalidOperationException("Avro bytes defaults must contain only Unicode code points 0 through 255.");
                if (index != 0)
                    builder.Append(", ");
                builder.Append("(byte)").Append((int)value[index]);
            }
            return builder.Append(" }").ToString();
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

        private static string EmitStringArray(ImmutableArray<string> values) =>
            values.IsDefaultOrEmpty
                ? "global::System.Array.Empty<string>()"
                : "new string[] { " + string.Join(", ", values.Select(Literal)) + " }";

        private static string GetAccessibility(INamedTypeSymbol symbol) => symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal"
        };

        private static string GetDeclaration(INamedTypeSymbol symbol)
        {
            if (symbol.IsRecord)
                return symbol.TypeKind == TypeKind.Struct ? "partial record struct" : "partial record";
            if (symbol.TypeKind == TypeKind.Struct)
                return symbol.IsReadOnly ? "readonly partial struct" : "partial struct";
            return "partial class";
        }

        private static string EscapeIdentifier(string name) => "@" + name;
    }

    private sealed class RecordModel
    {
        internal RecordModel(
            INamedTypeSymbol symbol,
            string avroName,
            string avroNamespace,
            string fullName,
            ImmutableArray<MemberModel> members)
        {
            Symbol = symbol;
            AvroName = avroName;
            AvroNamespace = avroNamespace;
            FullName = fullName;
            Members = members;
            TypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        internal INamedTypeSymbol Symbol { get; }
        internal string AvroName { get; }
        internal string AvroNamespace { get; }
        internal string FullName { get; }
        internal ImmutableArray<MemberModel> Members { get; }
        internal string TypeName { get; }
    }

    private sealed class MemberModel
    {
        internal MemberModel(
            string clrName,
            string avroName,
            ImmutableArray<string> aliases,
            string? defaultJson,
            int order,
            int sourceOrder,
            string clrType,
            TypeModel type)
        {
            ClrName = clrName;
            AvroName = avroName;
            Aliases = aliases;
            DefaultJson = defaultJson;
            Order = order;
            SourceOrder = sourceOrder;
            ClrType = clrType;
            Type = type;
        }

        internal string ClrName { get; }
        internal string AvroName { get; }
        internal ImmutableArray<string> Aliases { get; }
        internal string? DefaultJson { get; }
        internal int Order { get; }
        internal int SourceOrder { get; }
        internal string ClrType { get; }
        internal TypeModel Type { get; }
    }

    private sealed class TypeModel
    {
        private TypeModel(TypeKindModel kind, string symbolType)
        {
            Kind = kind;
            SymbolType = symbolType;
        }

        internal TypeKindModel Kind { get; private set; }
        internal string SymbolType { get; }
        internal TypeModel? Item { get; private set; }
        internal ImmutableArray<TypeModel> Branches { get; private set; }
        internal RecordModel? Record { get; private set; }
        internal string? FullName { get; private set; }
        internal ImmutableArray<string> Symbols { get; private set; }
        internal int Precision { get; private set; }
        internal int Scale { get; private set; }
        internal bool IsValueType => Kind is not (TypeKindModel.String or TypeKindModel.Bytes or TypeKindModel.Record or TypeKindModel.Array or TypeKindModel.List or TypeKindModel.Map or TypeKindModel.Union);
        internal string? LogicalTypeName => Kind switch
        {
            TypeKindModel.Date => "date",
            TypeKindModel.TimeMicroseconds => "time-micros",
            TypeKindModel.TimestampMicroseconds => "timestamp-micros",
            TypeKindModel.Uuid => "uuid",
            TypeKindModel.Decimal => "decimal",
            _ => null
        };

        internal static TypeModel Primitive(TypeKindModel kind, string symbolType = "global::System.Object") => new(kind, symbolType);

        internal static TypeModel Collection(TypeKindModel kind, string symbolType, TypeModel item) =>
            new(kind, symbolType) { Item = item };

        internal static TypeModel Nullable(string symbolType, TypeModel inner) =>
            new(TypeKindModel.Nullable, symbolType)
            {
                Branches = ImmutableArray.Create(Primitive(TypeKindModel.Null), inner.WithoutNullable())
            };

        internal static TypeModel Union(string symbolType, ImmutableArray<TypeModel> branches) =>
            new(TypeKindModel.Union, symbolType) { Branches = branches };

        internal static TypeModel CreateRecord(string symbolType, RecordModel record) =>
            new(TypeKindModel.Record, symbolType) { Record = record, FullName = record.FullName };

        internal static TypeModel Enum(string symbolType, string fullName, ImmutableArray<string> symbols) =>
            new(TypeKindModel.Enum, symbolType) { FullName = fullName, Symbols = symbols };

        internal static TypeModel Decimal(string symbolType, int precision, int scale) =>
            new(TypeKindModel.Decimal, symbolType) { Precision = precision, Scale = scale };

        internal TypeModel WithoutNullable() => Kind == TypeKindModel.Nullable
            ? Branches.First(static branch => branch.Kind != TypeKindModel.Null)
            : this;
    }

    private enum TypeKindModel
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
        Enum,
        Array,
        List,
        Map,
        Nullable,
        Union,
        Date,
        TimeMicroseconds,
        TimestampMicroseconds,
        Uuid,
        Decimal
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().FirstOrDefault(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal));

    private static string? GetNamedString(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;

    private static int GetNamedInt(AttributeData? attribute, string name, int fallback = 0)
    {
        if (attribute is null)
            return fallback;
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is int value)
                return value;
        }
        return fallback;
    }

    private static ImmutableArray<string> GetNamedStrings(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<string>.Empty;
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name || pair.Value.Kind != TypedConstantKind.Array)
                continue;
            return pair.Value.Values.Select(static value => (string)value.Value!).ToImmutableArray();
        }
        return ImmutableArray<string>.Empty;
    }

    private static ImmutableArray<ITypeSymbol> GetNamedTypes(AttributeData? attribute, string name)
    {
        if (attribute is null)
            return ImmutableArray<ITypeSymbol>.Empty;
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key != name || pair.Value.Kind != TypedConstantKind.Array)
                continue;
            return pair.Value.Values.Select(static value => (ITypeSymbol)value.Value!).ToImmutableArray();
        }
        return ImmutableArray<ITypeSymbol>.Empty;
    }
}
