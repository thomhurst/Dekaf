/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  tutorialSidebar: [
    {
      type: 'category',
      label: 'Start Here',
      items: [
        'intro',
        'getting-started',
        'compatibility',
        'migrating-from-confluent-kafka',
        'configuration/confluent-migration',
      ],
    },
    {
      type: 'category',
      label: 'Producer',
      items: [
        'producer/basics',
        'producer/topic-producer',
        'producer/batch-production',
        'producer/fire-and-forget',
        'producer/headers',
        'producer/partitioning',
        'producer/transactions',
        'producer/outbox',
      ],
    },
    {
      type: 'category',
      label: 'Consumer',
      items: [
        'consumer/basics',
        'consumer/delivery-semantics',
        'consumer/filtering-and-routing',
        'consumer/offset-management',
        'consumer/consumer-groups',
        'consumer/share-consumers',
        'consumer/partitioned-processing-api',
        'consumer/linq-extensions',
        'consumer/dead-letter-queues',
        'consumer/manual-assignment',
      ],
    },
    {
      type: 'category',
      label: 'Configuration & Hosting',
      items: [
        'configuration/presets',
        'configuration/producer-options',
        'configuration/consumer-options',
        'configuration/client-dns-lookup',
        'dependency-injection',
        'hosted-services',
      ],
    },
    {
      type: 'category',
      label: 'Serialization & Compression',
      items: [
        'serialization/built-in',
        'serialization/json',
        'serialization/schema-registry',
        'serialization/routing',
        'serialization/custom',
        'compression',
      ],
    },
    {
      type: 'category',
      label: 'Security',
      items: [
        'security/tls',
        'security/sasl',
        'security/oauth',
      ],
    },
    {
      type: 'category',
      label: 'Operations & Administration',
      items: [
        'observability',
        'admin/topic-identifiers',
        'admin/transaction-remediation',
        'admin/replica-log-directories',
        'admin/streams-group-management',
      ],
    },
    {
      type: 'category',
      label: 'Testing',
      items: [
        'testing',
        'testing/fault-injection',
      ],
    },
    {
      type: 'category',
      label: 'Performance & Reliability',
      items: [
        'performance',
        'benchmarks',
        'stress-tests',
        'soak-tests',
      ],
    },
    'api-compatibility',
  ],
};

export default sidebars;
