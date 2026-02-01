/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  tutorialSidebar: [
    'intro',
    'getting-started',
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
        'consumer/offset-management',
        'consumer/consumer-groups',
        'consumer/linq-extensions',
        'consumer/manual-assignment',
      ],
    },
    {
      type: 'category',
      label: 'Configuration',
      items: [
        'configuration/presets',
        'configuration/producer-options',
        'configuration/consumer-options',
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
    'performance',
    'benchmarks',
    'stress-tests',
  ],
};

export default sidebars;
