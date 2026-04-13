# KIP-890: Transactions V2 (Server-Side Fencing) — Design

**Issue:** #818
**KIP:** https://cwiki.apache.org/confluence/display/KAFKA/KIP-890%3A+Transactions+Server-Side+Defense
**Date:** 2026-04-13

## Overview

Implement KIP-890 Transactions V2, which introduces server-side fencing for transactional producers. Kafka 4.0+ brokers support TV2 natively, enabling more efficient transactional workflows: implicit partition registration, epoch bumps via EndTxn response, and elimination of the post-abort InitProducerId round-trip.

## 1. TV2 Capability Detection

**MetadataManager** gains finalized feature storage. `NegotiateApiVersionsAsync` already parses `FinalizedFeatures` from `ApiVersionsResponse` but discards them. Store them:

- New field: `private volatile IReadOnlyList<FinalizedFeature>? _finalizedFeatures`
- New method: `internal short GetFinalizedFeatureVersion(string featureName)` — iterates `_finalizedFeatures`, returns `MaxVersionLevel` for matching name, or `0` if absent
- In `NegotiateApiVersionsAsync`, after existing API version processing: `_finalizedFeatures = response.FinalizedFeatures`

**KafkaProducer** latches per-transaction. In `BeginTransaction()`:

- New field: `internal bool _currentTransactionUsesTV2`
- Set at transaction start: `_currentTransactionUsesTV2 = _metadataManager.GetFinalizedFeatureVersion("transaction.version") >= 2`
- Never changes mid-transaction — all downstream logic checks this single field

## 2. EndTxn v4 Response — Epoch Bump

**EndTxnResponse** currently only reads `ThrottleTimeMs` and `ErrorCode`, skipping tagged fields. In v4, the broker returns bumped `ProducerId` and `ProducerEpoch` as tagged fields (tags 0 and 1).

Changes:
- Add `public long ProducerId { get; init; } = -1` and `public short ProducerEpoch { get; init; } = -1` to `EndTxnResponse`
- Replace `reader.SkipTaggedFields()` with explicit tag parsing: tag 0 → `ReadInt64()` (ProducerId), tag 1 → `ReadInt16()` (ProducerEpoch), default → `Skip(size)`

**KafkaProducer.EndTransactionAsync** uses these values. On success, if TV2 is active and the response contains a valid ProducerId (`>= 0`):
- Update `_producerId` and `_producerEpoch`
- Update `_accumulator.ProducerId` and `_accumulator.ProducerEpoch`
- Reset sequence numbers via `_accumulator.ResetSequenceNumbers()`

## 3. Abort Flow Change

Currently `AbortAsync` calls `EndTransactionAsync` then `ReinitializeProducerIdAsync` (network round-trip). In TV2, `EndTransactionAsync` already applied the bumped epoch from the response, so the re-init is unnecessary.

In `Transaction.AbortAsync`:
- After `EndTransactionAsync(committed: false)`, check `_currentTransactionUsesTV2`
- TV1: call `ReinitializeProducerIdAsync` (existing behavior)
- TV2: skip — epoch already updated

This is the main latency win: abort-to-next-transaction eliminates one network round-trip.

Commit flow is structurally unchanged — it already doesn't call `ReinitializeProducerIdAsync`. But `EndTransactionAsync` will now apply the bumped epoch from v4 on commit too.

## 4. Implicit AddPartitionsToTxn

~~Originally planned to skip `AddPartitionsToTxn` in TV2 based on the assumption that the broker registers partitions implicitly. This was incorrect — Kafka 4.0+ still requires explicit `AddPartitionsToTxn` before the Produce request. Omitting it causes `NotMemberOfTenant` errors.~~

**No change:** `AddPartitionsToTxn` is always sent regardless of TV2. KIP-890 only changes epoch management and error classification, not partition registration.

## 5. Error Classification

New `TransactionErrorClassification` enum: `Retriable`, `Abortable`, `Fatal`.

New `TransactionErrorClassifier.Classify(ErrorCode, bool tv2)` static method:
- **Fatal (always):** `ProducerFenced`, `TransactionalIdAuthorizationFailed`, `TransactionCoordinatorFenced`
- **Fatal (TV2 only) / Abortable (TV1):** `InvalidProducerIdMapping` (error code 49)
- **Retriable:** `CoordinatorLoadInProgress`, `CoordinatorNotAvailable`, `NotCoordinator`, `ConcurrentTransactions`
- **Default:** `Abortable` (unknown errors abort the transaction)

Refactor error handling in `EndTransactionAsync`, `AddPartitionsToTransactionAsync`, and `ReinitializeProducerIdAsync` to use `Classify()` instead of inline `if` chains. Existing `IsRetriable()` extension on `ErrorCode` stays unchanged for non-transactional use.

## 6. Files to Change

| File | Change |
|------|--------|
| `src/Dekaf/Metadata/MetadataManager.cs` | Store `_finalizedFeatures`, add `GetFinalizedFeatureVersion()` |
| `src/Dekaf/Protocol/Messages/EndTxnResponse.cs` | Parse tagged fields for ProducerId/ProducerEpoch in v4 |
| `src/Dekaf/Producer/KafkaProducer.cs` | Add `_currentTransactionUsesTV2` field, latch in `BeginTransaction()`, use in `EndTransactionAsync()` |
| `src/Dekaf/Producer/KafkaProducer.cs` (Transaction inner class) | Conditional `ReinitializeProducerIdAsync` skip in `AbortAsync()` |
| `src/Dekaf/Producer/TransactionErrorClassifier.cs` | New file: `TransactionErrorClassification` enum + `Classify()` method |
| `src/Dekaf/Producer/KafkaProducer.cs` | Refactor error handling to use `TransactionErrorClassifier` |

## 7. Testing

**Unit tests:**
- EndTxn v4 response parsing: verify ProducerId/ProducerEpoch from tagged fields, v3 backward compatibility (defaults to -1)
- `TransactionErrorClassifier.Classify`: all error codes in TV1 and TV2, especially `InvalidProducerIdMapping` classification change
- `MetadataManager.GetFinalizedFeatureVersion`: feature present, absent, version thresholds

**Integration tests (Kafka 4.0+ via Testcontainers):**
- Produce → commit → verify epoch bump → produce again in new transaction
- Produce → abort → verify no InitProducerId call → produce again
- V1 fallback: verify abort still calls InitProducerId when `transaction.version < 2`

**Protocol tests:**
- EndTxn v4 round-trip serialization with tagged fields
- Existing v3 tests remain unchanged
