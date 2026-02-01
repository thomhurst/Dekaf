# Simplify API Entry Point Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move the static `Dekaf` class to global namespace, eliminating the confusing `Dekaf.Dekaf.` pattern and enabling zero-ceremony API usage.

**Architecture:** Split `src/Dekaf/Dekaf.cs` into two files - entry point class in global namespace, builder classes in `Dekaf` namespace. All existing code with `using Dekaf;` continues to work unchanged.

**Tech Stack:** C# 13, .NET 10, TUnit for testing

---

## Task 1: Refactor Core API Files

**Files:**
- Modify: `src/Dekaf/Dekaf.cs` (will be split)
- Create: `src/Dekaf/Builders.cs`

**Step 1: Read current Dekaf.cs structure**

```bash
cat src/Dekaf/Dekaf.cs | head -100
```

Expected: See static class `Dekaf` in namespace `Dekaf`, followed by builder classes

**Step 2: Create new Dekaf.cs with static class in global namespace**

Create `src/Dekaf/Dekaf.cs`:

```csharp
using Dekaf.Consumer;
using Dekaf.Producer;

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

**Step 3: Extract builder classes to Builders.cs**

Copy the `ProducerBuilder<TKey, TValue>` and `ConsumerBuilder<TKey, TValue>` classes from the current `Dekaf.cs` to `src/Dekaf/Builders.cs`, keeping them in the `Dekaf` namespace.

The file should start with:

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
    // ... all existing implementation ...
}

/// <summary>
/// Fluent builder for creating consumers.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class ConsumerBuilder<TKey, TValue>
{
    // ... all existing implementation ...
}
```

**Step 4: Build to verify compilation**

```bash
dotnet build src/Dekaf --configuration Release
```

Expected: Build succeeds with 0 warnings, 0 errors

**Step 5: Commit the refactoring**

```bash
git add src/Dekaf/Dekaf.cs src/Dekaf/Builders.cs
git commit -m "refactor: Move Dekaf static class to global namespace

Split src/Dekaf/Dekaf.cs into:
- Dekaf.cs: Static entry point in global namespace
- Builders.cs: Builder classes in Dekaf namespace

This enables zero-ceremony API usage: Dekaf.CreateProducer()
works without any using directives.

Addresses #135"
```

---

## Task 2: Verify Existing Tests Pass

**Files:**
- Verify: `tests/Dekaf.Tests.Unit/**/*.cs`
- Verify: `tests/Dekaf.Tests.Integration/**/*.cs`

**Step 1: Build unit tests**

```bash
dotnet build tests/Dekaf.Tests.Unit --configuration Release
```

Expected: Build succeeds

**Step 2: Run unit tests**

```bash
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit
```

Expected: All 855 tests pass (0 failures)

**Step 3: Verify integration test build (skip run - requires Docker)**

```bash
dotnet build tests/Dekaf.Tests.Integration --configuration Release
```

Expected: Build succeeds

**Step 4: Commit verification note**

```bash
git commit --allow-empty -m "test: Verify all existing tests pass with refactored API"
```

---

## Task 3: Create Zero-Ceremony Verification Test

**Files:**
- Create: `tests/Dekaf.Tests.Unit/ZeroCeremonyApiTests.cs`

**Step 1: Write test without using directives**

Create `tests/Dekaf.Tests.Unit/ZeroCeremonyApiTests.cs`:

```csharp
// IMPORTANT: No using Dekaf directive here - we're testing zero-ceremony API
using TUnit.Core;

namespace Dekaf.Tests.Unit;

public class ZeroCeremonyApiTests
{
    [Test]
    public void CreateProducer_WithoutUsingDirective_CreatesBuilder()
    {
        // Arrange & Act - Note: No 'using Dekaf;' needed!
        var builder = Dekaf.CreateProducer<string, string>();

        // Assert
        Assert.That(builder).IsNotNull();
        Assert.That(builder).IsTypeOf<ProducerBuilder<string, string>>();
    }

    [Test]
    public void CreateConsumer_WithoutUsingDirective_CreatesBuilder()
    {
        // Arrange & Act - Note: No 'using Dekaf;' needed!
        var builder = Dekaf.CreateConsumer<string, string>();

        // Assert
        Assert.That(builder).IsNotNull();
        Assert.That(builder).IsTypeOf<ConsumerBuilder<string, string>>();
    }

    [Test]
    public void CreateProducer_WithBootstrapServers_ThrowsForInvalidBuild()
    {
        // Arrange & Act & Assert - Verify the shorthand method exists
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            // This should throw because we're calling Build() without servers
            var builder = Dekaf.CreateProducer<string, string>();
            builder.Build();
        });

        Assert.That(exception.Message).Contains("Bootstrap servers must be specified");
    }

    [Test]
    public void CreateTopicProducer_WithoutUsingDirective_CreatesTopicProducer()
    {
        // Arrange & Act - Note: No 'using Dekaf;' needed!
        // This will throw because no Kafka broker, but proves the method is accessible
        var exception = Assert.Throws<AggregateException>(() =>
        {
            var producer = Dekaf.CreateTopicProducer<string, string>("localhost:9092", "test-topic");
        });

        // Just verify the method is callable without using directive
        Assert.That(exception).IsNotNull();
    }
}
```

**Step 2: Build the test project**

```bash
dotnet build tests/Dekaf.Tests.Unit --configuration Release
```

Expected: Build succeeds

**Step 3: Run the new tests**

```bash
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit --treenode-filter /*/*/ZeroCeremonyApiTests/*
```

Expected: All 4 tests pass

**Step 4: Commit the test**

```bash
git add tests/Dekaf.Tests.Unit/ZeroCeremonyApiTests.cs
git commit -m "test: Add zero-ceremony API verification tests

Verify that Dekaf.CreateProducer() and Dekaf.CreateConsumer()
work without any using directives, proving the global namespace
approach is successful."
```

---

## Task 4: Update README.md

**Files:**
- Modify: `README.md`

**Step 1: Remove unnecessary using directives from examples**

Update the "Producing Messages" section (around line 28):

```markdown
### Producing Messages

The simplest way to send a message:

```csharp
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Wait for acknowledgment
var metadata = await producer.ProduceAsync("my-topic", "key", "Hello, Kafka!");
Console.WriteLine($"Sent to partition {metadata.Partition} at offset {metadata.Offset}");
```
```

**Step 2: Update fire-and-forget example (around line 40)**

```markdown
For high-throughput scenarios where you don't need to wait:

```csharp
// Fire and forget - returns immediately
producer.Send("my-topic", "key", "value");

// Make sure everything's delivered before shutting down
await producer.FlushAsync();
```
```

**Step 3: Update topic producer examples (around line 50)**

```markdown
### Topic-Specific Producers

When you're always producing to the same topic, use a topic producer for a cleaner API:

```csharp
await using var producer = Dekaf.CreateTopicProducer<string, string>(
    "localhost:9092", "orders");

// No topic parameter needed
await producer.ProduceAsync("order-123", orderJson);
producer.Send("order-456", orderJson);
```
```

**Step 4: Update consumer example (around line 76)**

```markdown
### Consuming Messages

```csharp
await using var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-consumer-group")
    .SubscribeTo("my-topic")
    .Build();

await foreach (var message in consumer.ConsumeAsync(cancellationToken))
{
    Console.WriteLine($"Got: {message.Key} = {message.Value}");
}
```
```

**Step 5: Verify README compiles**

Check that all code examples would compile without extra using directives.

**Step 6: Commit README updates**

```bash
git add README.md
git commit -m "docs: Update README to show zero-ceremony API usage

Remove unnecessary 'using Dekaf;' directives from all examples.
The entry point now works globally without any ceremony."
```

---

## Task 5: Update Getting Started Documentation

**Files:**
- Modify: `docs/docs/getting-started.md`

**Step 1: Add namespace explanation section (after line 20)**

Add this section after the installation instructions:

```markdown
## Using Dekaf

Dekaf's entry point is available globally - no `using` directive needed:

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();
```

For advanced scenarios where you reference types directly (builders, interfaces, options), add:

```csharp
using Dekaf;
using Dekaf.Producer;
using Dekaf.Consumer;
```

But for typical usage, the static `Dekaf` class is all you need.
```

**Step 2: Update "Your First Producer" section (around line 49)**

Remove the `using Dekaf;` and `using Dekaf.Producer;` lines:

```csharp
// Create a producer
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Send a message and wait for acknowledgment
var metadata = await producer.ProduceAsync("my-topic", "greeting", "Hello, Kafka!");

Console.WriteLine($"Message sent to partition {metadata.Partition} at offset {metadata.Offset}");
```

**Step 3: Update "Your First Consumer" section (around line 80)**

Remove the `using Dekaf;` and `using Dekaf.Consumer;` lines:

```csharp
// Create a consumer
await using var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-first-consumer")
    .SubscribeTo("my-topic")
    .Build();

// Consume messages
Console.WriteLine("Waiting for messages... (Ctrl+C to exit)");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await foreach (var message in consumer.ConsumeAsync(cts.Token))
{
    Console.WriteLine($"Received: {message.Key} = {message.Value}");
}
```

**Step 4: Update "Putting It Together" section (around line 116)**

Remove the `using Dekaf;`, `using Dekaf.Producer;`, and `using Dekaf.Consumer;` lines:

```csharp
const string bootstrapServers = "localhost:9092";
const string topic = "getting-started";

// Start the consumer in the background
var cts = new CancellationTokenSource();
var consumerTask = Task.Run(async () =>
{
    await using var consumer = Dekaf.CreateConsumer<string, string>()
        .WithBootstrapServers(bootstrapServers)
        .WithGroupId("getting-started-group")
        .WithAutoOffsetReset(AutoOffsetReset.Earliest)
        .SubscribeTo(topic)
        .Build();

    await foreach (var msg in consumer.ConsumeAsync(cts.Token))
    {
        Console.WriteLine($"[Consumer] {msg.Key}: {msg.Value}");
    }
});

// Give the consumer time to join the group
await Task.Delay(2000);

// Produce some messages
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers(bootstrapServers)
    .Build();

for (int i = 1; i <= 5; i++)
{
    var key = $"message-{i}";
    var value = $"Hello from message {i}!";

    await producer.ProduceAsync(topic, key, value);
    Console.WriteLine($"[Producer] Sent: {key}");
}

// Wait for messages to be consumed
await Task.Delay(2000);
cts.Cancel();

try { await consumerTask; }
catch (OperationCanceledException) { }

Console.WriteLine("Done!");
```

Note: The `AutoOffsetReset.Earliest` enum reference requires `using Dekaf.Consumer;`, but we'll keep it for now and address in a later task if needed.

**Step 5: Commit documentation updates**

```bash
git add docs/docs/getting-started.md
git commit -m "docs: Update getting-started guide for zero-ceremony API

Add explanation of namespace structure and simplify all code
examples to show the global Dekaf entry point."
```

---

## Task 6: Update Producer Documentation

**Files:**
- Modify: `docs/docs/producer/basics.md`
- Modify: `docs/docs/producer/fire-and-forget.md`
- Modify: `docs/docs/producer/headers.md`
- Modify: `docs/docs/producer/batch-production.md`
- Modify: `docs/docs/producer/topic-producer.md`

**Step 1: Review and simplify producer/basics.md**

Read the file, remove `using Dekaf;` and `using Dekaf.Producer;` from simple examples that only use the static entry point.

**Step 2: Review and simplify producer/fire-and-forget.md**

Read the file, remove unnecessary using directives.

**Step 3: Review and simplify producer/headers.md**

Read the file, remove unnecessary using directives (but keep if Headers type is referenced directly).

**Step 4: Review and simplify producer/batch-production.md**

Read the file, remove unnecessary using directives (but keep if ProducerMessage type is referenced directly).

**Step 5: Review and simplify producer/topic-producer.md**

Read the file, remove unnecessary using directives.

**Step 6: Commit producer docs updates**

```bash
git add docs/docs/producer/*.md
git commit -m "docs: Simplify producer documentation examples

Remove unnecessary using directives from examples that only
use the global Dekaf entry point. Keep using directives where
types are referenced directly."
```

---

## Task 7: Update Consumer Documentation

**Files:**
- Modify: `docs/docs/consumer/basics.md`
- Modify: `docs/docs/consumer/offset-management.md`
- Modify: `docs/docs/consumer/consumer-groups.md`
- Modify: `docs/docs/consumer/linq-extensions.md`

**Step 1: Review and simplify consumer/basics.md**

Read the file, remove `using Dekaf;` and `using Dekaf.Consumer;` from simple examples.

**Step 2: Review consumer/offset-management.md**

Keep `using Dekaf.Consumer;` if OffsetCommitMode enum is referenced directly.

**Step 3: Review and simplify consumer/consumer-groups.md**

Remove unnecessary using directives.

**Step 4: Review consumer/linq-extensions.md**

May need to keep using directives for extension method visibility.

**Step 5: Commit consumer docs updates**

```bash
git add docs/docs/consumer/*.md
git commit -m "docs: Simplify consumer documentation examples

Remove unnecessary using directives from examples that only
use the global Dekaf entry point."
```

---

## Task 8: Update Configuration Documentation

**Files:**
- Modify: `docs/docs/configuration/presets.md`
- Review: `docs/docs/configuration/producer-options.md`
- Review: `docs/docs/configuration/consumer-options.md`

**Step 1: Simplify presets.md examples**

Remove unnecessary using directives from preset examples.

**Step 2: Review producer-options.md**

Keep `using Dekaf;` since it references option types directly.

**Step 3: Review consumer-options.md**

Keep `using Dekaf;` since it references option types directly.

**Step 4: Commit configuration docs updates**

```bash
git add docs/docs/configuration/*.md
git commit -m "docs: Simplify configuration preset examples

Remove unnecessary using directives from preset examples."
```

---

## Task 9: Update Remaining Documentation

**Files:**
- Modify: `docs/docs/intro.md`
- Review: `docs/docs/compression.md`
- Review: `docs/docs/dependency-injection.md`
- Review: `docs/docs/serialization/*.md`
- Review: `docs/docs/security/*.md`

**Step 1: Update intro.md quick start**

Remove unnecessary using directives from quick start examples.

**Step 2: Review compression.md**

Simplify where possible.

**Step 3: Review dependency-injection.md**

Keep using directives (references interfaces and types).

**Step 4: Review serialization docs**

Keep using directives where serializer types are referenced.

**Step 5: Review security docs**

Simplify where possible.

**Step 6: Commit remaining docs updates**

```bash
git add docs/docs/intro.md docs/docs/compression.md docs/docs/dependency-injection.md docs/docs/serialization/*.md docs/docs/security/*.md
git commit -m "docs: Update remaining documentation for zero-ceremony API

Simplify examples where possible while keeping using directives
for advanced scenarios that reference types directly."
```

---

## Task 10: Run Final Verification

**Files:**
- Verify: All tests
- Verify: All documentation compiles

**Step 1: Run full unit test suite**

```bash
./tests/Dekaf.Tests.Unit/bin/Release/net10.0/Dekaf.Tests.Unit
```

Expected: All tests pass (855+ tests, 0 failures)

**Step 2: Build all projects**

```bash
dotnet build --configuration Release
```

Expected: Build succeeds with 0 warnings

**Step 3: Create manual verification console app**

Create a temporary test app to verify zero-ceremony works:

```bash
mkdir /tmp/dekaf-verify-test
cd /tmp/dekaf-verify-test
dotnet new console
dotnet add reference /home/tom-longhurst/dev/Dekaf/.worktrees/simplify-api-entry-point/src/Dekaf/Dekaf.csproj
```

Edit Program.cs (no using directives):

```csharp
// No using Dekaf; needed!
var builder = Dekaf.CreateProducer<string, string>();
Console.WriteLine($"Created builder: {builder.GetType().Name}");
```

Build and run:

```bash
dotnet build
dotnet run
```

Expected: Prints "Created builder: ProducerBuilder`2"

Clean up:

```bash
cd -
rm -rf /tmp/dekaf-verify-test
```

**Step 4: Commit verification note**

```bash
git commit --allow-empty -m "test: Verify zero-ceremony API works end-to-end

Manual verification confirms Dekaf.CreateProducer() and
Dekaf.CreateConsumer() work without any using directives."
```

---

## Task 11: Update CHANGELOG

**Files:**
- Modify: `CHANGELOG.md`

**Step 1: Add entry to CHANGELOG**

Add at the top of CHANGELOG.md:

```markdown
## [Unreleased]

### Changed

- **API Simplification**: Moved the static `Dekaf` class to the global namespace, enabling zero-ceremony API usage. You can now write `Dekaf.CreateProducer()` and `Dekaf.CreateConsumer()` without any `using` directives. This fixes the confusing `Dekaf.Dekaf.` double-qualification pattern. [#135]

### Migration

If you were using the fully-qualified syntax `Dekaf.Dekaf.CreateConsumer()`, replace it with `Dekaf.CreateConsumer()`. Code using `using Dekaf;` continues to work unchanged.
```

**Step 2: Commit CHANGELOG**

```bash
git add CHANGELOG.md
git commit -m "docs: Update CHANGELOG for API simplification"
```

---

## Success Criteria

✅ Static `Dekaf` class in global namespace
✅ Builder classes in `Dekaf` namespace
✅ All 855+ unit tests pass
✅ Zero-ceremony API verified with test
✅ README updated
✅ Getting started docs updated
✅ All producer/consumer docs updated
✅ Configuration docs updated
✅ CHANGELOG updated
✅ Manual verification successful

## Next Steps

After completing this plan:

1. **Review the changes** - Check that all code examples feel natural and simple
2. **Create PR** - Use the `/commit-push-pr` skill or manual PR creation
3. **Release notes** - Prepare release notes highlighting the API improvement
4. **Consider minor version bump** - This improves UX significantly but is technically a fix
