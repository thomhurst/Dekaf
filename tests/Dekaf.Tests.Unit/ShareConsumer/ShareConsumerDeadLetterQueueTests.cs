using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Protocol;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fixture = Dekaf.Tests.Unit.ShareConsumer.ShareConsumerOffsetResetTests.Fixture;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerDeadLetterQueueTests
{
    [Test]
    [Arguments("dlq.orders", null, null)]
    [Arguments("dlq.orders", false, "false")]
    [Arguments("dlq.orders", true, "true")]
    [Arguments(null, true, "true")]
    public async Task Initialize_AppliesExplicitDeadLetterConfigsOnceBeforeJoining(string? topic, bool? copyRecord, string? expectedCopy)
    {
        await using var fixture = new Fixture(Options(topic, copyRecord));
        await fixture.Consumer.InitializeAsync();
        await fixture.Consumer.InitializeAsync();

        var configs = fixture.Requests.Single().Resources.Single().Configs;
        await Assert.That(configs.Select(config => config.ConfigOperation)).All().Satisfy(operation => operation.IsEqualTo((sbyte)0));
        await Assert.That(configs.SingleOrDefault(config => config.Name == "errors.deadletterqueue.topic.name")?.Value)
            .IsEqualTo(topic);
        await Assert.That(configs.SingleOrDefault(config => config.Name == "errors.deadletterqueue.copy.record.enable")?.Value)
            .IsEqualTo(expectedCopy);
        await Assert.That(configs.Count).IsEqualTo((topic is null ? 0 : 1) + (expectedCopy is null ? 0 : 1));
    }

    [Test]
    public async Task Initialize_AppliesOffsetResetAndDeadLetterQueueInOneRequest()
    {
        await using var combined = new Fixture(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "offset-reset",
            AutoOffsetReset = AutoOffsetReset.Earliest, DeadLetterQueueTopic = "dlq.orders"
        });
        await combined.Consumer.InitializeAsync();

        var configs = combined.Requests.Single().Resources.Single().Configs;
        await Assert.That(configs.Count).IsEqualTo(2);
        await Assert.That(configs[0].Name).IsEqualTo("share.auto.offset.reset");
        await Assert.That(configs[0].Value).IsEqualTo("earliest");
        await Assert.That(configs[1].Name).IsEqualTo("errors.deadletterqueue.topic.name");
        await Assert.That(configs[1].Value).IsEqualTo("dlq.orders");
    }

    [Test]
    public async Task Initialize_SurfacesBrokerRejectionOfUnknownGroupConfig()
    {
        // Brokers before 4.4 reject errors.deadletterqueue.* as unknown group configs.
        await using var fixture = new Fixture(Options("dlq.orders"));
        fixture.Error = ErrorCode.InvalidConfig;

        var exception = await Assert.That(async () => await fixture.Consumer.InitializeAsync()).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
    }

    [Test]
    [Arguments("")]
    [Arguments("__dlq")]
    [Arguments(".")]
    [Arguments("..")]
    [Arguments("dlq orders")]
    [Arguments("dlq/orders")]
    [Arguments("dlq.ørders")]
    public async Task InvalidTopicName_IsRejectedByBuilderOptionsAndDependencyInjection(string topic)
    {
        await Assert.That(() => Kafka.CreateShareConsumer<string, string>().WithDeadLetterQueue(topic))
            .Throws<ArgumentException>();
        await Assert.That(() => KafkaShareConsumer<string, string>.CreateGroupConfigAlters(Options(topic))).Throws<ArgumentException>();

        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(Options(topic)));
        await using var provider = services.BuildServiceProvider();
        await Assert.That(() => provider.GetRequiredService<IKafkaShareConsumer<string, string>>())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task TopicNameLength_IsLimitedTo249Characters()
    {
        var builder = Kafka.CreateShareConsumer<string, string>();
        builder.WithDeadLetterQueue(new string('d', 249));
        await Assert.That(() => builder.WithDeadLetterQueue(new string('d', 250))).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Builder_SetsTopicAndCopyRecord(bool copyRecord)
    {
        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("dlq")
            .WithDeadLetterQueue("dlq.Orders_1-a", copyRecord).Build();

        var options = ShareConsumerOffsetResetTests.GetOptions(consumer);
        await Assert.That(options.DeadLetterQueueTopic).IsEqualTo("dlq.Orders_1-a");
        await Assert.That(options.DeadLetterQueueCopyRecord).IsEqualTo(copyRecord);
    }

    [Test]
    public async Task Builder_LeavesBrokerSettingsWhenNotConfigured()
    {
        await using var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("dlq").Build();

        var options = ShareConsumerOffsetResetTests.GetOptions(consumer);
        await Assert.That(options.DeadLetterQueueTopic).IsNull();
        await Assert.That(options.DeadLetterQueueCopyRecord).IsNull();
        await Assert.That(KafkaShareConsumer<string, string>.CreateGroupConfigAlters(options)).IsNull();
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("dlq.orders", null)]
    [Arguments("dlq.orders", true)]
    [Arguments(null, false)]
    public async Task DependencyInjection_PreservesEachSettingIndependently(string? topic, bool? copyRecord)
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(Options(topic, copyRecord)));
        await using var provider = services.BuildServiceProvider();

        var options = ShareConsumerOffsetResetTests.GetOptions(provider.GetRequiredService<IKafkaShareConsumer<string, string>>());
        await Assert.That(options.DeadLetterQueueTopic).IsEqualTo(topic);
        await Assert.That(options.DeadLetterQueueCopyRecord).IsEqualTo(copyRecord);
    }

    [Test]
    [Arguments(false, null, null)]
    [Arguments(true, "dlq.orders", null)]
    [Arguments(false, "dlq.orders", "true")]
    [Arguments(true, null, "false")]
    public async Task ConfigurationBinding_PreservesEachSettingIndependently(bool keyed, string? topic, string? copyRecord)
    {
        var values = new Dictionary<string, string?>
        {
            ["BootstrapServers:0"] = "localhost:9092",
            ["GroupId"] = "dlq"
        };
        if (topic is not null) values["DeadLetterQueueTopic"] = topic;
        if (copyRecord is not null) values["DeadLetterQueueCopyRecord"] = copyRecord;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            if (keyed) builder.AddShareConsumer<string, string>("configured", configuration);
            else builder.AddShareConsumer<string, string>(configuration);
        });
        await using var provider = services.BuildServiceProvider();
        var consumer = keyed
            ? provider.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("configured")
            : provider.GetRequiredService<IKafkaShareConsumer<string, string>>();

        var options = ShareConsumerOffsetResetTests.GetOptions(consumer);
        await Assert.That(options.DeadLetterQueueTopic).IsEqualTo(topic);
        await Assert.That(options.DeadLetterQueueCopyRecord).IsEqualTo(copyRecord is null ? null : bool.Parse(copyRecord));
    }

    [Test]
    public async Task ConfigurationBinding_RejectsInvalidTopicName()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapServers:0"] = "localhost:9092",
            ["GroupId"] = "dlq",
            ["DeadLetterQueueTopic"] = "__dlq"
        }).Build();
        var services = new ServiceCollection();
        services.AddDekaf(builder => builder.AddShareConsumer<string, string>(configuration));
        await using var provider = services.BuildServiceProvider();

        await Assert.That(() => provider.GetRequiredService<IKafkaShareConsumer<string, string>>())
            .Throws<ArgumentException>();
    }

    private static ShareConsumerOptions Options(string? topic, bool? copyRecord = null) => new()
    {
        BootstrapServers = ["localhost:9092"], GroupId = "offset-reset",
        DeadLetterQueueTopic = topic, DeadLetterQueueCopyRecord = copyRecord
    };
}
