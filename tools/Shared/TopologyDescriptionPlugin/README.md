# Topology description plugin fixture

`TopologyDescriptionKafkaContainer` builds this test-only [KIP-1331](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/421957212/KIP-1331+Streams+Group+Topology+Description+Plugin) plugin against the exact Kafka image selected by `KAFKA_TEST_IMAGE_TAG` (default 4.4.0). Docker builds the Java class with a JDK; the host needs Docker and the repository .NET SDK, not Java. Image construction has a five-minute deadline. Brokers older than 4.4 have no plugin API, so the fixture uses the plain image for them and the tests are gated `[SupportsKafka(440)]`.

The broker sets `group.streams.topology.description.plugin.class=dekaf.testing.FailingDeleteTopologyDescriptionPlugin`. The plugin extends Kafka's `InMemoryTopologyDescriptionPlugin`, so publication and readback behave as with the shipped plugin. It always fails `deleteTopology` for groups whose ID starts with `dekaf-delete-fails-`. The broker then answers `DeleteGroups` with `GROUP_DELETION_FAILED` and keeps the group. From v3 the response carries Kafka's fixed message, not the plugin's exception text. The failure depends only on the group ID, not on call counts, so the broker's periodic cleanup cannot consume it.

Run:

```powershell
dotnet test --project tests/Dekaf.Tests.Integration --configuration Release --framework net10.0 --treenode-filter "/*/*/StreamsGroupTopologyDescriptionIntegrationTests/*"
```

References: [plugin interface](https://github.com/apache/kafka/blob/4.4.0/group-coordinator/group-coordinator-api/src/main/java/org/apache/kafka/coordinator/group/api/streams/StreamsGroupTopologyDescriptionPlugin.java), [in-memory plugin](https://github.com/apache/kafka/blob/4.4.0/server/src/main/java/org/apache/kafka/server/streams/InMemoryTopologyDescriptionPlugin.java), [deletion semantics](https://github.com/apache/kafka/blob/4.4.0/docs/streams/developer-guide/topology-description-plugin.md#group-deletion-and-group_deletion_failed).
