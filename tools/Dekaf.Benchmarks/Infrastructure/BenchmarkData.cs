namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Shared benchmark input builders.
/// </summary>
public static class BenchmarkData
{
    /// <summary>
    /// Pre-creates "key-{i}" strings. Call from <c>[GlobalSetup]</c> or a static field
    /// initializer so the Allocated column reflects the code under test, not the
    /// benchmark's own key-string interpolation.
    /// </summary>
    public static string[] CreateKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
            keys[i] = $"key-{i}";
        return keys;
    }
}
