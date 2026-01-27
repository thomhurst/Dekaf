// @ts-check
import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Dekaf',
  tagline: 'Taking the Java out of Kafka',
  favicon: 'img/favicon.ico',

  url: 'https://thomhurst.github.io',
  baseUrl: '/Dekaf/',

  organizationName: 'thomhurst',
  projectName: 'Dekaf',

  onBrokenLinks: 'throw',

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
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'tutorialSidebar',
            position: 'left',
            label: 'Documentation',
          },
          {
            href: 'https://github.com/thomhurst/Dekaf',
            label: 'GitHub',
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
        copyright: `Copyright © ${new Date().getFullYear()} Dekaf. Built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'bash', 'json'],
      },
    }),
};

export default config;
