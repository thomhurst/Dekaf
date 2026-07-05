using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dekaf.Internal;

internal static class CompatibilityThrowHelpers
{
    public static void ThrowIfNull(
        [NotNull] object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(paramName);
    }

    public static void ThrowIfNullOrEmpty(
        [NotNull] string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(paramName);

        if (argument.Length == 0)
            throw new ArgumentException("Value cannot be empty.", paramName);
    }

    public static void ThrowIfNullOrWhiteSpace(
        [NotNull] string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(paramName);

        if (string.IsNullOrWhiteSpace(argument))
            throw new ArgumentException("Value cannot be whitespace.", paramName);
    }

    public static void ThrowIfGreaterThan<T>(
        T value,
        T other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) > 0)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be less than or equal to {other}.");
    }

    public static void ThrowIfGreaterThanOrEqual<T>(
        T value,
        T other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) >= 0)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be less than {other}.");
    }

    public static void ThrowIfLessThan<T>(
        T value,
        T other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) < 0)
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than or equal to {other}.");
    }

    public static void ThrowIfNegative<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
    }

    public static void ThrowIfNegativeOrZero<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
    }

    public static void ThrowIfZero<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) == 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-zero.");
    }

    public static void ThrowIfObjectDisposed(bool condition, object instance)
    {
        if (condition)
            throw new ObjectDisposedException(instance.GetType().Name);
    }
}
