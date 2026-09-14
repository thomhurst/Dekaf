---
sidebar_position: 6
description: "Kafka feature and KIP support, broker capability requirements, runtime limits, and validation evidence."
---

# Kafka feature support

This matrix describes Dekaf's client-facing scope. It is not a checklist of every
Java client, Kafka Streams, Connect, or broker-only KIP. A wire codec alone does
not establish a working client feature. Read [API and Runtime Compatibility](api-compatibility.md)
for package assets and configured broker coverage.

Last reviewed: **2026-09-14**. Broker API versions and finalized feature levels
are the capability requirements; an accepted KIP or a version advertised in
`ApiVersions` does not prove that every broker behavior is implemented.

## Status and evidence

- **Complete**: the client behavior named in the row is implemented. This does
  not claim every part of the linked KIP or every Java API is supported.
- **Partial**: usable support exists, with a specific remaining limitation.
- **Excluded / deferred**: intentionally outside the current client scope.
- **Experimental**: code exists, but supporting broker behavior or complete
  real-broker acceptance is not established.

Evidence is labeled separately: **Source** means implementation/tests were
inspected; **Configured CI** means the linked tests are selected by maintained
workflows, subject to their capability/platform skips. Neither label claims a
successful execution. **Run** requires a direct successful run/artifact link,
commit SHA, runtime, broker image, and relevant test outcomes. This page makes no
Run claim; consult linked issues and PRs for historical results.

Unless a row says otherwise, runtime coverage follows the core's `net10.0` and
`net8.0` assets, with configured .NET 10 and .NET 8 tests respectively.
The separate `netstandard2.0` asset retains its documented compatibility limits.
No additional OS restriction is known for those managed protocol paths; CI
coverage is Linux, not a certification of every OS. Historical protocol minima
below do not extend Dekaf's tested broker range to older Kafka releases.

## Consumer coordination and fetching

| Feature / authoritative KIP | Status and scope | Broker requirement | Implementation / documentation | Evidence and remaining work |
| --- | --- | --- | --- | --- |
| [KIP-848][kip848] consumer groups | Complete: server-side assignment, membership, reconciliation and heartbeats | Kafka 4.0+; `ConsumerGroupHeartbeat` v0+, enabled consumer protocol | [Consumer groups](consumer/consumer-groups.md), [coordinator][src/Consumer/ConsumerCoordinator.cs] | Configured CI: [coordination unit tests][unit/Consumer/ConsumerCoordinatorKip848Tests.cs], [group integration tests][integration/ConsumerGroupTests.cs]. Client-side assignors are outside this row. |
| Classic group participation (pre-[KIP-848][kip848]) | Excluded: no `JoinGroup`/`SyncGroup` consumer mode | Not applicable; use modern consumer groups on Kafka 4.0+ | [Protocol decision](consumer/consumer-groups.md#classic-protocol-support-decision) | Source: [coordinator tests][unit/Consumer/ConsumerCoordinatorKip848Tests.cs]; intentional decision [#2747][issue2747]. Admin inspection of classic groups does not imply participation. |
| [KIP-227][kip227] incremental fetch sessions | Complete: session establishment, incremental updates and recovery | `Fetch` v7+ | [Fetch session handler][src/Consumer/FetchSessionHandler.cs], [consumer options](configuration/consumer-options.md) | Configured CI: [session tests][unit/Consumer/FetchSessionHandlerTests.cs]. No outstanding issue tracked for this scope. |
| [KIP-320][kip320] log truncation handling | Complete: consumer epoch tracking and fetch-position recovery | Epoch fields require `Fetch` v9+; last-fetched epoch uses v12+ | [Consumer implementation][src/Consumer/KafkaConsumer.cs], [offset management](consumer/offset-management.md) | Configured CI: [truncation integration tests][integration/ConsumerLogTruncationIntegrationTests.cs]. No claim of replica/broker implementation. |
| [KIP-951][kip951] leader discovery | Complete: use leader hints in produce/fetch responses and recover through metadata | `Produce` v10+ / `Fetch` v16+ for hints; older versions use metadata recovery | [Response fields][src/Protocol/Messages/LeaderDiscoveryFields.cs] | Configured CI: [wire tests][unit/Protocol/LeaderDiscoveryResponseTests.cs], [consumer failover][integration/ConsumerLeaderFailoverIntegrationTests.cs], [producer failover][integration/ProducerLeaderFailoverIntegrationTests.cs]. No outstanding issue tracked. |

## Producers and share consumers

| Feature / authoritative KIP | Status and scope | Broker requirement | Implementation / documentation | Evidence and remaining work |
| --- | --- | --- | --- | --- |
| [KIP-98][kip98] idempotence and transactions | Complete: sequenced production, commit/abort, transactional offsets and read-committed consumption | Transaction APIs and record batch format v2 (Kafka 0.11+ protocol); group-based consumption additionally requires Kafka 4.0+ | [Transactions](producer/transactions.md), [producer][src/Producer/KafkaProducer.cs] | Configured CI: [idempotence][integration/IdempotentProducerTests.cs], [transactions][integration/TransactionTests.cs], [isolation][integration/TransactionIsolationLevelTests.cs]. Exactly-once external side effects require application coordination. |
| [KIP-890][kip890] Transactions V2 | Complete: negotiate V2 and adopt producer identity returned at transaction completion | `transaction.version` 2+, `InitProducerId` / `EndTxn` v5+; Kafka 4.0+ | [Producer][src/Producer/KafkaProducer.cs] | Configured CI: [V2 integration tests][integration/TransactionV2Tests.cs]. No outstanding issue tracked for this scope. |
| [KIP-939][kip939] external two-phase commit | Experimental: prepare and complete prepared transactions | `transaction.version` 3+ and `InitProducerId` v6; require an explicitly supporting broker, not merely Kafka 4.x | [Two-phase transaction API](producer/transactions.md), [options][src/Producer/ProducerOptions.cs] | Source: [transaction unit tests][unit/Producer/TransactionTests.cs], [wire tests][unit/Protocol/TransactionProtocolTests.cs]. No successful supporting-broker run cited; KIP acceptance is not release evidence. |
| [KIP-932][kip932] share consumption | Complete for queue consumption: explicit acknowledgements, record ownership, renewal and close | Dekaf requires `ShareGroupHeartbeat` v1 and `ShareAcknowledge` v1+; `ShareFetch` v0+; share groups enabled. Integration tests require Kafka 4.2+. Upstream: 4.0 early access, 4.1 preview, 4.2 completed | [Share consumers](consumer/share-consumers.md), [implementation][src/ShareConsumer/KafkaShareConsumer.cs] | Configured CI: [share integration tests][integration/ShareConsumerTests.cs], [ownership tests][integration/ShareConsumerOwnershipLoadTests.cs]. Early-access broker support is not a production recommendation. |

## Telemetry and authentication

| Feature / authoritative KIP | Status and scope | Broker / runtime requirement | Implementation / documentation | Evidence and remaining work |
| --- | --- | --- | --- | --- |
| [KIP-714][kip714] telemetry transport and required metrics | Complete for subscription, identity, push and required producer/consumer metrics | `GetTelemetrySubscriptions` and `PushTelemetry` v0; broker subscription and receiver configured | [Observability](observability.md), [telemetry manager][src/Telemetry/ClientTelemetryManager.cs] | Configured CI: [manager tests][unit/Telemetry/ClientTelemetryManagerTests.cs], [receiver integration tests][integration/ClientTelemetryReceiverIntegrationTests.cs] (Kafka 4.2+ fixture gate). This is narrower than complete KIP-714 support. |
| [KIP-714][kip714] standard metric catalog | Complete for applicable producer and ordinary-consumer metrics, including connection rates, queue times, commit/fetch/rebalance timing, assignments and asynchronous poll idle ratio | Same telemetry APIs; subscription selects requested metrics; measurement boundaries and reset behavior are documented | [Metric collector][src/Telemetry/ClientTelemetryMetricCollector.cs], [standard metrics][src/Telemetry/StandardClientTelemetryMetrics.cs], [observability](observability.md) | Configured CI: [standard metric tests][unit/Telemetry/StandardClientTelemetryMetricsTests.cs], [collector tests][unit/Telemetry/ClientTelemetryMetricCollectorTests.cs], [receiver integration tests][integration/ClientTelemetryReceiverIntegrationTests.cs]. Delivered under [#3311][issue3311]. |
| [KIP-714][kip714] OTLP resource labels | Complete: configured rack/group/static membership/transaction IDs and current joined member ID; unavailable values omitted | Same telemetry APIs and OTLP payload format; attributes apply only to the relevant client roles | [Resource attributes](observability.md#client-resource-attributes), [payload provider][src/Telemetry/ClientTelemetryPayloadProvider.cs] | Configured CI: [resource decoding tests][unit/Telemetry/ClientTelemetryResourceTests.cs], [receiver integration tests][integration/ClientTelemetryReceiverIntegrationTests.cs]. Delivered under [#3312][issue3312]. |
| [KIP-43][kip43] PLAIN and [KIP-84][kip84] SCRAM | Complete: PLAIN, SCRAM-SHA-256 and SCRAM-SHA-512 | Broker listener must enable selected mechanism; `SaslHandshake` v1 and `SaslAuthenticate` v2+. Both core runtime paths | [SASL configuration](security/sasl.md) | Configured CI: [authentication tests][integration/Security/SaslAuthenticationTests.cs], [SASL/TLS tests][integration/Security/SaslSslAuthenticationTests.cs]. No outstanding issue tracked. |
| [KIP-255][kip255] OAUTHBEARER | Complete for token authentication and refresh | OAUTHBEARER-enabled broker and token provider; `SaslHandshake` v1 / `SaslAuthenticate` v2+; both core runtime paths | [OAuth](security/oauth.md), [authenticator][src/Security/Sasl/OAuthBearerAuthenticator.cs] | Source: [authenticator tests][unit/Security/Sasl/OAuthBearerAuthenticatorTests.cs], [refresh concurrency tests][unit/Security/Sasl/OAuthBearerRefreshConcurrencyTests.cs]. Provider-specific deployments need their own validation. |
| [KIP-368][kip368] SASL re-authentication | Complete: negotiated session lifetime and gated connection exchange | Session lifetime introduced in `SaslAuthenticate` v1; Dekaf negotiates v2+ and requires broker session lifetime; supported underlying mechanism required | [Connection implementation][src/Networking/KafkaConnection.cs] | Configured CI: [re-authentication integration tests][integration/Security/SaslReauthenticationIntegrationTests.cs]. GSSAPI runtime restrictions still apply. |
| [KIP-12][kip12] GSSAPI / Kerberos | Complete for .NET 8 and .NET 10 package consumers; the `netstandard2.0` asset remains unsupported | GSSAPI-enabled listener with `SaslHandshake` v1 / `SaslAuthenticate` v2+; .NET 8 and .NET 10 use Windows SSPI or Unix GSSAPI. Linux requires Kerberos libraries/credentials; macOS uses Heimdal. Windows explicit `KeytabPath` is unsupported; use credential store/service identity | [SASL](security/sasl.md), [authenticator][src/Security/Sasl/GssapiAuthenticator.cs], [configuration][src/Security/Sasl/GssapiConfig.cs] | Configured CI: [local KDC round trip][integration/Security/GssapiAuthenticationIntegrationTests.cs] runs on Linux/.NET 8 and .NET 10, including clean NuGet consumers and invalid service rejection. Windows/macOS support here is Source evidence. Package asset coverage: [#3313][issue3313]. |

## Administration and Streams scope

Admin capabilities negotiate versions per destination. Optional interfaces expose
additional operations without requiring every custom `IAdminClient` to implement
them. The following rows identify representative capability boundaries; consult
[IAdminClient][src/Admin/IAdminClient.cs] and the administration navigation for
the full maintained API surface.

| Feature / authoritative KIP | Status and scope | Broker requirement | Implementation / documentation | Evidence and remaining work |
| --- | --- | --- | --- | --- |
| [KIP-516][kip516] topic IDs in Admin operations | Complete: describe and delete by ID | `Metadata` v10+ for description; `DeleteTopics` v6 for deletion | [Topic identifiers](admin/topic-identifiers.md) | Configured CI: [Admin topic-ID tests][unit/Admin/AdminClientTopicIdTests.cs]. No outstanding issue tracked. |
| [KIP-1043][kip1043] group administration | Complete: group-type listing and classic group description | `ListGroups` v4 for state filters, v5 for type filters; classic description uses `DescribeGroups` | [Group listing](admin/group-listing.md) | Configured CI: [listing tests][unit/Admin/AdminClientGroupListingTests.cs], [classic description tests][unit/Admin/AdminClientClassicGroupDescriptionTests.cs]. Does not add classic consumer participation. |
| [KIP-584][kip584] feature administration | Complete: inspect finalized features and request feature updates | `ApiVersions` v3+ feature fields and `UpdateFeatures` v0+; requested level must be supported by destination | [Node features](admin/node-features.md), [Admin API][src/Admin/IAdminClient.cs] | Configured CI: [feature tests][unit/Admin/AdminClientFeatureTests.cs]. Broker upgrade policy remains an operator responsibility. |
| [KIP-1071][kip1071] Streams membership | Partial / experimental for complete lifecycle: join, assignments, leave/rejoin and fencing implemented; later lifecycle fields await broker acceptance | `StreamsGroupHeartbeat` v0; Dekaf requires Kafka 4.2+. Task offsets/static membership tests target Kafka 4.4 because 4.3.1 rejects them | [Membership interface][src/Streams/IStreamsGroupMember.cs], [Streams administration](admin/streams-group-management.md) | Configured CI: [membership tests][integration/StreamsGroupMemberIntegrationTests.cs], with explicit 4.4 capability skips. Source: [unit tests][unit/Streams/StreamsGroupMemberTests.cs]. Complete lifecycle: [#2958][issue2958], parent [#2766][issue2766]. |
| Streams processing runtime (distinct from [KIP-1071][kip1071]) | Deferred: no topology DSL, state stores, changelog restoration or processing runtime | Not applicable until runtime scope is reopened | [Membership scope][src/Streams/IStreamsGroupMember.cs] | Source: [in-memory membership tests][unit/Testing/InMemoryStreamsGroupMemberTests.cs] cover membership only, not a runtime. Existing decision tracker [#2748][issue2748]. |

## Released brokers and future protocol work

Use the [configured compatibility coverage](api-compatibility.md#package-assets-and-tested-runtimes)
and [CI workflow][ci] for released Kafka 4.x testing. The [official Apache download index](https://downloads.apache.org/kafka/) has no
Kafka 4.4 release at this review date. Kafka 4.4 lifecycle checks
remain future acceptance under [#2958][issue2958]; do not treat their source or
capability skips as a passing 4.4 run. Likewise, the KIP-939 row records a feature
level requirement, not a claim that every released 4.x broker supports it.

## Maintaining this matrix

When a change adds, removes or limits support, update its row in the same PR.
Check the authoritative KIP and the destination API/feature gate; update runtime
limits, maintained documentation, tests and outstanding issue links together.
Keep completed decisions linked when they explain an exclusion. Split a row when
one part ships and another remains experimental. Link [CI][ci],
[performance gate][performance] and [stress tests][stress] instead of copying
their matrices here. Promote evidence to **Run** only with the exact run URL,
SHA, runtime, broker image and relevant results; skipped cases remain unverified.

[ci]: https://github.com/thomhurst/Dekaf/blob/main/.github/workflows/ci.yml
[integration/ClientTelemetryReceiverIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ClientTelemetryReceiverIntegrationTests.cs
[integration/ConsumerGroupTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ConsumerGroupTests.cs
[integration/ConsumerLeaderFailoverIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ConsumerLeaderFailoverIntegrationTests.cs
[integration/ConsumerLogTruncationIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ConsumerLogTruncationIntegrationTests.cs
[integration/IdempotentProducerTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/IdempotentProducerTests.cs
[integration/ProducerLeaderFailoverIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ProducerLeaderFailoverIntegrationTests.cs
[integration/Security/GssapiAuthenticationIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/Security/GssapiAuthenticationIntegrationTests.cs
[integration/Security/SaslAuthenticationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/Security/SaslAuthenticationTests.cs
[integration/Security/SaslReauthenticationIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/Security/SaslReauthenticationIntegrationTests.cs
[integration/Security/SaslSslAuthenticationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/Security/SaslSslAuthenticationTests.cs
[integration/ShareConsumerOwnershipLoadTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ShareConsumerOwnershipLoadTests.cs
[integration/ShareConsumerTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/ShareConsumerTests.cs
[integration/StreamsGroupMemberIntegrationTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/StreamsGroupMemberIntegrationTests.cs
[integration/TransactionIsolationLevelTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/TransactionIsolationLevelTests.cs
[integration/TransactionTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/TransactionTests.cs
[integration/TransactionV2Tests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Integration/TransactionV2Tests.cs
[issue2747]: https://github.com/thomhurst/Dekaf/issues/2747
[issue2748]: https://github.com/thomhurst/Dekaf/issues/2748
[issue2766]: https://github.com/thomhurst/Dekaf/issues/2766
[issue2958]: https://github.com/thomhurst/Dekaf/issues/2958
[issue3311]: https://github.com/thomhurst/Dekaf/issues/3311
[issue3312]: https://github.com/thomhurst/Dekaf/issues/3312
[issue3313]: https://github.com/thomhurst/Dekaf/issues/3313
[kip1043]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=305171038
[kip1071]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=311627992
[kip12]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=51809888
[kip227]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=74687799
[kip255]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=75968876
[kip320]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=87295403
[kip368]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-368%3A+Allow+SASL+Connections+to+Periodically+Re-Authenticate
[kip43]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-43%3A+Kafka+SASL+enhancements
[kip516]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-516%3A+Topic+Identifiers
[kip584]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-584%3A+Versioning+scheme+for+features
[kip714]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=173085915
[kip84]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-84%3A+Support+SASL+SCRAM+mechanisms
[kip848]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=217387038
[kip890]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=235834631
[kip932]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=255070434
[kip939]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=255071659
[kip951]: https://cwiki.apache.org/confluence/display/KAFKA/KIP-951%3A+Leader+discovery+optimizations+for+the+client
[kip98]: https://cwiki.apache.org/confluence/pages/viewpage.action?pageId=66854913
[performance]: https://github.com/thomhurst/Dekaf/blob/main/.github/workflows/performance-gate.yml
[src/Admin/IAdminClient.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Admin/IAdminClient.cs
[src/Consumer/ConsumerCoordinator.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Consumer/ConsumerCoordinator.cs
[src/Consumer/FetchSessionHandler.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Consumer/FetchSessionHandler.cs
[src/Consumer/KafkaConsumer.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Consumer/KafkaConsumer.cs
[src/Networking/KafkaConnection.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Networking/KafkaConnection.cs
[src/Producer/KafkaProducer.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Producer/KafkaProducer.cs
[src/Producer/ProducerOptions.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Producer/ProducerOptions.cs
[src/Protocol/Messages/LeaderDiscoveryFields.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Protocol/Messages/LeaderDiscoveryFields.cs
[src/Security/Sasl/GssapiAuthenticator.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Security/Sasl/GssapiAuthenticator.cs
[src/Security/Sasl/GssapiConfig.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Security/Sasl/GssapiConfig.cs
[src/Security/Sasl/OAuthBearerAuthenticator.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Security/Sasl/OAuthBearerAuthenticator.cs
[src/ShareConsumer/KafkaShareConsumer.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/ShareConsumer/KafkaShareConsumer.cs
[src/Streams/IStreamsGroupMember.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Streams/IStreamsGroupMember.cs
[src/Telemetry/ClientTelemetryManager.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Telemetry/ClientTelemetryManager.cs
[src/Telemetry/ClientTelemetryMetricCollector.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Telemetry/ClientTelemetryMetricCollector.cs
[src/Telemetry/ClientTelemetryPayloadProvider.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Telemetry/ClientTelemetryPayloadProvider.cs
[src/Telemetry/StandardClientTelemetryMetrics.cs]: https://github.com/thomhurst/Dekaf/blob/main/src/Dekaf/Telemetry/StandardClientTelemetryMetrics.cs
[stress]: https://github.com/thomhurst/Dekaf/blob/main/.github/workflows/stress-tests.yml
[unit/Admin/AdminClientClassicGroupDescriptionTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Admin/AdminClientClassicGroupDescriptionTests.cs
[unit/Admin/AdminClientFeatureTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Admin/AdminClientFeatureTests.cs
[unit/Admin/AdminClientGroupListingTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Admin/AdminClientGroupListingTests.cs
[unit/Admin/AdminClientTopicIdTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Admin/AdminClientTopicIdTests.cs
[unit/Consumer/ConsumerCoordinatorKip848Tests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Consumer/ConsumerCoordinatorKip848Tests.cs
[unit/Consumer/FetchSessionHandlerTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Consumer/FetchSessionHandlerTests.cs
[unit/Producer/TransactionTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Producer/TransactionTests.cs
[unit/Protocol/LeaderDiscoveryResponseTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Protocol/LeaderDiscoveryResponseTests.cs
[unit/Protocol/TransactionProtocolTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Protocol/TransactionProtocolTests.cs
[unit/Security/Sasl/OAuthBearerAuthenticatorTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Security/Sasl/OAuthBearerAuthenticatorTests.cs
[unit/Security/Sasl/OAuthBearerRefreshConcurrencyTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Security/Sasl/OAuthBearerRefreshConcurrencyTests.cs
[unit/Streams/StreamsGroupMemberTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Streams/StreamsGroupMemberTests.cs
[unit/Telemetry/ClientTelemetryManagerTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Telemetry/ClientTelemetryManagerTests.cs
[unit/Telemetry/ClientTelemetryMetricCollectorTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Telemetry/ClientTelemetryMetricCollectorTests.cs
[unit/Telemetry/ClientTelemetryPayloadProviderTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Telemetry/ClientTelemetryPayloadProviderTests.cs
[unit/Telemetry/StandardClientTelemetryMetricsTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Telemetry/StandardClientTelemetryMetricsTests.cs
[unit/Testing/InMemoryStreamsGroupMemberTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Testing/InMemoryStreamsGroupMemberTests.cs

[unit/Telemetry/ClientTelemetryResourceTests.cs]: https://github.com/thomhurst/Dekaf/blob/main/tests/Dekaf.Tests.Unit/Telemetry/ClientTelemetryResourceTests.cs
