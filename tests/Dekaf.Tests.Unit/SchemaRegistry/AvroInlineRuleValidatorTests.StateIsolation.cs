using System.Runtime.CompilerServices;
using Avro;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public partial class AvroInlineRuleValidatorTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Validate_AlternatingSchemasPreservesWarmedState(bool wideFirst)
    {
        var narrow = CreateNarrowValidationCase();
        var wide = CreateWideValidationCase();
        var first = wideFirst ? wide : narrow;
        var second = wideFirst ? narrow : wide;

        first.Validator.Validate(first.Valid, 42, failFast: false);
        second.Validator.Validate(second.Valid, 42, failFast: false);
        var allocated = MeasureAlternatingValidation(first, second);

        var narrowFailure = Assert.Throws<ValidationRulesFailedException>(() =>
            narrow.Validator.Validate(narrow.Invalid, 42, failFast: false));
        var wideFailure = Assert.Throws<ValidationRulesFailedException>(() =>
            wide.Validator.Validate(wide.Invalid, 42, failFast: false));

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(narrowFailure.Violations).HasSingleItem();
        await Assert.That(narrowFailure.Violations[0].Rule.Name).IsEqualTo("narrow-positive");
        await Assert.That(narrowFailure.Violations[0].FieldPath).IsEqualTo("$.items[1]");
        await Assert.That(wideFailure.Violations).HasSingleItem();
        await Assert.That(wideFailure.Violations[0].Rule.Name).IsEqualTo("wide-positive");
        await Assert.That(wideFailure.Violations[0].FieldPath).IsEqualTo("$.child.items[2]");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Validate_NestedFailureRestoresWarmedState(bool malformed, bool failFast)
    {
        var scenario = CreateWideValidationCase();
        // Remove the final array terminator so decoding fails inside nested traversal.
        var truncated = scenario.Valid[..^1];
        var initialDepth = CompiledValidationRule.ValueResolutionDepth;
        scenario.Validator.Validate(scenario.Valid, 42, failFast);

        Exception failure = malformed
            ? Assert.Throws<SchemaRegistryRuleException>(() =>
                scenario.Validator.Validate(truncated, 42, failFast))
            : Assert.Throws<ValidationRulesFailedException>(() =>
                scenario.Validator.Validate(scenario.Invalid, 42, failFast));
        var depthAfterFailure = CompiledValidationRule.ValueResolutionDepth;
        // Do not warm again after the failure: the first successful call must reuse state.
        var allocated = MeasureValidValidation(scenario.Validator, scenario.Valid, failFast);
        var depthAfterSuccess = CompiledValidationRule.ValueResolutionDepth;
        var subsequentFailure = Assert.Throws<ValidationRulesFailedException>(() =>
            scenario.Validator.Validate(scenario.Invalid, 42, failFast));

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(depthAfterFailure).IsEqualTo(initialDepth);
        await Assert.That(depthAfterSuccess).IsEqualTo(initialDepth);
        if (malformed)
            await Assert.That(failure.Message).Contains("Could not evaluate Avro validation rules");
        else
        {
            var violation = (ValidationRulesFailedException)failure;
            await Assert.That(violation.Violations).HasSingleItem();
            await Assert.That(violation.Violations[0].Rule.Name).IsEqualTo("wide-positive");
            await Assert.That(violation.Violations[0].FieldPath).IsEqualTo("$.child.items[2]");
        }
        await Assert.That(subsequentFailure.Violations).HasSingleItem();
        await Assert.That(subsequentFailure.Violations[0].Rule.Name).IsEqualTo("wide-positive");
        await Assert.That(subsequentFailure.Violations[0].FieldPath).IsEqualTo("$.child.items[2]");
    }

    [Test]
    public async Task Validate_SharedValidatorKeepsWarmedStateOnEachThread()
    {
        const int workerCount = 4;
        var schema = CreateWideValidationSchema();
        var validator = new AvroInlineRuleValidator(schema);
        var payloads = new (byte[] Valid, byte[] Invalid)[workerCount];
        for (var index = 0; index < workerCount; index++)
            payloads[index] = CreateWideValidationPayloads(schema, index + 1);

        using var start = new Barrier(workerCount);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var workers = new Task<ValidationWorkerResult>[workerCount];
        for (var index = 0; index < workerCount; index++)
        {
            var payload = payloads[index];
            // Dedicated synchronous workers keep warmup and measurement on distinct threads.
            workers[index] = Task.Factory.StartNew(() =>
            {
                validator.Validate(payload.Valid, 42, failFast: false);
                start.SignalAndWait(watchdog.Token);
                var allocated = MeasureValidValidation(validator, payload.Valid, failFast: false);
                var failure = Assert.Throws<ValidationRulesFailedException>(() =>
                    validator.Validate(payload.Invalid, 42, failFast: false));
                return new ValidationWorkerResult(Environment.CurrentManagedThreadId, allocated, failure);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        var results = await Task.WhenAll(workers);
        for (var index = 0; index < results.Length; index++)
        {
            await Assert.That(results[index].Allocated).IsEqualTo(0);
            await Assert.That(results[index].Failure.Violations).HasSingleItem();
            await Assert.That(results[index].Failure.Violations[0].Rule.Name).IsEqualTo("wide-positive");
            await Assert.That(results[index].Failure.Violations[0].FieldPath)
                .IsEqualTo($"$.child.items[{index}]");
            for (var previous = 0; previous < index; previous++)
                await Assert.That(results[index].ThreadId).IsNotEqualTo(results[previous].ThreadId);
        }
    }

    // Keep assertions and exception-checking delegates outside the allocation counters.
    // Each caller performs exactly one warmup per schema on the measuring thread.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureAlternatingValidation(ValidationCase first, ValidationCase second)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            first.Validator.Validate(first.Valid, 42, failFast: false);
            second.Validator.Validate(second.Valid, 42, failFast: false);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureValidValidation(AvroInlineRuleValidator validator, byte[] payload, bool failFast)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, 42, failFast);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static ValidationCase CreateNarrowValidationCase()
    {
        var schema = (RecordSchema)global::Avro.Schema.Parse("""
            {
              "type": "record", "name": "NarrowStateRecord",
              "confluent:rules": [{ "name": "narrow-root", "expr": "this.marker == 7 && size(this.items) == 2" }],
              "fields": [
                { "name": "marker", "type": "int" },
                { "name": "items", "type": { "type": "array", "items": {
                  "type": "int", "confluent:rules": [{ "name": "narrow-positive", "expr": "this > 0" }]
                } } }
              ]
            }
            """);
        var record = new GenericRecord(schema);
        var items = new[] { 8, 9 };
        record.Add("marker", 7);
        record.Add("items", items);
        var valid = Serialize(record, schema);
        items[^1] = -1;
        return new ValidationCase(new AvroInlineRuleValidator(schema), valid, Serialize(record, schema));
    }

    private static ValidationCase CreateWideValidationCase()
    {
        var schema = CreateWideValidationSchema();
        var payloads = CreateWideValidationPayloads(schema, 3);
        return new ValidationCase(new AvroInlineRuleValidator(schema), payloads.Valid, payloads.Invalid);
    }

    private static RecordSchema CreateWideValidationSchema() => (RecordSchema)global::Avro.Schema.Parse("""
        {
          "type": "record", "name": "WideStateRecord",
          "confluent:rules": [{ "name": "wide-root", "expr": "size(this) == 10 && this.a == 10 && this.b == 11 && this.c == 12 && this.d == 13 && this.e == 14 && this.f == 15 && this.g == 16 && this.h == 17 && this.marker == this.child.value && size(this.child.items) == this.marker" }],
          "fields": [
            { "name": "marker", "type": "int" },
            { "name": "a", "type": "int" }, { "name": "b", "type": "int" },
            { "name": "c", "type": "int" }, { "name": "d", "type": "int" },
            { "name": "e", "type": "int" }, { "name": "f", "type": "int" },
            { "name": "g", "type": "int" }, { "name": "h", "type": "int" },
            { "name": "child", "type": {
              "type": "record", "name": "WideStateChild",
              "confluent:rules": [{ "name": "wide-child", "expr": "this.value > 0 && size(this.items) == this.value" }],
              "fields": [
                { "name": "value", "type": "int" },
                { "name": "items", "type": { "type": "array", "items": {
                  "type": "int", "confluent:rules": [{ "name": "wide-positive", "expr": "this > 0" }]
                } } }
              ]
            } }
          ]
        }
        """);

    private static (byte[] Valid, byte[] Invalid) CreateWideValidationPayloads(RecordSchema schema, int itemCount)
    {
        var childSchema = (RecordSchema)schema.Fields[9].Schema;
        var items = new int[itemCount];
        for (var index = 0; index < items.Length; index++)
            items[index] = itemCount * 10 + index;
        var child = new GenericRecord(childSchema);
        child.Add("value", itemCount);
        child.Add("items", items);
        var record = new GenericRecord(schema);
        record.Add("marker", itemCount);
        for (var index = 0; index < 8; index++)
            record.Add(schema.Fields[index + 1].Name, index + 10);
        record.Add("child", child);
        var valid = Serialize(record, schema);
        items[^1] = -1;
        return (valid, Serialize(record, schema));
    }

    private readonly record struct ValidationCase(AvroInlineRuleValidator Validator, byte[] Valid, byte[] Invalid);
    private readonly record struct ValidationWorkerResult(int ThreadId, long Allocated, ValidationRulesFailedException Failure);
}
