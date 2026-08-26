/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  tutorialSidebar: [
    'intro',
    'getting-started',
    'migrating-from-confluent-kafka',
    'compatibility',
    'api-compatibility',
    {
      type: 'category',
      label: 'Admin',
      items: [
        'admin/topic-identifiers',
        'admin/transaction-remediation',
        'admin/replica-log-directories',
        'admin/streams-group-management',
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
        'consumer/partitioned-processing-api',
        'consumer/linq-extensions',
        'consumer/dead-letter-queues',
        'consumer/manual-assignment',
      ],
    },
    'hosted-services',
    {
      type: 'category',
      label: 'Configuration',
      items: [
        'configuration/presets',
        'configuration/producer-options',
        'configuration/consumer-options',
        'configuration/client-dns-lookup',
        'configuration/confluent-migration',
      ],
    },
    {
      type: 'category',
      label: 'Serialization',
      items: [
        'serialization/built-in',
        'serialization/json',
        'serialization/schema-registry',
        'serialization/routing',
        'serialization/custom',
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
    'compression',
    'dependency-injection',
    'observability',
    {
      type: 'category',
      label: 'Testing',
      items: [
        'testing',
        'testing/fault-injection',
      ],
    },
    'performance',
    'benchmarks',
    'stress-tests',
    {
      type: 'category',
      label: 'RFCs',
      items: ['rfcs/dekaf-streams', 'rfcs/dekaf-streams-scope'],
    },
  ],
};

export default sidebars;
