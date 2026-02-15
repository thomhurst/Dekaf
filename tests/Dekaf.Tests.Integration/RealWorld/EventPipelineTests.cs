using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for event-driven pipeline patterns that are common in microservice architectures.
/// These simulate real workflows: consume from input, transform, produce to output.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<RealWorldMessagingLimit>]
public sealed class EventPipelineTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Pipeline_ConsumeTransformProduce_MessagesFlowThroughPipeline()
    {
        // Simulate: OrderService -> order-events -> EnrichmentService -> enriched-orders -> AnalyticsService
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();

        await using var sourceProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("order-service")
            .BuildAsync();

        // Produce source events
        for (var i = 0; i < 5; i++)
        {
            await sourceProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = $"order-{i}",
                Value = $"{{\"orderId\":{i},\"amount\":{(i + 1) * 10.50}}}"
            });
        }

        // Pipeline worker: consume, transform, produce
        await using var pipelineConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"enrichment-service-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        await using var pipelineProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("enrichment-service")
            .BuildAsync();

        pipelineConsumer.Subscribe(inputTopic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var processedCount = 0;

        await foreach (var msg in pipelineConsumer.ConsumeAsync(cts.Token))
        {
            // Transform: enrich with processing metadata
            var enrichedValue = $"{{\"original\":{msg.Value},\"processedAt\":\"{DateTimeOffset.UtcNow:O}\"}}";
            var headers = new Headers { { "source-topic", inputTopic } };

            await pipelineProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Key = msg.Key,
                Value = enrichedValue,
                Headers = headers
            });

            processedCount++;
            if (processedCount >= 5)
            {
                await pipelineConsumer.CommitAsync();
                break;
            }
        }

        // Verify output topic has all transformed messages
        await using var outputConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"analytics-service-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        outputConsumer.Subscribe(outputTopic);

        var outputMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in outputConsumer.ConsumeAsync(cts2.Token))
        {
            outputMessages.Add(msg);
            if (outputMessages.Count >= 5) break;
        }

        await Assert.That(outputMessages).Count().IsEqualTo(5);
        foreach (var msg in outputMessages)
        {
            await Assert.That(msg.Value).Contains("processedAt");
            await Assert.That(msg.Value).Contains("original");
            await Assert.That(msg.Headers).IsNotNull();

            var sourceHeader = msg.Headers!.First(h => h.Key == "source-topic");
            await Assert.That(sourceHeader.GetValueAsString()).IsEqualTo(inputTopic);
        }
    }

    [Test]
    public async Task Pipeline_DeadLetterQueue_FailedMessagesRouteToDeadLetterTopic()
    {
        // Simulate: process messages, route failures to DLQ
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var dlqTopic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce mix of valid and "invalid" messages
        var validMessages = new[] { "valid-1", "valid-2", "valid-3" };
        var invalidMessages = new[] { "INVALID:bad-data-1", "INVALID:bad-data-2" };

        foreach (var msg in validMessages.Concat(invalidMessages))
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = msg.StartsWith("INVALID", StringComparison.Ordinal) ? "bad-key" : "good-key",
                Value = msg
            });
        }

        // Consumer with DLQ routing
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"processor-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        await using var dlqProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        consumer.Subscribe(inputTopic);

        var successCount = 0;
        var dlqCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Value.StartsWith("INVALID", StringComparison.Ordinal))
            {
                // Route to DLQ with error metadata
                await dlqProducer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = dlqTopic,
                    Key = msg.Key,
                    Value = msg.Value,
                    Headers = new Headers
                    {
                        { "error-reason", "Message failed validation" },
                        { "original-topic", inputTopic },
                        { "original-offset", msg.Offset.ToString() }
                    }
                });
                dlqCount++;
            }
            else
            {
                successCount++;
            }

            if (successCount + dlqCount >= 5)
            {
                await consumer.CommitAsync();
                break;
            }
        }

        await Assert.That(successCount).IsEqualTo(3);
        await Assert.That(dlqCount).IsEqualTo(2);

        // Verify DLQ messages
        await using var dlqConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"dlq-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        dlqConsumer.Subscribe(dlqTopic);

        var dlqMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in dlqConsumer.ConsumeAsync(cts2.Token))
        {
            dlqMessages.Add(msg);
            if (dlqMessages.Count >= 2) break;
        }

        await Assert.That(dlqMessages).Count().IsEqualTo(2);
        foreach (var msg in dlqMessages)
        {
            await Assert.That(msg.Value).StartsWith("INVALID");
            await Assert.That(msg.Headers).IsNotNull();

            var errorReason = msg.Headers!.First(h => h.Key == "error-reason");
            await Assert.That(errorReason.GetValueAsString()).IsEqualTo("Message failed validation");
        }
    }

    [Test]
    public async Task Pipeline_MessageEnrichment_HeadersAccumulateThroughStages()
    {
        // Simulate multi-stage pipeline where each stage adds headers
        var stage1Topic = await KafkaContainer.CreateTestTopicAsync();
        var stage2Topic = await KafkaContainer.CreateTestTopicAsync();
        var stage3Topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Stage 1: Original event with correlation ID
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = stage1Topic,
            Key = "event-1",
            Value = "original-payload",
            Headers = new Headers
            {
                { "correlation-id", Guid.NewGuid().ToString() },
                { "stage", "ingestion" }
            }
        });

        // Stage 2: Consume from stage 1, add enrichment headers, produce to stage 2
        await using var stage1Consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"stage2-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        stage1Consumer.Subscribe(stage1Topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stage1Msg = await stage1Consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);
        await Assert.That(stage1Msg).IsNotNull();

        // Forward with additional headers
        var enrichedHeaders = new Headers(stage1Msg!.Value.Headers!);
        enrichedHeaders.Add("processed-by", "stage-2-enrichment");
        enrichedHeaders.Add("stage", "enrichment");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = stage2Topic,
            Key = stage1Msg.Value.Key,
            Value = $"enriched:{stage1Msg.Value.Value}",
            Headers = enrichedHeaders
        });

        // Stage 3: Consume from stage 2, add final headers
        await using var stage2Consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"stage3-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        stage2Consumer.Subscribe(stage2Topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stage2Msg = await stage2Consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);
        await Assert.That(stage2Msg).IsNotNull();

        var finalHeaders = new Headers(stage2Msg!.Value.Headers!);
        finalHeaders.Add("processed-by", "stage-3-output");
        finalHeaders.Add("stage", "output");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = stage3Topic,
            Key = stage2Msg.Value.Key,
            Value = $"final:{stage2Msg.Value.Value}",
            Headers = finalHeaders
        });

        // Verify final message has accumulated headers from all stages
        await using var finalConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        finalConsumer.Subscribe(stage3Topic);

        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var finalMsg = await finalConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts3.Token);
        await Assert.That(finalMsg).IsNotNull();

        var headers = finalMsg!.Value.Headers!;
        await Assert.That(headers.Where(h => h.Key == "stage").Count()).IsEqualTo(3);
        await Assert.That(headers.Where(h => h.Key == "processed-by").Count()).IsEqualTo(2);
        await Assert.That(headers.FirstOrDefault(h => h.Key == "correlation-id").Key).IsNotNull();
        await Assert.That(finalMsg.Value.Value).IsEqualTo("final:enriched:original-payload");
    }

    [Test]
    public async Task Pipeline_MultiTopicAggregation_CombinesEventsFromMultipleSources()
    {
        // Simulate: aggregate events from multiple source topics into a single stream
        var orderTopic = await KafkaContainer.CreateTestTopicAsync();
        var paymentTopic = await KafkaContainer.CreateTestTopicAsync();
        var shipmentTopic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce events to different source topics
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = orderTopic,
            Key = "order-100",
            Value = "order-created"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = paymentTopic,
            Key = "payment-100",
            Value = "payment-received"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = shipmentTopic,
            Key = "shipment-100",
            Value = "shipment-dispatched"
        });

        // Aggregating consumer subscribes to all three topics
        await using var aggregator = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"aggregator-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        aggregator.Subscribe(orderTopic, paymentTopic, shipmentTopic);

        var events = new List<(string Topic, string Key, string Value)>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in aggregator.ConsumeAsync(cts.Token))
        {
            events.Add((msg.Topic, msg.Key!, msg.Value));
            if (events.Count >= 3) break;
        }

        await Assert.That(events).Count().IsEqualTo(3);
        var topics = events.Select(e => e.Topic).Distinct().ToList();
        await Assert.That(topics).Count().IsEqualTo(3);
        await Assert.That(events.Any(e => e.Value == "order-created")).IsTrue();
        await Assert.That(events.Any(e => e.Value == "payment-received")).IsTrue();
        await Assert.That(events.Any(e => e.Value == "shipment-dispatched")).IsTrue();
    }

    [Test]
    public async Task Pipeline_FilterAndRoute_MessagesRoutedByContent()
    {
        // Simulate: consume events, route to different output topics based on content
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var highPriorityTopic = await KafkaContainer.CreateTestTopicAsync();
        var lowPriorityTopic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce mixed-priority events
        await producer.ProduceAsync(inputTopic, "evt-1", "HIGH:critical-alert");
        await producer.ProduceAsync(inputTopic, "evt-2", "LOW:info-log");
        await producer.ProduceAsync(inputTopic, "evt-3", "HIGH:error-alert");
        await producer.ProduceAsync(inputTopic, "evt-4", "LOW:debug-log");

        // Router consumer
        await using var router = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"router-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        await using var routerProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        router.Subscribe(inputTopic);

        var routed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in router.ConsumeAsync(cts.Token))
        {
            var targetTopic = msg.Value.StartsWith("HIGH:", StringComparison.Ordinal) ? highPriorityTopic : lowPriorityTopic;
            await routerProducer.ProduceAsync(targetTopic, msg.Key, msg.Value);
            routed++;
            if (routed >= 4) break;
        }

        // Verify high priority topic
        await using var highConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"high-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        highConsumer.Subscribe(highPriorityTopic);

        var highMessages = new List<string>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in highConsumer.ConsumeAsync(cts2.Token))
        {
            highMessages.Add(msg.Value);
            if (highMessages.Count >= 2) break;
        }

        await Assert.That(highMessages).Count().IsEqualTo(2);
        foreach (var msg in highMessages)
        {
            await Assert.That(msg).StartsWith("HIGH:");
        }

        // Verify low priority topic
        await using var lowConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"low-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        lowConsumer.Subscribe(lowPriorityTopic);

        var lowMessages = new List<string>();
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in lowConsumer.ConsumeAsync(cts3.Token))
        {
            lowMessages.Add(msg.Value);
            if (lowMessages.Count >= 2) break;
        }

        await Assert.That(lowMessages).Count().IsEqualTo(2);
        foreach (var msg in lowMessages)
        {
            await Assert.That(msg).StartsWith("LOW:");
        }
    }

    [Test]
    public async Task Pipeline_BatchCollectAndForward_AggregatesBeforeProducing()
    {
        // Simulate: collect N messages, aggregate, then produce a single summary
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var summaryTopic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce individual metric events
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Key = "sensor-1",
                Value = $"{(i + 1) * 5}" // temperatures: 5, 10, 15, ...50
            });
        }

        // Batch aggregator
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"aggregator-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(inputTopic);

        var batch = new List<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            batch.Add(int.Parse(msg.Value));

            if (batch.Count >= 10)
            {
                // Produce aggregated summary
                var avg = batch.Average();
                var min = batch.Min();
                var max = batch.Max();

                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = summaryTopic,
                    Key = "sensor-1-summary",
                    Value = $"{{\"avg\":{avg},\"min\":{min},\"max\":{max},\"count\":{batch.Count}}}",
                    Headers = new Headers { { "batch-size", batch.Count.ToString() } }
                });
                break;
            }
        }

        // Verify summary
        await using var summaryConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"summary-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        summaryConsumer.Subscribe(summaryTopic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var summary = await summaryConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        await Assert.That(summary).IsNotNull();
        await Assert.That(summary!.Value.Key).IsEqualTo("sensor-1-summary");
        await Assert.That(summary.Value.Value).Contains("\"avg\":27.5");
        await Assert.That(summary.Value.Value).Contains("\"min\":5");
        await Assert.That(summary.Value.Value).Contains("\"max\":50");
        await Assert.That(summary.Value.Value).Contains("\"count\":10");
    }
}
