using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests for config admin API types.
/// </summary>
public class ConfigTypesTests
{
    #region ConfigResource Tests

    [Test]
    public async Task ConfigResource_Topic_CreatesCorrectType()
    {
        var resource = ConfigResource.Topic("my-topic");

        await Assert.That(resource.Type).IsEqualTo(ConfigResourceType.Topic);
        await Assert.That(resource.Name).IsEqualTo("my-topic");
    }

    [Test]
    public async Task ConfigResource_Broker_CreatesCorrectType()
    {
        var resource = ConfigResource.Broker(1);

        await Assert.That(resource.Type).IsEqualTo(ConfigResourceType.Broker);
        await Assert.That(resource.Name).IsEqualTo("1");
    }

    [Test]
    public async Task ConfigResource_ClusterBroker_CreatesCorrectType()
    {
        var resource = ConfigResource.ClusterBroker();

        await Assert.That(resource.Type).IsEqualTo(ConfigResourceType.Broker);
        await Assert.That(resource.Name).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ConfigResource_BrokerLogger_CreatesCorrectType()
    {
        var resource = ConfigResource.BrokerLogger(2);

        await Assert.That(resource.Type).IsEqualTo(ConfigResourceType.BrokerLogger);
        await Assert.That(resource.Name).IsEqualTo("2");
    }

    [Test]
    public async Task ConfigResource_Equality_SameTypeAndName_AreEqual()
    {
        var resource1 = ConfigResource.Topic("test");
        var resource2 = ConfigResource.Topic("test");

        await Assert.That(resource1.Equals(resource2)).IsTrue();
        await Assert.That(resource1.GetHashCode()).IsEqualTo(resource2.GetHashCode());
    }

    [Test]
    public async Task ConfigResource_Equality_DifferentType_AreNotEqual()
    {
        var resource1 = ConfigResource.Topic("test");
        var resource2 = new ConfigResource { Type = ConfigResourceType.Broker, Name = "test" };

        await Assert.That(resource1.Equals(resource2)).IsFalse();
    }

    [Test]
    public async Task ConfigResource_Equality_DifferentName_AreNotEqual()
    {
        var resource1 = ConfigResource.Topic("test1");
        var resource2 = ConfigResource.Topic("test2");

        await Assert.That(resource1.Equals(resource2)).IsFalse();
    }

    [Test]
    public async Task ConfigResource_ToString_ReturnsExpectedFormat()
    {
        var resource = ConfigResource.Topic("my-topic");

        await Assert.That(resource.ToString()).IsEqualTo("Topic:my-topic");
    }

    [Test]
    public async Task ConfigResource_Equality_WithNull_ReturnsFalse()
    {
        var resource = ConfigResource.Topic("test");

        await Assert.That(resource.Equals(null)).IsFalse();
    }

    [Test]
    public async Task ConfigResource_CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<ConfigResource, string>
        {
            [ConfigResource.Topic("topic1")] = "value1",
            [ConfigResource.Topic("topic2")] = "value2",
            [ConfigResource.Broker(1)] = "value3"
        };

        await Assert.That(dict[ConfigResource.Topic("topic1")]).IsEqualTo("value1");
        await Assert.That(dict[ConfigResource.Topic("topic2")]).IsEqualTo("value2");
        await Assert.That(dict[ConfigResource.Broker(1)]).IsEqualTo("value3");
    }

    #endregion

    #region ConfigAlter Tests

    [Test]
    public async Task ConfigAlter_Set_CreatesCorrectOperation()
    {
        var alter = ConfigAlter.Set("retention.ms", "3600000");

        await Assert.That(alter.Name).IsEqualTo("retention.ms");
        await Assert.That(alter.Value).IsEqualTo("3600000");
        await Assert.That(alter.Operation).IsEqualTo(ConfigAlterOperation.Set);
    }

    [Test]
    public async Task ConfigAlter_Delete_CreatesCorrectOperation()
    {
        var alter = ConfigAlter.Delete("retention.ms");

        await Assert.That(alter.Name).IsEqualTo("retention.ms");
        await Assert.That(alter.Value).IsNull();
        await Assert.That(alter.Operation).IsEqualTo(ConfigAlterOperation.Delete);
    }

    [Test]
    public async Task ConfigAlter_Append_CreatesCorrectOperation()
    {
        var alter = ConfigAlter.Append("follower.replication.throttled.replicas", "0:1");

        await Assert.That(alter.Name).IsEqualTo("follower.replication.throttled.replicas");
        await Assert.That(alter.Value).IsEqualTo("0:1");
        await Assert.That(alter.Operation).IsEqualTo(ConfigAlterOperation.Append);
    }

    [Test]
    public async Task ConfigAlter_Subtract_CreatesCorrectOperation()
    {
        var alter = ConfigAlter.Subtract("follower.replication.throttled.replicas", "0:1");

        await Assert.That(alter.Name).IsEqualTo("follower.replication.throttled.replicas");
        await Assert.That(alter.Value).IsEqualTo("0:1");
        await Assert.That(alter.Operation).IsEqualTo(ConfigAlterOperation.Subtract);
    }

    #endregion

    #region ConfigEntry Tests

    [Test]
    public async Task ConfigEntry_DefaultValues_AreCorrect()
    {
        var entry = new ConfigEntry
        {
            Name = "test.config"
        };

        await Assert.That(entry.Name).IsEqualTo("test.config");
        await Assert.That(entry.Value).IsNull();
        await Assert.That(entry.IsReadOnly).IsFalse();
        await Assert.That(entry.IsDefault).IsFalse();
        await Assert.That(entry.IsSensitive).IsFalse();
        await Assert.That(entry.Source).IsEqualTo(ConfigSource.Unknown);
        await Assert.That(entry.Synonyms).IsNull();
        await Assert.That(entry.Documentation).IsNull();
    }

    [Test]
    public async Task ConfigEntry_WithAllProperties_SetsCorrectly()
    {
        var synonyms = new List<ConfigSynonym>
        {
            new() { Name = "synonym1", Value = "value1", Source = ConfigSource.DefaultConfig }
        };

        var entry = new ConfigEntry
        {
            Name = "retention.ms",
            Value = "604800000",
            IsReadOnly = false,
            IsDefault = true,
            IsSensitive = false,
            Source = ConfigSource.DefaultConfig,
            Synonyms = synonyms,
            Documentation = "The retention time for logs"
        };

        await Assert.That(entry.Name).IsEqualTo("retention.ms");
        await Assert.That(entry.Value).IsEqualTo("604800000");
        await Assert.That(entry.IsDefault).IsTrue();
        await Assert.That(entry.Source).IsEqualTo(ConfigSource.DefaultConfig);
        await Assert.That(entry.Synonyms).IsNotNull();
        await Assert.That(entry.Synonyms!.Count).IsEqualTo(1);
        await Assert.That(entry.Documentation).IsEqualTo("The retention time for logs");
    }

    #endregion

    #region Options Tests

    [Test]
    public async Task DescribeConfigsOptions_DefaultValues_AreCorrect()
    {
        var options = new DescribeConfigsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
        await Assert.That(options.IncludeSynonyms).IsFalse();
        await Assert.That(options.IncludeDocumentation).IsFalse();
    }

    [Test]
    public async Task AlterConfigsOptions_DefaultValues_AreCorrect()
    {
        var options = new AlterConfigsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
        await Assert.That(options.ValidateOnly).IsFalse();
    }

    [Test]
    public async Task IncrementalAlterConfigsOptions_DefaultValues_AreCorrect()
    {
        var options = new IncrementalAlterConfigsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
        await Assert.That(options.ValidateOnly).IsFalse();
    }

    #endregion

    #region Enum Values Tests

    [Test]
    public async Task ConfigResourceType_HasCorrectValues()
    {
        // Cast to sbyte and store in variables to avoid constant value assertions
        var unknown = (sbyte)ConfigResourceType.Unknown;
        var topic = (sbyte)ConfigResourceType.Topic;
        var broker = (sbyte)ConfigResourceType.Broker;
        var brokerLogger = (sbyte)ConfigResourceType.BrokerLogger;

        await Assert.That(unknown).IsEqualTo((sbyte)0);
        await Assert.That(topic).IsEqualTo((sbyte)2);
        await Assert.That(broker).IsEqualTo((sbyte)4);
        await Assert.That(brokerLogger).IsEqualTo((sbyte)8);
    }

    [Test]
    public async Task ConfigSource_HasCorrectValues()
    {
        var unknown = (sbyte)ConfigSource.Unknown;
        var dynamicTopicConfig = (sbyte)ConfigSource.DynamicTopicConfig;
        var dynamicBrokerConfig = (sbyte)ConfigSource.DynamicBrokerConfig;
        var dynamicDefaultBrokerConfig = (sbyte)ConfigSource.DynamicDefaultBrokerConfig;
        var staticBrokerConfig = (sbyte)ConfigSource.StaticBrokerConfig;
        var defaultConfig = (sbyte)ConfigSource.DefaultConfig;
        var dynamicBrokerLoggerConfig = (sbyte)ConfigSource.DynamicBrokerLoggerConfig;

        await Assert.That(unknown).IsEqualTo((sbyte)0);
        await Assert.That(dynamicTopicConfig).IsEqualTo((sbyte)1);
        await Assert.That(dynamicBrokerConfig).IsEqualTo((sbyte)2);
        await Assert.That(dynamicDefaultBrokerConfig).IsEqualTo((sbyte)3);
        await Assert.That(staticBrokerConfig).IsEqualTo((sbyte)4);
        await Assert.That(defaultConfig).IsEqualTo((sbyte)5);
        await Assert.That(dynamicBrokerLoggerConfig).IsEqualTo((sbyte)6);
    }

    [Test]
    public async Task ConfigAlterOperation_HasCorrectValues()
    {
        var set = (sbyte)ConfigAlterOperation.Set;
        var delete = (sbyte)ConfigAlterOperation.Delete;
        var append = (sbyte)ConfigAlterOperation.Append;
        var subtract = (sbyte)ConfigAlterOperation.Subtract;

        await Assert.That(set).IsEqualTo((sbyte)0);
        await Assert.That(delete).IsEqualTo((sbyte)1);
        await Assert.That(append).IsEqualTo((sbyte)2);
        await Assert.That(subtract).IsEqualTo((sbyte)3);
    }

    #endregion
}
