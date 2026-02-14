using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Extensions;

public sealed class GlobalInterceptorOrderingTests
{
    #region Producer Interceptor Ordering Tests

    [Test]
    public async Task GlobalProducerInterceptors_ExecuteBeforePerInstance()
    {
        var callOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor(typeof(OrderTrackingProducerInterceptor<,>));
            builder.AddProducer<string, string>(p =>
            {
                p.WithBootstrapServers("localhost:9092");
                p.AddInterceptor(new NamedProducerInterceptor("per-instance", callOrder));
            });
        });

        // Register the call order list and the name for the global interceptor in DI
        services.AddSingleton(callOrder);
        services.AddSingleton(new InterceptorName("global"));

        var sp = services.BuildServiceProvider();
        var producer = sp.GetRequiredService<IKafkaProducer<string, string>>();

        // Get the interceptors from the producer via reflection to invoke them directly
        var interceptors = GetProducerInterceptors<string, string>(producer);

        await Assert.That(interceptors).IsNotNull();
        await Assert.That(interceptors!).Count().IsEqualTo(2);

        // Simulate interceptor invocation (same order as the producer pipeline)
        var message = new ProducerMessage<string, string> { Topic = "test", Key = "key", Value = "value" };
        foreach (var interceptor in interceptors)
        {
            message = interceptor.OnSend(message);
        }

        await Assert.That(callOrder).Count().IsEqualTo(2);
        await Assert.That(callOrder[0]).IsEqualTo("global");
        await Assert.That(callOrder[1]).IsEqualTo("per-instance");
    }

    [Test]
    public async Task MultipleGlobalProducerInterceptors_ExecuteInRegistrationOrder()
    {
        var callOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor(typeof(OrderTrackingProducerInterceptor<,>));
            builder.AddGlobalProducerInterceptor(typeof(SecondOrderTrackingProducerInterceptor<,>));
            builder.AddProducer<string, string>(p =>
            {
                p.WithBootstrapServers("localhost:9092");
            });
        });

        services.AddSingleton(callOrder);
        services.AddSingleton(new InterceptorName("global-1"));
        services.AddSingleton(new SecondInterceptorName("global-2"));

        var sp = services.BuildServiceProvider();
        var producer = sp.GetRequiredService<IKafkaProducer<string, string>>();

        var interceptors = GetProducerInterceptors<string, string>(producer);

        await Assert.That(interceptors).IsNotNull();
        await Assert.That(interceptors!).Count().IsEqualTo(2);

        var message = new ProducerMessage<string, string> { Topic = "test", Key = "key", Value = "value" };
        foreach (var interceptor in interceptors)
        {
            message = interceptor.OnSend(message);
        }

        await Assert.That(callOrder).Count().IsEqualTo(2);
        await Assert.That(callOrder[0]).IsEqualTo("global-1");
        await Assert.That(callOrder[1]).IsEqualTo("global-2");
    }

    [Test]
    public async Task PerInstanceProducerInterceptors_ExecuteInRegistrationOrderAfterGlobals()
    {
        var callOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalProducerInterceptor(typeof(OrderTrackingProducerInterceptor<,>));
            builder.AddProducer<string, string>(p =>
            {
                p.WithBootstrapServers("localhost:9092");
                p.AddInterceptor(new NamedProducerInterceptor("per-instance-1", callOrder));
                p.AddInterceptor(new NamedProducerInterceptor("per-instance-2", callOrder));
            });
        });

        services.AddSingleton(callOrder);
        services.AddSingleton(new InterceptorName("global"));

        var sp = services.BuildServiceProvider();
        var producer = sp.GetRequiredService<IKafkaProducer<string, string>>();

        var interceptors = GetProducerInterceptors<string, string>(producer);

        await Assert.That(interceptors).IsNotNull();
        await Assert.That(interceptors!).Count().IsEqualTo(3);

        var message = new ProducerMessage<string, string> { Topic = "test", Key = "key", Value = "value" };
        foreach (var interceptor in interceptors)
        {
            message = interceptor.OnSend(message);
        }

        await Assert.That(callOrder).Count().IsEqualTo(3);
        await Assert.That(callOrder[0]).IsEqualTo("global");
        await Assert.That(callOrder[1]).IsEqualTo("per-instance-1");
        await Assert.That(callOrder[2]).IsEqualTo("per-instance-2");
    }

    [Test]
    public async Task NoGlobalInterceptors_ProducerHasNoInterceptors()
    {
        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, string>(p =>
            {
                p.WithBootstrapServers("localhost:9092");
            });
        });

        var sp = services.BuildServiceProvider();
        var producer = sp.GetRequiredService<IKafkaProducer<string, string>>();

        var interceptors = GetProducerInterceptors<string, string>(producer);
        await Assert.That(interceptors).IsNull();
    }

    #endregion

    #region Consumer Interceptor Ordering Tests

    [Test]
    public async Task GlobalConsumerInterceptors_ExecuteBeforePerInstance()
    {
        var callOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalConsumerInterceptor(typeof(OrderTrackingConsumerInterceptor<,>));
            builder.AddConsumer<string, string>(c =>
            {
                c.WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group");
                c.AddInterceptor(new NamedConsumerInterceptor("per-instance", callOrder));
            });
        });

        services.AddSingleton(callOrder);
        services.AddSingleton(new InterceptorName("global"));

        var sp = services.BuildServiceProvider();
        var consumer = sp.GetRequiredService<IKafkaConsumer<string, string>>();

        var interceptors = GetConsumerInterceptors<string, string>(consumer);

        await Assert.That(interceptors).IsNotNull();
        await Assert.That(interceptors!).Count().IsEqualTo(2);

        // Simulate interceptor invocation
        var result = default(ConsumeResult<string, string>);
        foreach (var interceptor in interceptors)
        {
            result = interceptor.OnConsume(result);
        }

        await Assert.That(callOrder).Count().IsEqualTo(2);
        await Assert.That(callOrder[0]).IsEqualTo("global");
        await Assert.That(callOrder[1]).IsEqualTo("per-instance");
    }

    [Test]
    public async Task MultipleGlobalConsumerInterceptors_ExecuteInRegistrationOrder()
    {
        var callOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddDekaf(builder =>
        {
            builder.AddGlobalConsumerInterceptor(typeof(OrderTrackingConsumerInterceptor<,>));
            builder.AddGlobalConsumerInterceptor(typeof(SecondOrderTrackingConsumerInterceptor<,>));
            builder.AddConsumer<string, string>(c =>
            {
                c.WithBootstrapServers("localhost:9092")
                    .WithGroupId("test-group");
            });
        });

        services.AddSingleton(callOrder);
        services.AddSingleton(new InterceptorName("global-1"));
        services.AddSingleton(new SecondInterceptorName("global-2"));

        var sp = services.BuildServiceProvider();
        var consumer = sp.GetRequiredService<IKafkaConsumer<string, string>>();

        var interceptors = GetConsumerInterceptors<string, string>(consumer);

        await Assert.That(interceptors).IsNotNull();
        await Assert.That(interceptors!).Count().IsEqualTo(2);

        var result = default(ConsumeResult<string, string>);
        foreach (var interceptor in interceptors)
        {
            result = interceptor.OnConsume(result);
        }

        await Assert.That(callOrder).Count().IsEqualTo(2);
        await Assert.That(callOrder[0]).IsEqualTo("global-1");
        await Assert.That(callOrder[1]).IsEqualTo("global-2");
    }

    #endregion

    #region Reflection Helpers

    private static IProducerInterceptor<TKey, TValue>[]? GetProducerInterceptors<TKey, TValue>(
        IKafkaProducer<TKey, TValue> producer)
    {
        var field = producer.GetType().GetField("_interceptors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(producer) as IProducerInterceptor<TKey, TValue>[];
    }

    private static IConsumerInterceptor<TKey, TValue>[]? GetConsumerInterceptors<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer)
    {
        var field = consumer.GetType().GetField("_interceptors",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(consumer) as IConsumerInterceptor<TKey, TValue>[];
    }

    #endregion

    #region DI-Injectable Interceptor Implementations

    // Wrapper types to differentiate multiple interceptor names in DI
    public sealed record InterceptorName(string Name);
    public sealed record SecondInterceptorName(string Name);

    // Open generic producer interceptor that can be resolved via ActivatorUtilities
    public sealed class OrderTrackingProducerInterceptor<TKey, TValue> : IProducerInterceptor<TKey, TValue>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public OrderTrackingProducerInterceptor(List<string> callOrder, InterceptorName name)
        {
            _callOrder = callOrder;
            _name = name.Name;
        }

        public ProducerMessage<TKey, TValue> OnSend(ProducerMessage<TKey, TValue> message)
        {
            _callOrder.Add(_name);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    public sealed class SecondOrderTrackingProducerInterceptor<TKey, TValue> : IProducerInterceptor<TKey, TValue>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public SecondOrderTrackingProducerInterceptor(List<string> callOrder, SecondInterceptorName name)
        {
            _callOrder = callOrder;
            _name = name.Name;
        }

        public ProducerMessage<TKey, TValue> OnSend(ProducerMessage<TKey, TValue> message)
        {
            _callOrder.Add(_name);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    // Open generic consumer interceptor
    public sealed class OrderTrackingConsumerInterceptor<TKey, TValue> : IConsumerInterceptor<TKey, TValue>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public OrderTrackingConsumerInterceptor(List<string> callOrder, InterceptorName name)
        {
            _callOrder = callOrder;
            _name = name.Name;
        }

        public ConsumeResult<TKey, TValue> OnConsume(ConsumeResult<TKey, TValue> result)
        {
            _callOrder.Add(_name);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    public sealed class SecondOrderTrackingConsumerInterceptor<TKey, TValue> : IConsumerInterceptor<TKey, TValue>
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public SecondOrderTrackingConsumerInterceptor(List<string> callOrder, SecondInterceptorName name)
        {
            _callOrder = callOrder;
            _name = name.Name;
        }

        public ConsumeResult<TKey, TValue> OnConsume(ConsumeResult<TKey, TValue> result)
        {
            _callOrder.Add(_name);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    // Non-generic interceptor used from per-instance registration
    private sealed class NamedProducerInterceptor(string name, List<string> callOrder)
        : IProducerInterceptor<string, string>
    {
        public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message)
        {
            callOrder.Add(name);
            return message;
        }

        public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
    }

    private sealed class NamedConsumerInterceptor(string name, List<string> callOrder)
        : IConsumerInterceptor<string, string>
    {
        public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result)
        {
            callOrder.Add(name);
            return result;
        }

        public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
    }

    #endregion
}
