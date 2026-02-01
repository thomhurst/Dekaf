# Simplifying the Dekaf API Entry Point

**Date:** 2026-02-01
**Status:** Proposed
**Issue:** [#135](https://github.com/thomhurst/Dekaf/issues/135)

## Summary

Move the static `Dekaf` class to the global namespace to eliminate the confusing `Dekaf.Dekaf.` double-qualification pattern and provide a zero-ceremony API entry point.

## Problem Statement

Users encounter a confusing namespace collision. The static class `Dekaf` exists inside the `Dekaf` namespace, creating two valid but confusing usage patterns:

```csharp
// With using directive - works, but requires ceremony
using Dekaf;
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Without using directive - awkward and confusing
var consumer = Dekaf.Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();
```

The double-Dekaf syntax feels like a mistake, and users report it as a documentation issue when it's actually an API design issue. This creates a poor first impression and violates Dekaf's goal of being simple and intuitive.

## Design Goals

1. **Zero Ceremony** - Users should be able to write `Dekaf.CreateConsumer()` anywhere without using directives
2. **Simplicity** - The entry point should feel natural, like `Task.Run()` or `Console.WriteLine()`
3. **Modern C#** - Still follow .NET conventions for everything beyond the entry point
4. **Minimal Breaking Changes** - Preserve compatibility where possible

## Proposed Solution

### Architecture

Move the static `Dekaf` class to the global namespace (no namespace wrapper). All other types remain in the `Dekaf` namespace and its sub-namespaces.

**Structure:**
- **Global namespace**: `Dekaf` static class (entry point)
- **`Dekaf` namespace**: `ProducerBuilder<T>`, `ConsumerBuilder<T>`, interfaces, options
- **`Dekaf.Producer` namespace**: Producer-specific types
- **`Dekaf.Consumer` namespace**: Consumer-specific types
- **Other namespaces**: Unchanged

### User Experience

**Simple scenarios (95% of usage):**

```csharp
// No using directives needed
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .Build();
```

**Advanced scenarios (direct type references):**

```csharp
using Dekaf;
using Dekaf.Producer;
using Dekaf.Consumer;

// When you need to reference types directly
public class MyService
{
    private readonly IKafkaProducer<string, string> _producer;

    public MyService(IKafkaProducer<string, string> producer)
    {
        _producer = producer;
    }
}
```

## Implementation Details

### File Changes

**1. Split `src/Dekaf/Dekaf.cs` into two files:**

**`src/Dekaf/Dekaf.cs`** - Static entry point (global namespace):

```csharp
// No namespace declaration - this is in the global namespace

/// <summary>
/// Main entry point for creating Kafka clients.
/// </summary>
public static class Dekaf
{
    /// <summary>
    /// Creates a producer builder.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> CreateProducer<TKey, TValue>()
    {
        return new ProducerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a producer with the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    public static IKafkaProducer<TKey, TValue> CreateProducer<TKey, TValue>(string bootstrapServers)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .Build();
    }

    /// <summary>
    /// Creates a topic-specific producer with the specified bootstrap servers and topic.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <returns>A producer bound to the specified topic.</returns>
    public static ITopicProducer<TKey, TValue> CreateTopicProducer<TKey, TValue>(string bootstrapServers, string topic)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .BuildForTopic(topic);
    }

    /// <summary>
    /// Creates a consumer builder.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        return new ConsumerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a consumer with the specified bootstrap servers and group ID.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="groupId">The consumer group ID.</param>
    public static IKafkaConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string bootstrapServers, string groupId)
    {
        return new ConsumerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(groupId)
            .Build();
    }
}
```

**`src/Dekaf/Builders.cs`** - Builder classes (in `Dekaf` namespace):

```csharp
using System.Security.Cryptography.X509Certificates;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.Serialization;

namespace Dekaf;

/// <summary>
/// Fluent builder for creating producers.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class ProducerBuilder<TKey, TValue>
{
    // ... existing ProducerBuilder implementation ...
}

/// <summary>
/// Fluent builder for creating consumers.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class ConsumerBuilder<TKey, TValue>
{
    // ... existing ConsumerBuilder implementation ...
}
```

### Namespace Resolution

C# handles type/namespace name collisions by giving types precedence:

- `Dekaf.CreateConsumer()` → Resolves to global `Dekaf` static class ✅
- `using Dekaf;` → Imports the `Dekaf` namespace ✅
- Inside a file with `using Dekaf;`, you can reference both:
  - `Dekaf.CreateConsumer()` → Global static class
  - `ProducerBuilder<T>` → Type from namespace

This is well-defined behavior in the C# language specification and works correctly.

## Breaking Changes

**What breaks:**
- Code using fully-qualified `Dekaf.Dekaf.CreateConsumer()` will no longer compile

**What continues to work:**
- Code with `using Dekaf;` and `Dekaf.CreateConsumer()` ✅
- All builder patterns, interfaces, and other types ✅
- Existing serializers, compression codecs, extensions ✅

**Migration:**
- Simple find-replace: `Dekaf.Dekaf.` → `Dekaf.`
- This syntax was the confusing pattern we're fixing

**Classification:**
This is treated as a **bug fix**, not a breaking change, because:
1. The double-Dekaf syntax is the reported issue
2. The "correct" usage (with `using Dekaf;`) continues to work
3. Anyone using `Dekaf.Dekaf.` was working around poor API design

## Documentation Updates

### README.md

Update all examples to emphasize zero-ceremony usage:

```markdown
## Getting Started

```bash
dotnet add package Dekaf
```

### Producing Messages

The simplest way to send a message:

```csharp
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

var metadata = await producer.ProduceAsync("my-topic", "key", "Hello, Kafka!");
```

No `using` directives needed - just `Dekaf.CreateProducer()` and go.
```

### Docusaurus Documentation

**Files to update:**

1. **`docs/docs/getting-started.md`**
   - Add section explaining namespace structure
   - Show that `using Dekaf;` is optional for simple scenarios
   - Keep examples clean and minimal

2. **`docs/docs/intro.md`**
   - Update quick start to show zero-ceremony approach
   - Remove unnecessary `using` directives

3. **Producer documentation** (`docs/docs/producer/*.md`):
   - `basics.md` - Simplify all examples
   - `fire-and-forget.md` - Remove `using Dekaf;` from basic examples
   - `headers.md` - Review and simplify
   - `batch-production.md` - Review and simplify
   - `topic-producer.md` - Review and simplify
   - `transactions.md` - Keep `using` if needed for transaction types
   - `partitioning.md` - Review and simplify

4. **Consumer documentation** (`docs/docs/consumer/*.md`):
   - `basics.md` - Simplify all examples
   - `offset-management.md` - Keep `using` if referencing enum types directly
   - `consumer-groups.md` - Review and simplify
   - `linq-extensions.md` - Review (may need `using` for extension methods)

5. **Configuration documentation** (`docs/docs/configuration/*.md`):
   - `presets.md` - Simplify examples
   - `producer-options.md` - Keep `using Dekaf;` (references option types)
   - `consumer-options.md` - Keep `using Dekaf;` (references option types)

6. **Other documentation**:
   - `dependency-injection.md` - Keep `using Dekaf;` (references interfaces and types)
   - `serialization/json.md` - Keep `using` for serializer types
   - `serialization/schema-registry.md` - Keep `using` for serializer types
   - `compression.md` - Review and simplify
   - `security/*.md` - Review each file

**Documentation principle:** Remove `using Dekaf;` from examples unless they directly reference types (interfaces, options, enums). The entry point should feel effortless.

## Testing Strategy

1. **Unit tests** - Verify builder construction works
2. **Integration tests** - Ensure all existing tests pass without modification (since they use `using Dekaf;`)
3. **Documentation tests** - Build all documentation examples to ensure they compile
4. **Manual testing** - Create a new console app without any using directives and verify the API works

## Alternatives Considered

### Alternative 1: Document the global using pattern
Keep current structure, document that users should add:
```csharp
global using static Dekaf.Dekaf;
```

**Rejected because:** Still requires ceremony, users shouldn't need to know this trick.

### Alternative 2: Rename the static class
Rename to `Kafka`, `DekafClient`, or similar.

**Rejected because:** Loses clean branding, `Dekaf.CreateConsumer()` is the ideal API.

### Alternative 3: Different namespace
Put static class in namespace like `Dk` or `DekafClient`.

**Rejected because:** Still requires using directives, less clear branding.

## Success Criteria

1. Users can write `Dekaf.CreateConsumer()` without any using directives ✅
2. Existing code with `using Dekaf;` continues to work ✅
3. All documentation examples are simplified and consistent ✅
4. All tests pass without modification ✅
5. The API feels natural and intuitive ✅

## Implementation Checklist

- [ ] Split `src/Dekaf/Dekaf.cs` into `Dekaf.cs` (global) and `Builders.cs` (namespace)
- [ ] Update all XML documentation comments
- [ ] Run all unit tests
- [ ] Run all integration tests
- [ ] Update `README.md`
- [ ] Update `docs/docs/getting-started.md`
- [ ] Update `docs/docs/intro.md`
- [ ] Update all producer documentation
- [ ] Update all consumer documentation
- [ ] Update all configuration documentation
- [ ] Update security documentation
- [ ] Update serialization documentation
- [ ] Create a simple test console app to verify zero-ceremony works
- [ ] Update CHANGELOG.md
- [ ] Prepare release notes

## Future Considerations

This change sets up Dekaf for excellent discoverability:
- IntelliSense will show `Dekaf.` as soon as users type it
- No cognitive overhead about namespaces for getting started
- Advanced users still have full control with explicit using directives

Consider similar patterns for future API additions - keep the entry point simple and in global namespace.
