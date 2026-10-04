// @ts-check
/** @type {import('prism-react-renderer').PrismTheme} */
const dekafPrismTheme = {
  plain: {
    color: '#ece6df',
    backgroundColor: '#1d1916',
  },
  styles: [
    {types: ['comment', 'prolog', 'doctype', 'cdata'], style: {color: '#8f857c', fontStyle: 'italic'}},
    {types: ['keyword', 'builtin', 'constant', 'boolean'], style: {color: '#e09a68'}},
    {types: ['function', 'class-name', 'attr-name'], style: {color: '#9fb6c3'}},
    {types: ['string', 'char', 'attr-value', 'number'], style: {color: '#cdb48c'}},
    {types: ['tag', 'selector', 'variable'], style: {color: '#e6c9b0'}},
    {types: ['inserted'], style: {color: '#a3c0aa'}},
    {types: ['deleted'], style: {color: '#e0a19b'}},
    {types: ['changed'], style: {color: '#cdb48c'}},
  ],
};

/**
 * Docusaurus renders a "Direct link to ..." anchor inside every heading. Converting the
 * built HTML back to Markdown would carry those into each emitted .md file and into
 * llms-full.txt, so drop them before the conversion runs.
 *
 * Hand-rolled rather than using unist-util-visit, which is only a transitive dependency.
 *
 * @returns {(tree: import('hast').Root) => void}
 */
function rehypeStripHeadingAnchors() {
  const isHashLink = (/** @type {any} */ node) =>
    node.type === 'element' &&
    node.tagName === 'a' &&
    []
      .concat(node.properties?.className ?? [])
      .includes('hash-link');

  return (tree) => {
    const walk = (/** @type {any} */ node) => {
      if (!Array.isArray(node.children)) {
        return;
      }
      node.children = node.children.filter((child) => !isHashLink(child));
      node.children.forEach(walk);
    };
    walk(tree);
  };
}

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Dekaf',
  tagline: 'Kafka in pure C#, without librdkafka',
  favicon: 'img/favicon.ico',

  url: 'https://thomhurst.github.io',
  baseUrl: '/Dekaf/',

  organizationName: 'thomhurst',
  projectName: 'Dekaf',

  headTags: [
    {
      tagName: 'link',
      attributes: {
        rel: 'icon',
        type: 'image/svg+xml',
        sizes: 'any',
        href: '/Dekaf/img/favicon.svg',
      },
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'apple-touch-icon',
        sizes: '180x180',
        href: '/Dekaf/img/apple-touch-icon.png',
      },
    },
    {
      tagName: 'meta',
      attributes: {
        name: 'algolia-site-verification',
        content: '6EB30557717AAEE5',
      },
    },
    {
      tagName: 'script',
      attributes: {},
      innerHTML: `window.tlumaConfig = {
  source: 'thomhurst/dekaf',
  theme: 'auto',
  brandColor: 'slate',
  button: 'bottom-right',
  welcomePulse: true,
  edgePadding: '1rem',
  autoOpen: false,
  desktopFullscreenByDefault: false,
};`,
    },
  ],

  scripts: [
    {
      src: 'https://tluma.ai/widget.js',
      async: true,
    },
  ],

  onBrokenLinks: 'throw',

  plugins: [
    [
      '@signalwire/docusaurus-plugin-llms-txt',
      /** @type {import('@signalwire/docusaurus-plugin-llms-txt').PluginOptions} */
      ({
        siteTitle: 'Dekaf',
        siteDescription:
          'High-performance, pure C# Apache Kafka client library for .NET. A zero-allocation implementation of the Kafka wire protocol with no librdkafka and no native interop.',
        // Categories come from URL path segments. Under /Dekaf/docs/, deeper values give
        // every root-level page its own single-entry section, so keep the index flat and
        // let each link's title and description carry the signal.
        depth: 1,
        enableDescriptions: true,
        content: {
          // Serve raw Markdown alongside each HTML page.
          enableMarkdownFiles: true,
          // Off by default; this is the single-file full corpus.
          enableLlmsFullTxt: true,
          // Emit absolute URLs. Relative links omit the /Dekaf/ baseUrl, which 404s
          // once llms.txt is fetched away from the site.
          relativePaths: false,
          includeDocs: true,
          includeBlog: false,
          includePages: false,
          beforeDefaultRehypePlugins: [rehypeStripHeadingAnchors],
        },
      }),
    ],
  ],

  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/thomhurst/Dekaf/tree/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/dekaf-social-card.png',
      navbar: {
        title: 'Dekaf',
        logo: {
          alt: 'Dekaf Logo',
          src: 'img/logo.svg',
          srcDark: 'img/logo-dark.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'tutorialSidebar',
            position: 'left',
            label: 'Documentation',
          },
          {
            href: 'https://www.nuget.org/packages/Dekaf',
            label: 'NuGet',
            position: 'right',
          },
          {
            href: 'https://github.com/thomhurst/Dekaf',
            label: 'GitHub',
            position: 'right',
          },
          {
            href: 'https://github.com/sponsors/thomhurst',
            label: '❤️ Sponsor',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {
                label: 'Getting Started',
                to: '/docs/getting-started',
              },
              {
                label: 'Producer Guide',
                to: '/docs/producer/basics',
              },
              {
                label: 'Consumer Guide',
                to: '/docs/consumer/basics',
              },
            ],
          },
          {
            title: 'More',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/thomhurst/Dekaf',
              },
              {
                label: 'NuGet',
                href: 'https://www.nuget.org/packages/Dekaf',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Tom Longhurst. Built with Docusaurus.`,
      },
      prism: {
        theme: dekafPrismTheme,
        darkTheme: dekafPrismTheme,
        additionalLanguages: ['csharp', 'bash', 'json'],
      },
    }),
};

export default config;
