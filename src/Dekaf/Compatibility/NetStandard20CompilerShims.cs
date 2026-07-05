#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;

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
}
#endif
