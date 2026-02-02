# Test Baseline for Deadlock Prevention Fixes

## Baseline Failures (Pre-existing, unrelated to deadlock fixes)

These 7 tests were failing before our changes:

1. TryAppendFireAndForgetBatch_SingleItem_AppendsSuccessfully
2. BufferMemory_TracksCorrectly_AcrossMultipleOperations
3. TryAppendFireAndForgetBatch_MultipleItems_AppendsAll
4. TryAppendFireAndForgetBatch_WithPooledMemory_TracksArrays
5. BufferMemory_FireAndForget_ReleasesOnBatchRelease
6. Deserializer_WarmupAsync_PreCachesSchema
7. ChainStatus_IncludesUntrustedRootForSelfSigned

Issues:
- 5 BufferMemory accounting bugs (negative values during disposal)
- 1 Schema registry test (value mismatch: expected 99 but got 42)
- 1 Certificate test (ASN.1 encoding: argument exception in WriteIntegerCore)

## Expected After Fixes

Same 7 failures (these are pre-existing bugs)
Zero new failures from deadlock prevention changes

## Test Results

```
Test run summary: Failed! - Dekaf.Tests.Unit.dll (net10.0|x64)
  total: 860
  failed: 7
  succeeded: 853
  skipped: 0
  duration: 8s 901ms
```
