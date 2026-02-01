# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **API Simplification**: Introduced global `Kafka` class for zero-ceremony API usage. You can now write `Kafka.CreateProducer()` and `Kafka.CreateConsumer()` without any `using` directives. This replaces the previous `Dekaf.Dekaf.CreateX()` pattern. [#135]

### Migration

Existing code using `using Dekaf;` continues to work unchanged. If you were using the fully-qualified syntax `Dekaf.Dekaf.CreateConsumer()`, replace it with `Kafka.CreateConsumer()`.
