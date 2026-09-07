// @ts-check
// `@type` JSDoc annotations allow editor autocompletion and type checking
// (when paired with `@ts-check`).
// There are various equivalent ways to declare your Docusaurus config.
// See: https://docusaurus.io/docs/api/docusaurus-config

import {themes as prismThemes} from 'prism-react-renderer';
import rewriteRepoLinks from './src/remark/rewrite-repo-links.js';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'ConfIT',
  tagline: 'Declarative API integration testing for .NET',
  favicon: 'img/logo.svg',

  // Archivo (display), IBM Plex Sans (body), IBM Plex Mono (code/labels) -- reusing the
  // same type system as the sibling Lattice site; the color palette below is ConfIT's own.
  headTags: [
    {
      tagName: 'link',
      attributes: {rel: 'preconnect', href: 'https://fonts.googleapis.com'},
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'stylesheet',
        href: 'https://fonts.googleapis.com/css2?family=Archivo:wght@600;700;800;900&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@500;600&display=swap',
      },
    },
  ],

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://techygarg.github.io',
  // Set the /<baseUrl>/ pathname under which your site is served
  baseUrl: '/ConfIT/',

  // GitHub pages deployment config.
  organizationName: 'techygarg',
  projectName: 'ConfIT',

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  plugins: [
    [
      '@easyops-cn/docusaurus-search-local',
      /** @type {import('@easyops-cn/docusaurus-search-local').PluginOptions} */
      ({
        hashed: true,
        // Docs are read from the repo's ../doc folder (see presets.docs.path below),
        // not the Docusaurus-default website/docs -- the hasher needs the real path.
        docsDir: ['../doc'],
        docsRouteBasePath: '/docs',
        indexBlog: false, // blog is disabled (blog: false in the preset config)
        language: 'en',
        highlightSearchTermsOnTargetPage: true,
      }),
    ],
  ],

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          // Reads the repo's existing doc/ folder directly -- no copy, no second
          // source of truth. Content is edited in place, same as today.
          path: '../doc',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/techygarg/ConfIT/tree/main/doc/',
          // doc-strategy.md is an internal planning note, never meant for readers.
          // Package.Readme.md is the NuGet package description (duplicates this site's
          // own content, rendered by nuget.org instead) -- neither belongs on the site.
          exclude: ['doc-strategy.md', 'Package.Readme.md'],
          // doc/*.md keeps relative links like `../example/...` and `../skills/...` for
          // GitHub's own rendering and for the AI skills that read doc/ directly -- see
          // src/remark/rewrite-repo-links.js for why this build needs its own rewrite pass
          // rather than editing the source files.
          remarkPlugins: [rewriteRepoLinks],
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
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'ConfIT',
        logo: {
          alt: 'ConfIT logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'tutorialSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            href: 'https://www.nuget.org/packages/ConfIT/',
            label: 'NuGet',
            position: 'right',
          },
          {
            href: 'https://github.com/techygarg/ConfIT',
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
                label: 'Suite Setup',
                to: '/docs/suite-setup',
              },
              {
                label: 'Test File Format',
                to: '/docs/test-file-format',
              },
            ],
          },
          {
            title: 'Project',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/techygarg/ConfIT',
              },
              {
                label: 'NuGet',
                href: 'https://www.nuget.org/packages/ConfIT/',
              },
              {
                label: 'Changelog',
                href: 'https://github.com/techygarg/ConfIT/blob/main/CHANGELOG.md',
              },
              {
                label: 'License (MIT)',
                href: 'https://github.com/techygarg/ConfIT/blob/main/LICENSE',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} ConfIT. Built with Docusaurus.`,
      },
      // vsDark in both modes: code blocks stay consistently dark regardless of site theme.
      prism: {
        theme: prismThemes.vsDark,
        darkTheme: prismThemes.vsDark,
      },
    }),
};

export default config;
