namespace System.Runtime.CompilerServices;

/// <summary>
/// Opts one async method out of Runtime Async on net11.0 so it keeps its compiler-generated state
/// machine. The compiler matches this attribute by name; .NET 11 does not expose it publicly.
/// </summary>
/// <remarks>
/// Runtime Async allocates continuations on every suspension and ignores
/// <see cref="AsyncMethodBuilderAttribute"/>, so pooled builders and long-lived loops that
/// suspend once per message or request allocate where a state machine box is reused. Apply
/// <c>[RuntimeAsyncMethodGeneration(false)]</c> to such methods.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RuntimeAsyncMethodGenerationAttribute(bool runtimeAsync) : Attribute
{
    public bool RuntimeAsync { get; } = runtimeAsync;
}
