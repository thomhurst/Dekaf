package dekaf.testing;

import java.util.concurrent.CompletableFuture;

import org.apache.kafka.server.streams.InMemoryTopologyDescriptionPlugin;

/**
 * Kafka's in-memory topology description plugin, except that deleting the stored description of a group
 * whose ID starts with {@link #FAILING_GROUP_PREFIX} always fails. The broker then answers DeleteGroups
 * with GROUP_DELETION_FAILED and keeps the group.
 */
public final class FailingDeleteTopologyDescriptionPlugin extends InMemoryTopologyDescriptionPlugin {
    public static final String FAILING_GROUP_PREFIX = "dekaf-delete-fails-";

    @Override
    public CompletableFuture<Void> deleteTopology(String groupId) {
        if (groupId.startsWith(FAILING_GROUP_PREFIX)) {
            return CompletableFuture.failedFuture(new IllegalStateException(
                "Dekaf test plugin refused to delete the topology description of " + groupId));
        }
        return super.deleteTopology(groupId);
    }
}
