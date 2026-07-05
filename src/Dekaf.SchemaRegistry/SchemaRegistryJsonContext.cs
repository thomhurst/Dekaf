using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Dekaf.SchemaRegistry;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegisterSchemaRequest))]
[JsonSerializable(typeof(RegisterSchemaResponse))]
[JsonSerializable(typeof(GetSchemaResponse))]
[JsonSerializable(typeof(GetSubjectVersionResponse))]
[JsonSerializable(typeof(SchemaReferenceDto))]
[JsonSerializable(typeof(CompatibilityResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<int>))]
internal sealed partial class SchemaRegistryJsonContext : JsonSerializerContext;

internal static class SchemaRegistryJsonTypeInfo
{
    internal static readonly JsonTypeInfo<RegisterSchemaRequest> RegisterSchemaRequest =
        GetRequired<RegisterSchemaRequest>();

    internal static readonly JsonTypeInfo<RegisterSchemaResponse> RegisterSchemaResponse =
        GetRequired<RegisterSchemaResponse>();

    internal static readonly JsonTypeInfo<GetSchemaResponse> GetSchemaResponse =
        GetRequired<GetSchemaResponse>();

    internal static readonly JsonTypeInfo<GetSubjectVersionResponse> GetSubjectVersionResponse =
        GetRequired<GetSubjectVersionResponse>();

    internal static readonly JsonTypeInfo<CompatibilityResponse> CompatibilityResponse =
        GetRequired<CompatibilityResponse>();

    internal static readonly JsonTypeInfo<ErrorResponse> ErrorResponse =
        GetRequired<ErrorResponse>();

    internal static readonly JsonTypeInfo<List<string>> StringList =
        GetRequired<List<string>>();

    internal static readonly JsonTypeInfo<List<int>> Int32List =
        GetRequired<List<int>>();

    private static JsonTypeInfo<T> GetRequired<T>()
    {
        return (JsonTypeInfo<T>)SchemaRegistryJsonContext.Default.GetTypeInfo(typeof(T))!;
    }
}

internal sealed class RegisterSchemaRequest
{
    public required string Schema { get; init; }
    public string? SchemaType { get; init; }
    public List<SchemaReferenceDto>? References { get; init; }
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
}

internal sealed class GetSubjectVersionResponse
{
    public required string Subject { get; init; }
    public int Version { get; init; }
    public int Id { get; init; }
    public required string Schema { get; init; }
    public string? SchemaType { get; init; }
    public List<SchemaReferenceDto>? References { get; init; }
}

internal sealed class SchemaReferenceDto
{
    public required string Name { get; init; }
    public required string Subject { get; init; }
    public int Version { get; init; }
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
