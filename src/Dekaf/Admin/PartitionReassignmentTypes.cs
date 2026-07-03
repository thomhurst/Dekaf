namespace Dekaf.Admin;

/// <summary>
/// Optional value used by admin APIs that need to distinguish a value from an explicit removal.
/// </summary>
public readonly struct Optional<T>
{
    private readonly T? _value;

    internal Optional(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>
    /// True when this optional contains a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// The contained value.
    /// </summary>
    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException("Optional value is not present.");

    public static implicit operator Optional<T>(T? value) =>
        value is null ? default : Optional.Some(value);
}

/// <summary>
/// Factory methods for <see cref="Optional{T}"/>.
/// </summary>
public static class Optional
{
    /// <summary>
    /// Creates an optional value.
    /// </summary>
    public static Optional<T> Some<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Optional<T>(value);
    }

    /// <summary>
    /// Creates an empty optional value.
    /// </summary>
    public static Optional<T> None<T>() => default;
}

/// <summary>
/// Target replica assignment for a partition reassignment.
/// </summary>
public sealed class NewPartitionReassignment
{
    /// <summary>
    /// The target replica broker IDs. An empty list cancels an in-progress reassignment.
    /// </summary>
    public required IReadOnlyList<int> TargetReplicas { get; init; }

    /// <summary>
    /// Creates a reassignment to the specified target replicas.
    /// </summary>
    public static NewPartitionReassignment ToReplicas(params int[] replicas) =>
        new() { TargetReplicas = replicas.ToList() };
}

/// <summary>
/// An in-progress partition reassignment.
/// </summary>
public sealed class PartitionReassignment
{
    /// <summary>
    /// The current replica set.
    /// </summary>
    public required IReadOnlyList<int> Replicas { get; init; }

    /// <summary>
    /// Replicas currently being added.
    /// </summary>
    public required IReadOnlyList<int> AddingReplicas { get; init; }

    /// <summary>
    /// Replicas currently being removed.
    /// </summary>
    public required IReadOnlyList<int> RemovingReplicas { get; init; }
}

/// <summary>
/// Options for AlterPartitionReassignments.
/// </summary>
public sealed class AlterPartitionReassignmentsOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Whether changing a partition replication factor is allowed.
    /// </summary>
    public bool AllowReplicationFactorChange { get; init; } = true;
}

/// <summary>
/// Options for ListPartitionReassignments.
/// </summary>
public sealed class ListPartitionReassignmentsOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 60000;
}
