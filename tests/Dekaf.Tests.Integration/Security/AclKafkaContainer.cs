using Dekaf.Admin;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Kafka container with SASL_PLAINTEXT authentication and ACL authorization enabled.
/// Configures two users:
///   - "admin" (super user) with full access
///   - "testuser" with no default permissions (ACLs must be explicitly granted)
///
/// Uses KRaft mode with StandardAuthorizer.
/// Extends <see cref="KafkaTestContainer"/> to reuse common container lifecycle logic.
/// </summary>
public class AclKafkaContainer : KafkaTestContainer
{
    // JAAS config for SASL PLAIN with two users: admin and testuser
    private const string JaasConfig =
        "org.apache.kafka.common.security.plain.PlainLoginModule required " +
        """username="admin" password="admin-secret" """ +
        """user_admin="admin-secret" """ +
        """user_testuser="testuser-secret";""";

    /// <summary>
    /// The admin username that has super-user privileges.
    /// </summary>
    public const string AdminUsername = "admin";

    /// <summary>
    /// The admin password.
    /// </summary>
    public const string AdminPassword = "admin-secret";

    /// <summary>
    /// The restricted test user that has no default permissions.
    /// </summary>
    public const string TestUsername = "testuser";

    /// <summary>
    /// The restricted test user's password.
    /// </summary>
    public const string TestPassword = "testuser-secret";

    public override string ContainerName => "apache/kafka:3.9.1";
    public override int Version => 391;

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) =>
        builder
            // Enable StandardAuthorizer for KRaft mode
            .WithEnvironment("KAFKA_AUTHORIZER_CLASS_NAME",
                "org.apache.kafka.metadata.authorizer.StandardAuthorizer")
            // Admin is a super user - bypasses all ACL checks
            .WithEnvironment("KAFKA_SUPER_USERS", "User:admin")
            // Deny access by default when no ACLs match
            .WithEnvironment("KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND", "false")
            // Configure SASL PLAIN on the external listener
            // The Testcontainers KafkaBuilder for apache/kafka creates listeners named:
            //   PLAINTEXT (controller), BROKER (inter-broker), and the external listener
            // We configure SASL on the external-facing listener
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP",
                "PLAINTEXT:SASL_PLAINTEXT,BROKER:SASL_PLAINTEXT")
            .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_BROKER_SASL_ENABLED_MECHANISMS", "PLAIN")
            // JAAS configuration for SASL PLAIN - applies to all listeners
            .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG", JaasConfig)
            .WithEnvironment("KAFKA_LISTENER_NAME_BROKER_PLAIN_SASL_JAAS_CONFIG", JaasConfig)
            // Inter-broker communication uses admin credentials
            .WithEnvironment("KAFKA_SASL_JAAS_CONFIG", JaasConfig)
            // Allow the controller to communicate without SASL (KRaft controller listener)
            .WithEnvironment("KAFKA_LISTENER_NAME_CONTROLLER_SASL_ENABLED_MECHANISMS", "PLAIN")
            .WithEnvironment("KAFKA_LISTENER_NAME_CONTROLLER_PLAIN_SASL_JAAS_CONFIG", JaasConfig);

    /// <summary>
    /// Creates an admin client authenticated as the super user.
    /// Overrides the base class to provide SASL credentials for topic creation and metadata operations.
    /// </summary>
    public override IAdminClient CreateAdminClient()
    {
        return Kafka.CreateAdminClient()
            .WithBootstrapServers(BootstrapServers)
            .WithSaslPlain(AdminUsername, AdminPassword)
            .WithClientId("acl-test-admin")
            .Build();
    }
}
