using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Integration tests verifying that Kafka ACL enforcement works correctly.
/// These tests use a SASL-enabled Kafka broker with StandardAuthorizer,
/// where "admin" is a super user and "testuser" starts with no permissions.
///
/// Tests verify that:
/// - Operations without required ACLs are denied with AuthorizationException
/// - Operations succeed after ACLs are granted
/// - Different resource types (topic, group, cluster) enforce ACLs independently
/// </summary>
[Category("Security")]
[NotInParallel("AclKafka")]
[ClassDataSource<AclKafkaContainer>(Shared = SharedType.PerTestSession)]
public class AclEnforcementTests(AclKafkaContainer kafka)
{
    /// <summary>
    /// Waits for ACLs to propagate by polling DescribeAclsAsync until the expected
    /// number of ACL bindings appear for the given filter.
    /// Checks immediately before delaying to avoid unnecessary waits.
    /// </summary>
    private static async Task WaitForAclPropagationAsync(
        IAdminClient admin,
        AclBindingFilter filter,
        int expectedCount,
        int maxRetries = 30,
        int initialDelayMs = 100)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var acls = await admin.DescribeAclsAsync(filter);
            if (acls.Count >= expectedCount)
                return;

            await Task.Delay(initialDelayMs * (i + 1));
        }

        throw new TimeoutException(
            $"ACL propagation timed out: expected at least {expectedCount} ACL(s) for " +
            $"resource {filter.ResourceType}/{filter.ResourceName}, but they did not appear.");
    }

    /// <summary>
    /// Waits for ACL deletion to propagate by polling DescribeAclsAsync until the
    /// ACL count drops to the expected value.
    /// Checks immediately before delaying to avoid unnecessary waits.
    /// </summary>
    private static async Task WaitForAclDeletionAsync(
        IAdminClient admin,
        AclBindingFilter filter,
        int expectedCount = 0,
        int maxRetries = 30,
        int initialDelayMs = 100)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var acls = await admin.DescribeAclsAsync(filter);
            if (acls.Count <= expectedCount)
                return;

            await Task.Delay(initialDelayMs * (i + 1));
        }

        throw new TimeoutException(
            $"ACL deletion propagation timed out: expected at most {expectedCount} ACL(s) for " +
            $"resource {filter.ResourceType}/{filter.ResourceName}.");
    }

    #region Producer ACL Enforcement

    [Test]
    public async Task Producer_WithoutWritePermission_ThrowsAuthorizationException()
    {
        // Arrange: create topic as admin, then try to produce as testuser (no ACLs)
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-producer-denied")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act & Assert: producing without WRITE permission should fail
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            });
        });

        // The error should indicate authorization failure
        // Kafka returns TopicAuthorizationFailed (error code 29) when WRITE is denied
        await Assert.That(exception).IsNotNull();
        await Assert.That(
            exception is AuthorizationException ||
            exception.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed
        ).IsTrue();
    }

    [Test]
    public async Task Producer_WithWritePermission_SucceedsAfterAclCreated()
    {
        // Arrange: create topic and grant WRITE + DESCRIBE permissions to testuser
        var topic = await kafka.CreateTestTopicAsync();

        await using var admin = kafka.CreateAdminClient();

        // Grant WRITE on the topic (required for producing)
        await admin.CreateAclsAsync([
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Write),
            // DESCRIBE is needed for metadata lookup during produce
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Describe)
        ]);

        // Wait for ACLs to propagate by polling
        await WaitForAclPropagationAsync(
            admin,
            AclBindingFilter.ForResource(ResourceType.Topic, topic),
            expectedCount: 2);

        // Act: produce as testuser with the new ACLs
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-producer-allowed")
            .WithAcks(Acks.All)
            .WithIdempotence(false)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Assert: produce should succeed
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Consumer ACL Enforcement - Topic

    [Test]
    public async Task Consumer_WithoutReadPermissionOnTopic_ThrowsAuthorizationException()
    {
        // Arrange: create topic and produce a message as admin
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"acl-test-group-{Guid.NewGuid():N}";

        await using (var adminProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.AdminUsername, AclKafkaContainer.AdminPassword)
            .WithClientId("acl-test-admin-producer")
            .BuildAsync())
        {
            await adminProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            });
        }

        // Grant READ on the group (so the group authorization doesn't fail first)
        // but do NOT grant READ on the topic
        await using var admin = kafka.CreateAdminClient();
        await admin.CreateAclsAsync([
            AclBinding.Allow(
                ResourcePattern.Group(groupId),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Read)
        ]);

        // Wait for ACL to propagate by polling
        await WaitForAclPropagationAsync(
            admin,
            AclBindingFilter.ForResource(ResourceType.Group, groupId),
            expectedCount: 1);

        // Act & Assert: consuming without READ permission on topic should fail
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-consumer-topic-denied")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var exceptionThrown = false;
        var messageReceived = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                // If we receive a message, ACL enforcement is broken
                messageReceived = true;
                break;
            }
        }
        catch (KafkaException ex) when (
            ex is AuthorizationException ||
            ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed)
        {
            exceptionThrown = true;
        }
        catch (OperationCanceledException)
        {
            // Timeout without authorization exception - the assertion below will fail,
            // ensuring we don't silently pass when ACL enforcement is broken
        }

        // The test MUST fail if ACL enforcement is broken:
        // - If a message was received, ACLs are not being enforced
        // - If no exception was thrown AND no timeout occurred, something is wrong
        await Assert.That(messageReceived).IsFalse()
            .Because("consumer should not receive messages without READ permission on topic");
        await Assert.That(exceptionThrown).IsTrue()
            .Because("consumer should throw AuthorizationException when READ permission on topic is missing");
    }

    #endregion

    #region Consumer ACL Enforcement - Group

    [Test]
    public async Task Consumer_WithoutReadPermissionOnGroup_ThrowsAuthorizationException()
    {
        // Arrange: create topic as admin
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"acl-test-group-denied-{Guid.NewGuid():N}";

        // Grant READ on the topic but NOT on the consumer group
        await using var admin = kafka.CreateAdminClient();
        await admin.CreateAclsAsync([
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Read),
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Describe)
        ]);

        // Wait for ACLs to propagate by polling
        await WaitForAclPropagationAsync(
            admin,
            AclBindingFilter.ForResource(ResourceType.Topic, topic),
            expectedCount: 2);

        // Act & Assert: joining consumer group without READ permission should fail
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-consumer-group-denied")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var exceptionThrown = false;
        var messageReceived = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                // If we receive a message, ACL enforcement is broken
                messageReceived = true;
                break;
            }
        }
        catch (KafkaException ex) when (
            ex is AuthorizationException ||
            ex is GroupException ||
            ex.ErrorCode == Dekaf.Protocol.ErrorCode.GroupAuthorizationFailed)
        {
            exceptionThrown = true;
        }
        catch (OperationCanceledException)
        {
            // Timeout without authorization exception - the assertion below will fail,
            // ensuring we don't silently pass when ACL enforcement is broken
        }

        // The test MUST fail if ACL enforcement is broken:
        // - If a message was received, ACLs are not being enforced
        // - The consumer must throw an authorization/group exception
        await Assert.That(messageReceived).IsFalse()
            .Because("consumer should not receive messages without READ permission on group");
        await Assert.That(exceptionThrown).IsTrue()
            .Because("consumer should throw AuthorizationException when READ permission on group is missing");
    }

    #endregion

    #region Consumer ACL Enforcement - Success

    [Test]
    public async Task Consumer_WithReadPermission_SucceedsAfterAclCreated()
    {
        // Arrange: create topic and produce message as admin
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"acl-test-group-allowed-{Guid.NewGuid():N}";

        await using (var adminProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.AdminUsername, AclKafkaContainer.AdminPassword)
            .WithClientId("acl-test-admin-producer-for-consumer")
            .BuildAsync())
        {
            await adminProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "test-key",
                Value = "test-value"
            });
        }

        // Grant READ on both topic and group
        await using var admin = kafka.CreateAdminClient();
        await admin.CreateAclsAsync([
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Read),
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Describe),
            AclBinding.Allow(
                ResourcePattern.Group(groupId),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Read)
        ]);

        // Wait for ACLs to propagate by polling
        await WaitForAclPropagationAsync(
            admin,
            AclBindingFilter.ForResource(ResourceType.Topic, topic),
            expectedCount: 2);
        await WaitForAclPropagationAsync(
            admin,
            AclBindingFilter.ForResource(ResourceType.Group, groupId),
            expectedCount: 1);

        // Act: consume as testuser with proper ACLs
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-consumer-allowed")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Assert: should successfully consume the message
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Key).IsEqualTo("test-key");
        await Assert.That(consumed.Value.Value).IsEqualTo("test-value");
    }

    #endregion

    #region Admin ACL Enforcement

    [Test]
    public async Task AdminClient_WithoutAlterPermission_FailsOnConfigChange()
    {
        // Arrange: create topic as admin
        var topic = await kafka.CreateTestTopicAsync();

        // Connect as testuser (no ALTER permission on topic configs)
        await using var restrictedAdmin = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-restricted-admin")
            .Build();

        // Act & Assert: altering topic config without ALTER permission should fail
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
        {
            var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
            {
                [ConfigResource.Topic(topic)] =
                [
                    new ConfigEntry { Name = "retention.ms", Value = "3600000" }
                ]
            };

            await restrictedAdmin.AlterConfigsAsync(configs);
        });

        await Assert.That(exception).IsNotNull();
        // Should be either AuthorizationException or a KafkaException with authorization error code
        await Assert.That(
            exception is AuthorizationException ||
            exception.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed ||
            exception.ErrorCode == Dekaf.Protocol.ErrorCode.ClusterAuthorizationFailed
        ).IsTrue();
    }

    [Test]
    public async Task AdminClient_WithoutDescribePermission_FailsOnDescribeTopics()
    {
        // Arrange: create topic as admin
        var topic = await kafka.CreateTestTopicAsync();

        // Connect as testuser (no DESCRIBE permission)
        await using var restrictedAdmin = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
            .WithClientId("acl-test-restricted-admin-describe")
            .Build();

        // Act & Assert: describing topic without DESCRIBE permission should fail
        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
        {
            await restrictedAdmin.DescribeTopicsAsync([topic]);
        });

        await Assert.That(exception).IsNotNull();
        await Assert.That(
            exception is AuthorizationException ||
            exception.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed
        ).IsTrue();
    }

    #endregion

    #region ACL Lifecycle

    [Test]
    public async Task AclLifecycle_GrantAndRevoke_EnforcesCorrectly()
    {
        // This test verifies the full lifecycle:
        // 1. No ACL -> denied
        // 2. Grant ACL -> allowed
        // 3. Revoke ACL -> denied again

        var topic = await kafka.CreateTestTopicAsync();

        // Step 1: Verify access is denied without ACLs
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
                .WithClientId("acl-lifecycle-denied-1")
                .WithAcks(Acks.All)
                .BuildAsync();

            var denied = false;
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "key",
                    Value = "value"
                });
            }
            catch (KafkaException ex) when (
                ex is AuthorizationException ||
                ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed)
            {
                denied = true;
            }

            await Assert.That(denied).IsTrue();
        }

        // Step 2: Grant WRITE + DESCRIBE ACLs and verify access is allowed
        await using var admin = kafka.CreateAdminClient();

        await admin.CreateAclsAsync([
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Write),
            AclBinding.Allow(
                ResourcePattern.Topic(topic),
                AclKafkaContainer.TestPrincipal,
                AclOperation.Describe)
        ]);

        // Wait for ACLs to propagate by polling
        var topicFilter = AclBindingFilter.ForResource(ResourceType.Topic, topic);
        await WaitForAclPropagationAsync(admin, topicFilter, expectedCount: 2);

        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
                .WithClientId("acl-lifecycle-allowed")
                .WithAcks(Acks.All)
                .WithIdempotence(false)
                .BuildAsync();

            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            });

            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }

        // Step 3: Revoke ACLs and verify access is denied again
        var filter = new AclBindingFilter
        {
            ResourceType = ResourceType.Topic,
            ResourceName = topic,
            Principal = AclKafkaContainer.TestPrincipal
        };
        await admin.DeleteAclsAsync([filter]);

        // Wait for ACL deletion to propagate by polling
        await WaitForAclDeletionAsync(admin, topicFilter);

        {
            // Create a fresh producer to avoid cached authorization state from step 2
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithSaslPlain(AclKafkaContainer.TestUsername, AclKafkaContainer.TestPassword)
                .WithClientId("acl-lifecycle-denied-2")
                .WithAcks(Acks.All)
                .BuildAsync();

            var deniedAgain = false;
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "key",
                    Value = "value"
                });
            }
            catch (KafkaException ex) when (
                ex is AuthorizationException ||
                ex.ErrorCode == Dekaf.Protocol.ErrorCode.TopicAuthorizationFailed)
            {
                deniedAgain = true;
            }

            await Assert.That(deniedAgain).IsTrue();
        }
    }

    #endregion
}
