#if NET8_0
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, Inherited = false)]
internal sealed class OverloadResolutionPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}
#endif
