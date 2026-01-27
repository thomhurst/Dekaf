import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <p className={styles.heroDescription}>
          A high-performance, pure C# Apache Kafka client for .NET 10+.<br />
          No JVM, no interop, no native dependencies.
        </p>
        <div className={styles.buttons}>
          <Link
            className="button button--secondary button--lg"
            to="/docs/getting-started">
            Get Started
          </Link>
          <Link
            className="button button--outline button--secondary button--lg"
            style={{marginLeft: '1rem'}}
            href="https://github.com/thomhurst/Dekaf">
            View on GitHub
          </Link>
        </div>
      </div>
    </header>
  );
}

const FeatureList = [
  {
    title: 'Pure C#',
    description: (
      <>
        No native DLLs to ship. No interop overhead. Just C# all the way down,
        from the wire protocol to the API you call. Runs anywhere .NET runs.
      </>
    ),
  },
  {
    title: 'Zero-Allocation Hot Paths',
    description: (
      <>
        We obsess over allocations so you don't have to. <code>Span&lt;T&gt;</code>,
        <code>ref struct</code>, and buffer pooling keep GC pressure out of
        your critical paths.
      </>
    ),
  },
  {
    title: 'Modern .NET',
    description: (
      <>
        Built for .NET 10+ with all the good stuff: nullable reference types,
        <code>IAsyncEnumerable</code>, <code>ValueTask</code>, and patterns
        you're already using.
      </>
    ),
  },
  {
    title: 'Simple API',
    description: (
      <>
        Fluent builders that guide you to valid configurations. Presets for
        common scenarios. You'll be productive in minutes, not hours.
      </>
    ),
  },
  {
    title: 'Full Kafka Support',
    description: (
      <>
        Transactions, idempotent producers, consumer groups, exactly-once
        semantics, all compression codecs—everything you'd expect.
      </>
    ),
  },
  {
    title: 'Pluggable',
    description: (
      <>
        Bring your own serializers, compression codecs, or Schema Registry.
        Use what makes sense for your project.
      </>
    ),
  },
];

function Feature({title, description}) {
  return (
    <div className={clsx('col col--4')}>
      <div className="padding-horiz--md padding-vert--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

function QuickExample() {
  return (
    <section className={styles.quickExample}>
      <div className="container">
        <Heading as="h2" className="text--center margin-bottom--lg">
          Quick Example
        </Heading>
        <div className="row">
          <div className="col col--6">
            <Heading as="h3">Producer</Heading>
            <pre className={styles.codeBlock}>
              <code>{`await using var producer = Dekaf
    .CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .Build();

var metadata = await producer.ProduceAsync(
    "orders",
    orderId,
    orderJson
);

Console.WriteLine($"Sent to partition {metadata.Partition}");`}</code>
            </pre>
          </div>
          <div className="col col--6">
            <Heading as="h3">Consumer</Heading>
            <pre className={styles.codeBlock}>
              <code>{`await using var consumer = Dekaf
    .CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processor")
    .SubscribeTo("orders")
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessOrderAsync(msg.Value);
}`}</code>
            </pre>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title} - Pure C# Kafka Client`}
      description="High-performance, pure C# Apache Kafka client for .NET 10+">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
        <QuickExample />
      </main>
    </Layout>
  );
}
