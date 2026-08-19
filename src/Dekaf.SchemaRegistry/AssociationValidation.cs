namespace Dekaf.SchemaRegistry;

internal static class AssociationValidation
{
    internal static void ValidateGet(
        string resourceName,
        string resourceNamespace,
        string? resourceType,
        IReadOnlyList<string>? associationTypes,
        string? lifecycle,
        int offset,
        int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceNamespace);
        ValidateOptionalFilter(resourceType, nameof(resourceType));
        ValidateOptionalFilter(lifecycle, nameof(lifecycle));
        ValidateTypes(associationTypes);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, -1);
    }

    internal static void ValidateCreate(AssociationCreateOrUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResourceNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResourceType);
        ArgumentNullException.ThrowIfNull(request.Associations);
        if (request.Associations.Count == 0)
            throw new ArgumentException("At least one association is required.", nameof(request));

        for (var index = 0; index < request.Associations.Count; index++)
        {
            var association = request.Associations[index]
                ?? throw new ArgumentException("Associations cannot contain null entries.", nameof(request));
            ArgumentException.ThrowIfNullOrWhiteSpace(association.Subject);
            ArgumentException.ThrowIfNullOrWhiteSpace(association.AssociationType);
            ArgumentException.ThrowIfNullOrWhiteSpace(association.Lifecycle);
        }
    }

    internal static void ValidateDelete(
        string resourceId,
        string? resourceType,
        IReadOnlyList<string>? associationTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ValidateOptionalFilter(resourceType, nameof(resourceType));
        ValidateTypes(associationTypes);
    }

    private static void ValidateTypes(IReadOnlyList<string>? associationTypes)
    {
        if (associationTypes is null)
            return;

        for (var index = 0; index < associationTypes.Count; index++)
            ArgumentException.ThrowIfNullOrWhiteSpace(associationTypes[index]);
    }

    private static void ValidateOptionalFilter(string? value, string paramName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
    }
}
