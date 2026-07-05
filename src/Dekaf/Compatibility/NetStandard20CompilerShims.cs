#if NETSTANDARD2_0
namespace System
{
    public readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _value = fromEnd ? ~value : value;
        }

        private Index(int value)
        {
            _value = value;
        }

        public static Index Start => new(0);

        public static Index End => new(~0);

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length)
        {
            var offset = IsFromEnd ? length - Value : Value;
            if ((uint)offset > (uint)length)
                throw new ArgumentOutOfRangeException(nameof(length));

            return offset;
        }

        public static implicit operator Index(int value) => new(value);
    }

    public readonly struct Range
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }

        public Index End { get; }

        public static Range All => new(Index.Start, Index.End);

        public static Range StartAt(Index start) => new(start, Index.End);

        public static Range EndAt(Index end) => new(Index.Start, end);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            var start = Start.GetOffset(length);
            var end = End.GetOffset(length);
            if (end < start)
                throw new ArgumentOutOfRangeException(nameof(length));

            return (start, end - start);
        }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute;
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }

    [AttributeUsage(
        AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Field
        | AttributeTargets.Property,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute;

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public const string RefStructs = nameof(RefStructs);
        public const string RequiredMembers = nameof(RequiredMembers);

        public string FeatureName { get; } = featureName;
        public bool IsOptional { get; set; }
    }

    [AttributeUsage(
        AttributeTargets.Module
        | AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Constructor
        | AttributeTargets.Method
        | AttributeTargets.Property,
        Inherited = false)]
    internal sealed class SkipLocalsInitAttribute : Attribute;

    internal static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(array.Length);
            var result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}
#endif
