using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Unit.Extensions;

public sealed class ServiceProviderIsolationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReusedDescriptors_DisposedProviderDoesNotLeakInterceptors(bool keyed)
    {
        var calls = new List<string>();
        var services = CreateServices(keyed, calls);
        ProviderInterceptor firstInterceptor;
        await using (var first = services.BuildServiceProvider())
        {
            firstInterceptor = first.GetRequiredService<ProviderInterceptor>();
            await AssertClientsAsync(first, keyed, calls);
        }

        await Assert.That(firstInterceptor.Disposed).IsTrue();
        await using var second = services.BuildServiceProvider();
        await AssertClientsAsync(second, keyed, calls);
        await Assert.That(second.GetRequiredService<ProviderInterceptor>()).IsNotSameReferenceAs(firstInterceptor);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConcurrentProviders_KeepTheirOwnInterceptors(bool keyed)
    {
        var calls = new List<string>();
        var services = CreateServices(keyed, calls);
        using var barrier = new Barrier(2);
        services.AddSingleton(_ =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Both providers must resolve their interceptor concurrently.");
            return new ProviderInterceptor(calls);
        });
        await using var first = services.BuildServiceProvider();
        await using var second = services.BuildServiceProvider();

        await Task.WhenAll(
            Task.Run(() => ResolveClients(first, keyed)),
            Task.Run(() => ResolveClients(second, keyed)));

        await AssertClientsAsync(first, keyed, calls);
        await AssertClientsAsync(second, keyed, calls);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task CopiedDescriptors_UseProviderLoggerOrConfiguredFallback(bool keyed, bool configuredLogger)
    {
        using var fallback = new TestLoggerFactory();
        var services = CreateServices(keyed, [], configuredLogger ? fallback : null);
        var firstServices = new ServiceCollection();
        foreach (var descriptor in services)
            ((IServiceCollection)firstServices).Add(descriptor);
        firstServices.AddSingleton<ILoggerFactory, TestLoggerFactory>();

        TestLoggerFactory firstLogger;
        await using (var first = firstServices.BuildServiceProvider())
        {
            firstLogger = (TestLoggerFactory)first.GetRequiredService<ILoggerFactory>();
            var (producer, consumer) = ResolveClients(first, keyed);
            await AssertLoggerAsync(producer, firstLogger.Logger);
            await AssertLoggerAsync(consumer, firstLogger.Logger);
        }

        await Assert.That(firstLogger.Disposed).IsTrue();
        await using var second = services.BuildServiceProvider();
        var (secondProducer, secondConsumer) = ResolveClients(second, keyed);
        if (configuredLogger)
        {
            await AssertLoggerAsync(secondProducer, fallback.Logger);
            await AssertLoggerAsync(secondConsumer, fallback.Logger);
        }
        else
        {
            await Assert.That(GetField<ILogger>(secondProducer, "_logger"))
                .IsSameReferenceAs(NullLogger<KafkaProducer<string, string>>.Instance);
            await Assert.That(GetField<ILogger>(secondConsumer, "_logger"))
                .IsSameReferenceAs(NullLogger<KafkaConsumer<string, string>>.Instance);
        }
    }

    private static ServiceCollection CreateServices(bool keyed, List<string> calls, ILoggerFactory? loggerFactory = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new ProviderInterceptor(calls));
        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor<string, string, ProviderInterceptor>();
            builder.AddGlobalConsumerInterceptor<string, string, ProviderInterceptor>();
            void ConfigureProducer(ProducerBuilder<string, string> producer)
            {
                producer.WithBootstrapServers("localhost:9092").AddInterceptor(new InstanceInterceptor(calls));
                if (loggerFactory is not null)
                    producer.WithLoggerFactory(loggerFactory);
            }
            void ConfigureConsumer(ConsumerBuilder<string, string> consumer)
            {
                consumer.WithBootstrapServers("localhost:9092").WithGroupId("isolation-test")
                    .AddInterceptor(new InstanceInterceptor(calls));
                if (loggerFactory is not null)
                    consumer.WithLoggerFactory(loggerFactory);
            }
            if (keyed)
            {
                builder.AddProducer<string, string>("client", ConfigureProducer);
                builder.AddConsumer<string, string>("client", ConfigureConsumer);
            }
            else
            {
                builder.AddProducer<string, string>(ConfigureProducer);
                builder.AddConsumer<string, string>(ConfigureConsumer);
            }
        });
        return services;
    }

    private static (IKafkaProducer<string, string>, IKafkaConsumer<string, string>) ResolveClients(
        ServiceProvider provider, bool keyed) => keyed
        ? (provider.GetRequiredKeyedService<IKafkaProducer<string, string>>("client"),
           provider.GetRequiredKeyedService<IKafkaConsumer<string, string>>("client"))
        : (provider.GetRequiredService<IKafkaProducer<string, string>>(),
           provider.GetRequiredService<IKafkaConsumer<string, string>>());

    private static async Task AssertClientsAsync(ServiceProvider provider, bool keyed, List<string> calls)
    {
        var (producer, consumer) = ResolveClients(provider, keyed);
        var expected = provider.GetRequiredService<ProviderInterceptor>();
        var producerInterceptors = GetField<IProducerInterceptor<string, string>[]>(producer, "_interceptors");
        var consumerInterceptors = GetField<IConsumerInterceptor<string, string>[]>(consumer, "_interceptors");
        await Assert.That(producerInterceptors).Count().IsEqualTo(2);
        await Assert.That(consumerInterceptors).Count().IsEqualTo(2);
        await Assert.That(producerInterceptors[0]).IsSameReferenceAs(expected);
        await Assert.That(consumerInterceptors[0]).IsSameReferenceAs(expected);
        calls.Clear();
        foreach (var interceptor in producerInterceptors)
            interceptor.OnSend(new ProducerMessage<string, string> { Topic = "test", Value = "value" });
        foreach (var interceptor in consumerInterceptors)
            interceptor.OnConsume(default);
        await Assert.That(calls).Count().IsEqualTo(4);
        await Assert.That(calls[0]).IsEqualTo("global");
        await Assert.That(calls[1]).IsEqualTo("instance");
        await Assert.That(calls[2]).IsEqualTo("global");
        await Assert.That(calls[3]).IsEqualTo("instance");
    }

    private static async Task AssertLoggerAsync(object client, TestLogger expected)
    {
        var checksBefore = expected.EnabledCheckCount;
        GetField<ILogger>(client, "_logger").IsEnabled(LogLevel.Information);
        await Assert.That(expected.EnabledCheckCount).IsEqualTo(checksBefore + 1);
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private sealed class ProviderInterceptor(List<string> calls)
        : IProducerInterceptor<string, string>, IConsumerInterceptor<string, string>, IDisposable
    {
        public bool Disposed { get; private set; }
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            calls.Add("global");
            return message;
        }
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            calls.Add("global");
            return result;
        }
        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
        public void Dispose() => Disposed = true;
    }

    private sealed class InstanceInterceptor(List<string> calls)
        : IProducerInterceptor<string, string>, IConsumerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            calls.Add("instance");
            return message;
        }
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            calls.Add("instance");
            return result;
        }
        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public TestLogger Logger { get; } = new();
        public bool Disposed { get; private set; }
        public ILogger CreateLogger(string categoryName)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return Logger;
        }
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() => Disposed = true;
    }

    private sealed class TestLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public int EnabledCheckCount { get; private set; }
        public bool IsEnabled(LogLevel logLevel)
        {
            EnabledCheckCount++;
            return false;
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
