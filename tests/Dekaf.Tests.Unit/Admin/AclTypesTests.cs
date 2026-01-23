using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests for ACL types and helper methods.
/// </summary>
public class AclTypesTests
{
    #region ResourcePattern Tests

    [Test]
    public async Task ResourcePattern_Topic_CreatesCorrectPattern()
    {
        var pattern = ResourcePattern.Topic("my-topic");

        await Assert.That(pattern.Type).IsEqualTo(ResourceType.Topic);
        await Assert.That(pattern.Name).IsEqualTo("my-topic");
        await Assert.That(pattern.PatternType).IsEqualTo(PatternType.Literal);
    }

    [Test]
    public async Task ResourcePattern_Topic_WithPrefixedPattern()
    {
        var pattern = ResourcePattern.Topic("my-prefix", PatternType.Prefixed);

        await Assert.That(pattern.Type).IsEqualTo(ResourceType.Topic);
        await Assert.That(pattern.Name).IsEqualTo("my-prefix");
        await Assert.That(pattern.PatternType).IsEqualTo(PatternType.Prefixed);
    }

    [Test]
    public async Task ResourcePattern_Group_CreatesCorrectPattern()
    {
        var pattern = ResourcePattern.Group("my-group");

        await Assert.That(pattern.Type).IsEqualTo(ResourceType.Group);
        await Assert.That(pattern.Name).IsEqualTo("my-group");
        await Assert.That(pattern.PatternType).IsEqualTo(PatternType.Literal);
    }

    [Test]
    public async Task ResourcePattern_Cluster_CreatesCorrectPattern()
    {
        var pattern = ResourcePattern.Cluster();

        await Assert.That(pattern.Type).IsEqualTo(ResourceType.Cluster);
        await Assert.That(pattern.Name).IsEqualTo("kafka-cluster");
        await Assert.That(pattern.PatternType).IsEqualTo(PatternType.Literal);
    }

    [Test]
    public async Task ResourcePattern_TransactionalId_CreatesCorrectPattern()
    {
        var pattern = ResourcePattern.TransactionalId("my-tx-id");

        await Assert.That(pattern.Type).IsEqualTo(ResourceType.TransactionalId);
        await Assert.That(pattern.Name).IsEqualTo("my-tx-id");
        await Assert.That(pattern.PatternType).IsEqualTo(PatternType.Literal);
    }

    #endregion

    #region AclBinding Tests

    [Test]
    public async Task AclBinding_Allow_CreatesAllowBinding()
    {
        var binding = AclBinding.Allow(
            ResourcePattern.Topic("my-topic"),
            "User:alice",
            AclOperation.Read);

        await Assert.That(binding.Pattern.Type).IsEqualTo(ResourceType.Topic);
        await Assert.That(binding.Pattern.Name).IsEqualTo("my-topic");
        await Assert.That(binding.Entry.Principal).IsEqualTo("User:alice");
        await Assert.That(binding.Entry.Host).IsEqualTo("*");
        await Assert.That(binding.Entry.Operation).IsEqualTo(AclOperation.Read);
        await Assert.That(binding.Entry.Permission).IsEqualTo(AclPermissionType.Allow);
    }

    [Test]
    public async Task AclBinding_Allow_WithCustomHost()
    {
        var binding = AclBinding.Allow(
            ResourcePattern.Topic("my-topic"),
            "User:alice",
            AclOperation.Write,
            "192.168.1.100");

        await Assert.That(binding.Entry.Host).IsEqualTo("192.168.1.100");
    }

    [Test]
    public async Task AclBinding_Deny_CreatesDenyBinding()
    {
        var binding = AclBinding.Deny(
            ResourcePattern.Topic("sensitive-topic"),
            "User:bob",
            AclOperation.Read);

        await Assert.That(binding.Entry.Permission).IsEqualTo(AclPermissionType.Deny);
        await Assert.That(binding.Entry.Operation).IsEqualTo(AclOperation.Read);
    }

    #endregion

    #region AclBindingFilter Tests

    [Test]
    public async Task AclBindingFilter_MatchAll_CreatesDefaultFilter()
    {
        var filter = AclBindingFilter.MatchAll();

        await Assert.That(filter.ResourceType).IsEqualTo(ResourceType.Any);
        await Assert.That(filter.ResourceName).IsNull();
        await Assert.That(filter.PatternType).IsEqualTo(PatternType.Any);
        await Assert.That(filter.Principal).IsNull();
        await Assert.That(filter.Host).IsNull();
        await Assert.That(filter.Operation).IsEqualTo(AclOperation.Any);
        await Assert.That(filter.Permission).IsEqualTo(AclPermissionType.Any);
    }

    [Test]
    public async Task AclBindingFilter_ForResource_CreatesResourceFilter()
    {
        var filter = AclBindingFilter.ForResource(ResourceType.Topic, "my-topic");

        await Assert.That(filter.ResourceType).IsEqualTo(ResourceType.Topic);
        await Assert.That(filter.ResourceName).IsEqualTo("my-topic");
        await Assert.That(filter.PatternType).IsEqualTo(PatternType.Any);
    }

    [Test]
    public async Task AclBindingFilter_ForResource_WithPatternType()
    {
        var filter = AclBindingFilter.ForResource(ResourceType.Topic, "my-prefix", PatternType.Prefixed);

        await Assert.That(filter.PatternType).IsEqualTo(PatternType.Prefixed);
    }

    [Test]
    public async Task AclBindingFilter_ForPrincipal_CreatesPrincipalFilter()
    {
        var filter = AclBindingFilter.ForPrincipal("User:alice");

        await Assert.That(filter.Principal).IsEqualTo("User:alice");
        await Assert.That(filter.ResourceType).IsEqualTo(ResourceType.Any);
    }

    #endregion

    #region Enum Value Tests

    [Test]
    public async Task ResourceType_EnumValues_AreCorrect()
    {
        var unknown = ResourceType.Unknown;
        var any = ResourceType.Any;
        var topic = ResourceType.Topic;
        var group = ResourceType.Group;
        var cluster = ResourceType.Cluster;
        var transactionalId = ResourceType.TransactionalId;
        var delegationToken = ResourceType.DelegationToken;

        await Assert.That((sbyte)unknown).IsEqualTo((sbyte)0);
        await Assert.That((sbyte)any).IsEqualTo((sbyte)1);
        await Assert.That((sbyte)topic).IsEqualTo((sbyte)2);
        await Assert.That((sbyte)group).IsEqualTo((sbyte)3);
        await Assert.That((sbyte)cluster).IsEqualTo((sbyte)4);
        await Assert.That((sbyte)transactionalId).IsEqualTo((sbyte)5);
        await Assert.That((sbyte)delegationToken).IsEqualTo((sbyte)6);
    }

    [Test]
    public async Task PatternType_EnumValues_AreCorrect()
    {
        var unknown = PatternType.Unknown;
        var any = PatternType.Any;
        var match = PatternType.Match;
        var literal = PatternType.Literal;
        var prefixed = PatternType.Prefixed;

        await Assert.That((sbyte)unknown).IsEqualTo((sbyte)0);
        await Assert.That((sbyte)any).IsEqualTo((sbyte)1);
        await Assert.That((sbyte)match).IsEqualTo((sbyte)2);
        await Assert.That((sbyte)literal).IsEqualTo((sbyte)3);
        await Assert.That((sbyte)prefixed).IsEqualTo((sbyte)4);
    }

    [Test]
    public async Task AclOperation_EnumValues_AreCorrect()
    {
        var unknown = AclOperation.Unknown;
        var any = AclOperation.Any;
        var all = AclOperation.All;
        var read = AclOperation.Read;
        var write = AclOperation.Write;
        var create = AclOperation.Create;
        var delete = AclOperation.Delete;
        var alter = AclOperation.Alter;
        var describe = AclOperation.Describe;
        var clusterAction = AclOperation.ClusterAction;
        var describeConfigs = AclOperation.DescribeConfigs;
        var alterConfigs = AclOperation.AlterConfigs;
        var idempotentWrite = AclOperation.IdempotentWrite;

        await Assert.That((sbyte)unknown).IsEqualTo((sbyte)0);
        await Assert.That((sbyte)any).IsEqualTo((sbyte)1);
        await Assert.That((sbyte)all).IsEqualTo((sbyte)2);
        await Assert.That((sbyte)read).IsEqualTo((sbyte)3);
        await Assert.That((sbyte)write).IsEqualTo((sbyte)4);
        await Assert.That((sbyte)create).IsEqualTo((sbyte)5);
        await Assert.That((sbyte)delete).IsEqualTo((sbyte)6);
        await Assert.That((sbyte)alter).IsEqualTo((sbyte)7);
        await Assert.That((sbyte)describe).IsEqualTo((sbyte)8);
        await Assert.That((sbyte)clusterAction).IsEqualTo((sbyte)9);
        await Assert.That((sbyte)describeConfigs).IsEqualTo((sbyte)10);
        await Assert.That((sbyte)alterConfigs).IsEqualTo((sbyte)11);
        await Assert.That((sbyte)idempotentWrite).IsEqualTo((sbyte)12);
    }

    [Test]
    public async Task AclPermissionType_EnumValues_AreCorrect()
    {
        var unknown = AclPermissionType.Unknown;
        var any = AclPermissionType.Any;
        var deny = AclPermissionType.Deny;
        var allow = AclPermissionType.Allow;

        await Assert.That((sbyte)unknown).IsEqualTo((sbyte)0);
        await Assert.That((sbyte)any).IsEqualTo((sbyte)1);
        await Assert.That((sbyte)deny).IsEqualTo((sbyte)2);
        await Assert.That((sbyte)allow).IsEqualTo((sbyte)3);
    }

    #endregion

    #region Options Tests

    [Test]
    public async Task CreateAclsOptions_HasDefaultTimeout()
    {
        var options = new CreateAclsOptions();
        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task DeleteAclsOptions_HasDefaultTimeout()
    {
        var options = new DeleteAclsOptions();
        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task DescribeAclsOptions_HasDefaultTimeout()
    {
        var options = new DescribeAclsOptions();
        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    #endregion
}
