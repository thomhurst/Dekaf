import {useState} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

const producerCode = `await using var producer = Kafka
    .CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .Build();

var result = await producer.ProduceAsync(
    "orders", order.Id, order);

Console.WriteLine(
    $"partition {result.Partition} · offset {result.Offset}");`;

const consumerCode = `await using var consumer = Kafka
    .CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processor")
    .SubscribeTo("orders")
    .Build();

await foreach (var message in consumer.ConsumeAsync(ct))
{
    await ProcessOrderAsync(message.Value);
}`;

function ArrowIcon() {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true">
      <path d="M3 8h10M9 4l4 4-4 4" />
    </svg>
  );
}

function GithubIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 2a10 10 0 0 0-3.16 19.49c.5.09.68-.22.68-.48v-1.86c-2.78.6-3.37-1.18-3.37-1.18-.45-1.16-1.11-1.47-1.11-1.47-.91-.62.07-.61.07-.61 1 .07 1.53 1.03 1.53 1.03.9 1.53 2.35 1.09 2.92.83.09-.65.35-1.09.64-1.34-2.22-.25-4.55-1.11-4.55-4.94 0-1.09.39-1.98 1.03-2.68-.1-.25-.45-1.27.1-2.64 0 0 .84-.27 2.75 1.02A9.57 9.57 0 0 1 12 6.84a9.6 9.6 0 0 1 2.5.34c1.9-1.29 2.74-1.02 2.74-1.02.55 1.37.2 2.39.1 2.64.64.7 1.03 1.59 1.03 2.68 0 3.84-2.34 4.68-4.57 4.93.36.31.68.92.68 1.86v2.75c0 .27.18.58.69.48A10 10 0 0 0 12 2Z" />
    </svg>
  );
}

function SignalMark({type}) {
  const paths = {
    managed: 'M5 7.5h14M5 12h14M5 16.5h9',
    memory: 'M8 5v14m8-14v14M4 9h16M4 15h16',
    api: 'm7 5-4 7 4 7m10-14 4 7-4 7M14 3l-4 18',
    kafka: 'M6 7a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm12 16a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM6 7v10m0 0a3 3 0 1 0 0 6m0-12 6 12',
    plug: 'M9 4v5m6-5v5M7 9h10v2a5 5 0 0 1-5 5v4m-3 0h6',
  };
  return (
    <span className={styles.signalMark} aria-hidden="true">
      <svg viewBox="0 0 24 24"><path d={paths[type]} /></svg>
    </span>
  );
}

function BrokerVisual() {
  return (
    <div className={styles.brokerVisual} aria-label="Messages flowing through Kafka partitions">
      <div className={styles.visualTopbar}>
        <div className={styles.windowDots}><i /><i /><i /></div>
        <span>dekaf / producer</span>
        <span className={styles.liveStatus}><i /> connected</span>
      </div>
      <div className={styles.visualBody}>
        <div className={styles.metricStrip}>
          <div><span>RUNTIME</span><strong>.NET 10</strong></div>
          <div><span>PROTOCOL</span><strong>native</strong></div>
          <div><span>HOT PATH</span><strong>0 B <small>/ msg</small></strong></div>
        </div>
        <div className={styles.streamHeader}>
          <span>TOPIC / orders</span>
          <span>PARTITIONS / 03</span>
        </div>
        <div className={styles.streams}>
          {[0, 1, 2].map((partition) => (
            <div className={styles.stream} key={partition}>
              <span className={styles.partition}>P{partition}</span>
              <div className={styles.track}>
                <i /><i /><i /><i />
              </div>
              <span className={styles.brokerNode}>B{partition + 1}</span>
            </div>
          ))}
        </div>
        <div className={styles.consoleLine}>
          <span>&gt;</span> produced <b>orders/2</b> at offset <em>481516</em><i />
        </div>
      </div>
    </div>
  );
}

function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.heroGlow} />
      <div className={styles.heroGrid} />
      <div className={`container ${styles.heroInner}`}>
        <div className={styles.heroCopy}>
          <div className={styles.eyebrow}><span>PURE C#</span> APACHE KAFKA CLIENT</div>
          <Heading as="h1">Kafka,<br /><span>fluent in C#.</span></Heading>
          <p className={styles.heroDescription}>
            A high-performance Kafka client built for modern .NET—without a JVM,
            native dependencies, or interop standing between you and the wire.
          </p>
          <div className={styles.heroActions}>
            <Link className={styles.primaryButton} to="/docs/getting-started">
              Start building <ArrowIcon />
            </Link>
            <Link className={styles.secondaryButton} href="https://github.com/thomhurst/Dekaf">
              <GithubIcon /> Explore the source
            </Link>
          </div>
          <div className={styles.proofRow}>
            <span><i /> No JVM</span>
            <span><i /> No native DLLs</span>
            <span><i /> .NET 10+</span>
          </div>
        </div>
        <div className={styles.visualWrap}>
          <span className={styles.visualLabel}>LIVE MESSAGE FLOW</span>
          <BrokerVisual />
          <div className={styles.orbitBadge}>wire<br />speed</div>
        </div>
      </div>
    </header>
  );
}

const capabilities = [
  {
    type: 'managed',
    title: 'Managed, all the way down',
    description: 'From the Kafka wire protocol to the API you call: pure C# that runs anywhere modern .NET runs.',
    className: styles.capabilityWide,
  },
  {
    type: 'memory',
    title: 'Zero-allocation hot paths',
    description: 'Span<T>, ref structs, pooled buffers, and ValueTask keep GC pressure away from message processing.',
    className: styles.capabilityAccent,
  },
  {
    type: 'api',
    title: 'An API that feels like .NET',
    description: 'Fluent builders guide valid configuration. Nullable types and async streams work exactly as you expect.',
  },
  {
    type: 'kafka',
    title: 'Kafka, fully covered',
    description: 'Transactions, idempotence, consumer groups, exactly-once semantics, headers, and every compression codec.',
  },
  {
    type: 'plug',
    title: 'Compose your own stack',
    description: 'Bring serializers, codecs, Schema Registry, dependency injection, and hosting integrations.',
  },
];

function Capabilities() {
  return (
    <section className={styles.capabilities}>
      <div className="container">
        <div className={styles.sectionIntro}>
          <div>
            <span className={styles.sectionKicker}>ENGINEERED DIFFERENTLY</span>
            <Heading as="h2">Lose the baggage.<br />Keep the protocol.</Heading>
          </div>
          <p>
            Dekaf speaks Kafka natively and embraces the strengths of the .NET runtime.
            The result is a smaller operational footprint and an API that belongs in your codebase.
          </p>
        </div>
        <div className={styles.capabilityGrid}>
          {capabilities.map((item) => (
            <article className={`${styles.capability} ${item.className || ''}`} key={item.title}>
              <SignalMark type={item.type} />
              <Heading as="h3">{item.title}</Heading>
              <p>{item.description}</p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function CodeExample() {
  const [activeTab, setActiveTab] = useState('producer');
  const code = activeTab === 'producer' ? producerCode : consumerCode;
  return (
    <section className={styles.codeSection}>
      <div className={`container ${styles.codeSectionInner}`}>
        <div className={styles.codeCopy}>
          <span className={styles.sectionKicker}>FROM ZERO TO MESSAGES</span>
          <Heading as="h2">Your first message.<br /><span>On the wire in minutes.</span></Heading>
          <p>
            Sensible presets handle the deep Kafka knowledge. Fluent builders keep every choice visible and
            let you tune when you need to.
          </p>
          <Link className={styles.textLink} to="/docs/getting-started">
            Follow the quickstart <ArrowIcon />
          </Link>
        </div>
        <div className={styles.codeWindow}>
          <div className={styles.codeToolbar}>
            <div className={styles.codeTabs} role="tablist" aria-label="Code examples">
              {['producer', 'consumer'].map((tab) => (
                <button
                  type="button"
                  role="tab"
                  aria-selected={activeTab === tab}
                  className={activeTab === tab ? styles.activeTab : ''}
                  onClick={() => setActiveTab(tab)}
                  key={tab}>
                  {tab}.cs
                </button>
              ))}
            </div>
            <span className={styles.codeLanguage}>C#</span>
          </div>
          <pre className={styles.codeBlock}><code>{code}</code></pre>
          <div className={styles.codeFooter}>
            <span><i /> ready</span>
            <span>UTF-8</span>
          </div>
        </div>
      </div>
    </section>
  );
}

function FinalCta() {
  return (
    <section className={styles.finalCta}>
      <div className="container">
        <div className={styles.ctaPanel}>
          <div className={styles.ctaSignal} aria-hidden="true"><span /><span /><span /></div>
          <div>
            <span className={styles.sectionKicker}>READY WHEN YOU ARE</span>
            <Heading as="h2">Take Java out of the equation.</Heading>
          </div>
          <Link className={styles.primaryButton} to="/docs/getting-started">
            Read the docs <ArrowIcon />
          </Link>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout
      title="Pure C# Kafka Client"
      description="Dekaf is a high-performance, pure C# Apache Kafka client for .NET 10+.">
      <Hero />
      <main>
        <Capabilities />
        <CodeExample />
        <FinalCta />
      </main>
    </Layout>
  );
}
