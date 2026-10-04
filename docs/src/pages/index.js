import {useEffect, useState} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';

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

/*
 * Each hero lane is one partition. Records get deterministic widths and tones so the
 * server render and the client render agree. The track holds the sequence twice and
 * slides by half its width, which loops without a seam.
 */
const lanes = [
  {partition: 0, seconds: 90, base: 48112, rate: 1},
  {partition: 1, seconds: 70, base: 33071, rate: 2},
  {partition: 2, seconds: 110, base: 51204, rate: 1},
  {partition: 3, seconds: 80, base: 29988, rate: 2},
];

function makeRecords(seed, count = 44) {
  let state = seed * 7919 + 104729;
  const next = () => {
    state = (state * 9301 + 49297) % 233280;
    return state / 233280;
  };
  return Array.from({length: count}, () => {
    const roll = next();
    const tone = roll < 0.12 ? 'hot' : roll < 0.26 ? 'caramel' : roll < 0.38 ? 'steel' : 'dim';
    return {tone, width: 14 + Math.round(next() * 72)};
  });
}

function useTickingOffsets() {
  const [ticks, setTicks] = useState(0);
  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return undefined;
    }
    const id = window.setInterval(() => setTicks(value => value + 1), 900);
    return () => window.clearInterval(id);
  }, []);
  return ticks;
}

function Lane({lane, ticks}) {
  const records = makeRecords(lane.partition + 1);
  return (
    <div className={styles.lane} style={{'--lane-seconds': `${lane.seconds}s`}}>
      <div className={styles.laneTrack}>
        {[...records, ...records].map((record, index) => (
          <span className={`${styles.record} ${styles[record.tone]}`} style={{width: record.width}} key={index} />
        ))}
      </div>
      <span className={styles.laneLabel}>
        <span>p{lane.partition}</span>
        <span>{(lane.base + ticks * lane.rate).toLocaleString('en-US')}</span>
      </span>
    </div>
  );
}

const droppedWord = 'librdkafka';

function Hero() {
  const ticks = useTickingOffsets();
  return (
    <header className={styles.hero}>
      <div className={styles.lanes} aria-hidden="true">
        {lanes.slice(0, 2).map(lane => <Lane lane={lane} ticks={ticks} key={lane.partition} />)}
      </div>

      <div className={`${styles.container} ${styles.heroBody}`}>
        <Heading as="h1" className={styles.headline}>
          Kafka in pure C#,
          <br />without{' '}
          <span className={styles.dropped}>
            {droppedWord}
            <span className={styles.droppedLetters} aria-hidden="true">
              {[...droppedWord].map((letter, index) => (
                <span
                  className={styles.droppedLetter}
                  style={{'--i': index, '--tx': `${(0.2 + index * 0.32).toFixed(2)}em`, '--ty': `${(2.5 + (index % 3) * 0.12).toFixed(2)}em`, '--turn': `${index % 2 ? 9 : -9}deg`}}
                  key={index}>
                  {letter}
                </span>
              ))}
            </span>
          </span>.
        </Heading>
        <div className={styles.heroFoot}>
          <p className={styles.heroDescription}>
            Dekaf is an Apache Kafka client for .NET. Unlike Confluent.Kafka, which wraps the
            native librdkafka library, it implements the Kafka wire protocol in C#, so there is
            nothing native to ship and no interop layer to cross.
          </p>
          <div className={styles.heroActions}>
            <Link className={styles.primaryButton} to="/docs/getting-started">Start building</Link>
            <code className={styles.installChip}>dotnet add package Dekaf</code>
          </div>
        </div>
      </div>

      <div className={styles.lanes} aria-hidden="true">
        {lanes.slice(2).map(lane => <Lane lane={lane} ticks={ticks} key={lane.partition} />)}
      </div>
    </header>
  );
}

const guides = [
  {
    title: 'Send with confidence',
    description: 'Batching, idempotence, and transactions.',
    links: [
      ['Producer guide', '/docs/producer/basics'],
      ['Delivery guarantees', '/docs/producer/transactions'],
    ],
  },
  {
    title: 'Keep consumers moving',
    description: 'Consumer groups, rebalances, and offset control.',
    links: [
      ['Consumer guide', '/docs/consumer/basics'],
      ['Consumer groups', '/docs/consumer/consumer-groups'],
    ],
  },
  {
    title: 'Make it your stack',
    description: 'Serializers, compression, and the .NET services you already use.',
    links: [
      ['Dependency injection', '/docs/dependency-injection'],
      ['Serialization', '/docs/serialization/built-in'],
    ],
  },
];

function Entry({offset, title, children, id}) {
  return (
    <section className={styles.entry} aria-labelledby={id}>
      <div className={styles.entryOffset} aria-hidden="true">
        <span className={styles.entryMarker} />
        offset {offset}
      </div>
      <div className={styles.entryBody}>
        <Heading as="h2" id={id}>{title}</Heading>
        {children}
      </div>
    </section>
  );
}

function ReadTheLog() {
  return (
    <div className={`${styles.container} ${styles.log}`}>
      <Entry offset={0} title="Add the package." id="install-heading">
        <div className={styles.entrySplit}>
          <p>One NuGet package with assets for .NET 10, .NET 8, and .NET Standard 2.0. Nothing
            native ships with it.</p>
          <CodeBlock language="bash">dotnet add package Dekaf</CodeBlock>
        </div>
      </Entry>
      <Entry offset={1} title="Produce a message." id="produce-heading">
        <div className={styles.entrySplit}>
          <p>Build a producer from a broker address, then await the broker's acknowledgement.
            Fluent builders guide you to a valid configuration.</p>
          <CodeBlock language="csharp" title="Producer.cs">{producerCode}</CodeBlock>
        </div>
      </Entry>
      <Entry offset={2} title="Read it back." id="consume-heading">
        <div className={styles.entrySplit}>
          <p>Join a consumer group and read records as an async stream with
            {' '}<code>await foreach</code>.</p>
          <CodeBlock language="csharp" title="Consumer.cs">{consumerCode}</CodeBlock>
        </div>
      </Entry>
      <Entry offset={3} title="Keep reading." id="guides-heading">
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
        <Link className={styles.textLink} to="/docs/">Browse all documentation</Link>
      </Entry>
    </div>
  );
}

const contents = [
  ['librdkafka', 'Not included'],
  ['Native libraries', 'None'],
  ['P/Invoke interop', 'None'],
];

function Performance() {
  return (
    <section className={`${styles.container} ${styles.performance}`} aria-labelledby="performance-heading">
      <dl className={styles.contentsLabel} aria-label="What's in Dekaf">
        <div className={styles.labelName}>
          <dt>Dekaf</dt>
          <dd>100% C# Kafka client</dd>
        </div>
        <div className={styles.labelLead}>
          <dt>Allocations per message<span>on the hot path</span></dt>
          <dd>0 B</dd>
        </div>
        {contents.map(([name, value]) => (
          <div className={styles.labelRow} key={name}>
            <dt>{name}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      <div className={styles.performanceCopy}>
        <Heading as="h2" id="performance-heading">Less work for the runtime. More room for your messages.</Heading>
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
    <Layout title="Pure C# Kafka Client" description="Dekaf is a high-performance, pure C# Apache Kafka client for .NET.">
      <main className={`${styles.home} dekaf-home`}>
        <Hero />
        <ReadTheLog />
        <Performance />
      </main>
    </Layout>
  );
}
