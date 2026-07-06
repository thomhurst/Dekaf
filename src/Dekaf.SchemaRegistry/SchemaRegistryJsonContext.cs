using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dekaf.SchemaRegistry;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegisterSchemaRequest))]
[JsonSerializable(typeof(RegisterSchemaResponse))]
[JsonSerializable(typeof(GetSchemaResponse))]
[JsonSerializable(typeof(GetSubjectVersionResponse))]
[JsonSerializable(typeof(SchemaReferenceDto))]
[JsonSerializable(typeof(SchemaMetadataDto))]
[JsonSerializable(typeof(SchemaRuleSetDto))]
[JsonSerializable(typeof(SchemaRuleDto))]
[JsonSerializable(typeof(RegisterKekRequestDto))]
[JsonSerializable(typeof(KekDto))]
[JsonSerializable(typeof(RegisterDekRequestDto))]
[JsonSerializable(typeof(DekDto))]
[JsonSerializable(typeof(CompatibilityResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<int>))]
internal sealed partial class SchemaRegistryJsonContext : JsonSerializerContext;

internal sealed class RegisterSchemaRequest
{
    public required string Schema { get; init; }
    public string? SchemaType { get; init; }
    public List<SchemaReferenceDto>? References { get; init; }
    public SchemaMetadataDto? Metadata { get; init; }
    public SchemaRuleSetDto? RuleSet { get; init; }
}

internal sealed class RegisterSchemaResponse
{
    public int Id { get; init; }
}

internal sealed class GetSchemaResponse
{
    public required string Schema { get; init; }
    public string? SchemaType { get; init; }
    public List<SchemaReferenceDto>? References { get; init; }
    public SchemaMetadataDto? Metadata { get; init; }
    public SchemaRuleSetDto? RuleSet { get; init; }
}

internal sealed class GetSubjectVersionResponse
{
    public required string Subject { get; init; }
    public int Version { get; init; }
    public int Id { get; init; }
    public required string Schema { get; init; }
    public string? SchemaType { get; init; }
    public List<SchemaReferenceDto>? References { get; init; }
    public SchemaMetadataDto? Metadata { get; init; }
    public SchemaRuleSetDto? RuleSet { get; init; }
}

internal sealed class SchemaReferenceDto
{
    public required string Name { get; init; }
    public required string Subject { get; init; }
    public int Version { get; init; }
}

internal sealed class SchemaMetadataDto
{
    public Dictionary<string, HashSet<string>>? Tags { get; init; }
    public Dictionary<string, string>? Properties { get; init; }
    public HashSet<string>? Sensitive { get; init; }
}

internal sealed class SchemaRuleSetDto
{
    public List<SchemaRuleDto>? MigrationRules { get; init; }
    public List<SchemaRuleDto>? DomainRules { get; init; }
    public List<SchemaRuleDto>? EncodingRules { get; init; }
    public string? EnableAt { get; init; }
}

internal sealed class SchemaRuleDto
{
    public string? Name { get; init; }
    public string? Doc { get; init; }
    public string? Kind { get; init; }
    public string? Mode { get; init; }
    public string? Type { get; init; }
    public HashSet<string>? Tags { get; init; }
    [JsonPropertyName("params")]
    public Dictionary<string, string>? Params { get; init; }
    public string? Expr { get; init; }
    public string? OnSuccess { get; init; }
    public string? OnFailure { get; init; }
    public bool Disabled { get; init; }
}

internal sealed class RegisterKekRequestDto
{
    public required string Name { get; init; }
    public required string KmsType { get; init; }
    public required string KmsKeyId { get; init; }
    public Dictionary<string, string>? KmsProps { get; init; }
    public string? Doc { get; init; }
    public bool? Shared { get; init; }
    public bool? Deleted { get; init; }
}

internal sealed class KekDto
{
    public string? Name { get; init; }
    public string? KmsType { get; init; }
    public string? KmsKeyId { get; init; }
    public Dictionary<string, string>? KmsProps { get; init; }
    public string? Doc { get; init; }
    public bool Shared { get; init; }
    public bool Deleted { get; init; }
    public JsonElement? Ts { get; init; }
}

internal sealed class RegisterDekRequestDto
{
    public required string Subject { get; init; }
    public int? Version { get; init; }
    public string? Algorithm { get; init; }
    public string? EncryptedKeyMaterial { get; init; }
    public bool? Deleted { get; init; }
}

internal sealed class DekDto
{
    public string? KekName { get; init; }
    public string? Subject { get; init; }
    public int Version { get; init; }
    public string? Algorithm { get; init; }
    public string? EncryptedKeyMaterial { get; init; }
    public string? KeyMaterial { get; init; }
    public JsonElement? Ts { get; init; }
    public bool Deleted { get; init; }
}

internal sealed class CompatibilityResponse
{
    [JsonPropertyName("is_compatible")]
    public bool IsCompatible { get; init; }
}

internal sealed class ErrorResponse
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; init; }
    public string? Message { get; init; }
}
