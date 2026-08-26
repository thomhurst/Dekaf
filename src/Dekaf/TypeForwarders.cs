using System.Runtime.CompilerServices;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

#pragma warning disable RS0041 // Forwarded compiler-generated record members retain shipped oblivious annotations.

[assembly: TypeForwardedTo(typeof(IInitializableKafkaClient))]
[assembly: TypeForwardedTo(typeof(TopicPartition))]
[assembly: TypeForwardedTo(typeof(TopicPartitionOffset))]
[assembly: TypeForwardedTo(typeof(TopicPartitionTimestamp))]
[assembly: TypeForwardedTo(typeof(WatermarkOffsets))]

[assembly: TypeForwardedTo(typeof(IConsumerBatchOffsetStore))]
[assembly: TypeForwardedTo(typeof(IConsumerCommitConfiguration))]
[assembly: TypeForwardedTo(typeof(IConsumerCommittedOffsets))]
[assembly: TypeForwardedTo(typeof(IConsumerLag))]
[assembly: TypeForwardedTo(typeof(IConsumerPositions))]
[assembly: TypeForwardedTo(typeof(IConsumerPartitions))]
[assembly: TypeForwardedTo(typeof(IConsumerOffsets))]
[assembly: TypeForwardedTo(typeof(ConsumerCloseOptions))]
[assembly: TypeForwardedTo(typeof(ConsumerGroupMembershipOperation))]
[assembly: TypeForwardedTo(typeof(ConsumerGroupMetadata))]
[assembly: TypeForwardedTo(typeof(OffsetCommitMode))]

[assembly: TypeForwardedTo(typeof(IKafkaProducer<,>))]
[assembly: TypeForwardedTo(typeof(IProducerMetadata))]
[assembly: TypeForwardedTo(typeof(ITopicProducer<,>))]
[assembly: TypeForwardedTo(typeof(ITransaction<,>))]
[assembly: TypeForwardedTo(typeof(ProducerMessage<,>))]
[assembly: TypeForwardedTo(typeof(TopicProducerMessage<,>))]
[assembly: TypeForwardedTo(typeof(RecordMetadata))]
[assembly: TypeForwardedTo(typeof(ProducerPartitionMetadata))]
[assembly: TypeForwardedTo(typeof(PurgeOptions))]
[assembly: TypeForwardedTo(typeof(PreparedTransactionState))]

[assembly: TypeForwardedTo(typeof(ApplicationTelemetryMetric))]
[assembly: TypeForwardedTo(typeof(ApplicationTelemetryMetricKind))]

[assembly: TypeForwardedTo(typeof(ISerializer<>))]
[assembly: TypeForwardedTo(typeof(IDeserializer<>))]
[assembly: TypeForwardedTo(typeof(ISerde<>))]
[assembly: TypeForwardedTo(typeof(IAsyncSerializer<>))]
[assembly: TypeForwardedTo(typeof(IAsyncDeserializer<>))]
[assembly: TypeForwardedTo(typeof(IAsyncSerde<>))]
[assembly: TypeForwardedTo(typeof(IAsyncSerializerPreparer<>))]
[assembly: TypeForwardedTo(typeof(IAsyncDeserializerPreparer<>))]
[assembly: TypeForwardedTo(typeof(IAsyncDeserializerPreparationRequirement))]
[assembly: TypeForwardedTo(typeof(SerializationContext))]
[assembly: TypeForwardedTo(typeof(SerializationComponent))]
[assembly: TypeForwardedTo(typeof(Headers))]
[assembly: TypeForwardedTo(typeof(Header))]
[assembly: TypeForwardedTo(typeof(Ignore))]

#pragma warning restore RS0041
