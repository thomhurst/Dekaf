/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  tutorialSidebar: [
    'intro',
    'getting-started',
    'compatibility',
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
      ],
    },
    {
      type: 'category',
      label: 'Consumer',
      items: [
        'consumer/basics',
        'consumer/delivery-semantics',
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
      ],
    },
    {
      type: 'category',
      label: 'Serialization',
      items: [
        'serialization/built-in',
        'serialization/json',
        'serialization/schema-registry',
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
    'testing',
    'performance',
    'benchmarks',
    'stress-tests',
  ],
};

export default sidebars;
