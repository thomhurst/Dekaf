import {useState} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

import styles from './index.module.css';

const producerCode = `using Dekaf;

await using var producer = await Kafka
    .CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .BuildAsync();

await producer.ProduceAsync(
    "greetings", "hello", "Hello, Kafka!");`;

const consumerCode = `using Dekaf;

await using var consumer = await Kafka
    .CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("greeting-reader")
    .SubscribeTo("greetings")
    .BuildAsync();

await foreach (var message in consumer.ConsumeAsync())
{
    Console.WriteLine(message.Value);
}`;

function PartitionDemo() {
  const [sent, setSent] = useState(0);
  const activePartition = sent === 0 ? null : (sent - 1) % 3;

  return (
    <figure className={styles.messageFlow}>
      <div className={styles.flowIntro}>
        <div>
          <span className={styles.flowTitle}>A place for every message.</span>
          <p>One topic. Three partitions. Keep the conversation moving.</p>
        </div>
        <button className={styles.sendButton} type="button" onClick={() => setSent(count => count + 1)}>
          Produce a message <span aria-hidden="true">+</span>
        </button>
      </div>
      <div className={styles.flowDiagram}>
        <div className={styles.producerNode} aria-hidden="true">
          <span className={styles.producerSymbol}>C#</span>
          <span>Your producer</span>
        </div>
        <div className={styles.partitionList}>
          {[0, 1, 2].map(partition => {
            const count = Math.floor((sent + 2 - partition) / 3);
            return (
              <div className={styles.partitionRow} key={partition}>
                <span className={styles.partitionLabel}>Partition {partition}</span>
                <div className={styles.records} aria-hidden="true">
                  {Array.from({length: 7}, (_, index) => (
                    <span
                      className={`${styles.record} ${sent > 0 && activePartition === partition && index === 6 ? styles.newRecord : ''}`}
                      key={count + index}>
                      <span>{String(count + index).padStart(2, '0')}</span>
                    </span>
                  ))}
                </div>
                <span className={styles.offset}>offset {count + 6}</span>
              </div>
            );
          })}
        </div>
      </div>
      <figcaption className={styles.flowCaption}>
        <span>Interactive illustration · messages distributed in turn</span>
        <span role="status" aria-live="polite" aria-atomic="true">
          {sent === 0
            ? 'Send a message to try it'
            : `Message ${sent} appended to partition ${activePartition}, offset ${Math.floor((sent + 2 - activePartition) / 3) + 6}`}
        </span>
      </figcaption>
    </figure>
  );
}

function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.container}>
        <div className={styles.heroIntro}>
          <div className={styles.heroTitle}>
            <p className={styles.productLine}>Dekaf / The pure C# Apache Kafka client</p>
            <Heading as="h1">Kafka, fluent<br />in C#.</Heading>
          </div>
          <div className={styles.heroCopy}>
            <p>All the Kafka.<br />{' '}Right at home in .NET.</p>
            <p className={styles.heroDescription}>
              Produce, consume, and work with Kafka through a fully managed client.
              Built in C#, from the fluent API to the wire protocol.
            </p>
            <Link className={styles.primaryButton} to="/docs/getting-started">Start building</Link>
            <Link className={styles.sourceLink} href="https://github.com/thomhurst/Dekaf">Explore the source</Link>
          </div>
        </div>
        <PartitionDemo />
        <div className={styles.heroFootnote}>
          <span>No native libraries to ship.</span>
          <span>No interop layer to cross.</span>
          <span>Just .NET 10+.</span>
        </div>
      </div>
    </header>
  );
}

function CodeExample() {
  return (
    <section className={`${styles.container} ${styles.codeSection}`} aria-labelledby="code-heading">
      <div className={styles.codeCopy}>
        <Heading as="h2" id="code-heading">Small API.<br />Big conversations.</Heading>
        <p>
          Start with a broker address and a topic. Fluent builders and async streams
          make the rest feel familiar.
        </p>
        <div className={styles.install}>
          <span>Get the package</span>
          <CodeBlock language="bash">dotnet add package Dekaf</CodeBlock>
        </div>
        <Link className={styles.textLink} to="/docs/getting-started">Follow the quickstart</Link>
      </div>
      <div className={styles.codeExample}>
        <Tabs aria-label="Kafka code examples">
          <TabItem value="producer" label="Producer" default>
            <CodeBlock language="csharp" title="Producer.cs">{producerCode}</CodeBlock>
          </TabItem>
          <TabItem value="consumer" label="Consumer">
            <CodeBlock language="csharp" title="Consumer.cs">{consumerCode}</CodeBlock>
          </TabItem>
        </Tabs>
        <p className={styles.codeNote}>Connect to your local Kafka broker at localhost:9092.</p>
      </div>
    </section>
  );
}

const guides = [
  {
    title: 'Send with confidence',
    description: 'From your first message to batching, idempotence, and transactions.',
    links: [
      ['Producer guide', '/docs/producer/basics'],
      ['Delivery guarantees', '/docs/producer/transactions'],
    ],
  },
  {
    title: 'Keep consumers moving',
    description: 'Read async streams, share work across groups, and take control of offsets.',
    links: [
      ['Consumer guide', '/docs/consumer/basics'],
      ['Consumer groups', '/docs/consumer/consumer-groups'],
    ],
  },
  {
    title: 'Make it your stack',
    description: 'Plug in serializers, compression, and the .NET services you already use.',
    links: [
      ['Dependency injection', '/docs/dependency-injection'],
      ['Serialization', '/docs/serialization/built-in'],
    ],
  },
];

function Guides() {
  return (
    <section className={styles.guides} aria-labelledby="guides-heading">
      <div className={styles.container}>
        <div className={styles.guideHeading}>
          <Heading as="h2" id="guides-heading">Pick up the thread.</Heading>
          <Link className={styles.textLink} to="/docs/">Browse all documentation</Link>
        </div>
        <div className={styles.guideList}>
          {guides.map(guide => (
            <article className={styles.guide} key={guide.title}>
              <Heading as="h3">{guide.title}</Heading>
              <p>{guide.description}</p>
              <ul>
                {guide.links.map(([label, to]) => <li key={to}><Link to={to}>{label}</Link></li>)}
              </ul>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function Performance() {
  return (
    <section className={`${styles.container} ${styles.performance}`} aria-labelledby="performance-heading">
      <div className={styles.performanceMark} aria-hidden="true">0 B<span>per message<br />on the hot path</span></div>
      <div className={styles.performanceCopy}>
        <Heading as="h2" id="performance-heading">Less work for the runtime.<br />More room for your messages.</Heading>
        <p>Spans, pooled buffers, and ValueTask keep allocations off the hot path.
          Explore the benchmarks and the decisions behind the performance.</p>
        <div className={styles.performanceLinks}>
          <Link className={styles.textLink} to="/docs/benchmarks">See the benchmarks</Link>
          <Link className={styles.textLink} to="/docs/performance">Performance guide</Link>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout title="Pure C# Kafka Client" description="Dekaf is a high-performance, pure C# Apache Kafka client for .NET 10+.">
      <main className={styles.home}>
        <Hero />
        <CodeExample />
        <Guides />
        <Performance />
      </main>
    </Layout>
  );
}
