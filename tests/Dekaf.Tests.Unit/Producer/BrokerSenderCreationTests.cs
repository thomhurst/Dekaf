using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class BrokerSenderCreationTests
{
    [Test]
    public async Task ConcurrentCacheMiss_CreatesSingleBrokerSender()
    {
        const int workerCount = 16;
        var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        using var workersReady = new CountdownEvent(workerCount);
        using var startWorkers = new ManualResetEventSlim();
        using var creationEntered = new ManualResetEventSlim();
        using var releaseCreation = new ManualResetEventSlim();
        var creationCount = 0;
        producer.BeforeBrokerSenderCreationForTest = () =>
        {
            Interlocked.Increment(ref creationCount);
            creationEntered.Set();
            releaseCreation.Wait();
        };

        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "GetExistingOrCreateBrokerSender",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var tasks = new Task<BrokerSender>[workerCount];

        try
        {
            for (var i = 0; i < workerCount; i++)
            {
                tasks[i] = Task.Factory.StartNew(
                    () =>
                    {
                        workersReady.Signal();
                        startWorkers.Wait();
                        return (BrokerSender)method.Invoke(producer, [1])!;
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            await Assert.That(workersReady.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            startWorkers.Set();
            await Assert.That(creationEntered.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            releaseCreation.Set();

            var senders = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(creationCount).IsEqualTo(1);
            for (var i = 1; i < senders.Length; i++)
            {
                await Assert.That(senders[i]).IsSameReferenceAs(senders[0]);
            }
        }
        finally
        {
            startWorkers.Set();
            releaseCreation.Set();
            producer.BeforeBrokerSenderCreationForTest = null;
            await producer.DisposeAsync();
        }
    }
}
