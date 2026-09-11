import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import NugetDownloads from '@site/src/components/NugetDownloads';
import CreatorNote from '@site/src/components/CreatorNote';

import Heading from '@theme/Heading';
import styles from './index.module.css';

function HeroDiagram() {
  return (
    <svg
      viewBox="0 0 640 210"
      width="640"
      height="210"
      className={styles.heroDiagram}
      role="img"
      aria-label="A test file flows into an HTTP call, which flows into a response match, with a green extract-and-inject loop carrying data from the match back into the next test file.">
      <defs>
        <marker id="hero-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" fill="var(--ifm-color-primary)" />
        </marker>
        <marker id="hero-arrow-accent" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" fill="var(--confit-accent)" />
        </marker>
      </defs>

      <line x1="104" y1="105" x2="306" y2="105" stroke="var(--ifm-color-primary)" strokeWidth="2" strokeDasharray="1 5" strokeLinecap="round" markerEnd="url(#hero-arrow)" />
      <line x1="334" y1="105" x2="536" y2="105" stroke="var(--ifm-color-primary)" strokeWidth="2" strokeDasharray="1 5" strokeLinecap="round" markerEnd="url(#hero-arrow)" />

      {/* extract -> inject: data from a matched response feeds the next test's request */}
      {/* The marker's tip sits 3 units past this path's own endpoint (arrowhead viewBox is
          10 units wide, refX=8, scaled by markerWidth=6 * this path's strokeWidth=2.5 / 10 =
          1.5x -- (10-8)*1.5=3), in the direction of travel. Ending the path at y=88 (not the
          box's actual top edge at y=91) puts the visible tip exactly on that edge instead of
          floating above it or burying it inside the box. */}
      <path d="M 550,92 C 550,28 90,28 90,88" fill="none" stroke="var(--confit-accent)" strokeWidth="2.5" strokeLinecap="round" markerEnd="url(#hero-arrow-accent)" />
      <text x="325" y="22" textAnchor="middle" fontFamily="IBM Plex Mono, monospace" fontSize="11" letterSpacing="0.08em" fill="var(--confit-accent)">EXTRACT → INJECT</text>

      <rect x="76" y="91" width="28" height="28" rx="6" fill="var(--confit-surface)" stroke="var(--ifm-color-primary)" strokeWidth="1.5" />
      <text x="90" y="110" textAnchor="middle" fontFamily="IBM Plex Mono, monospace" fontSize="13" fill="var(--ifm-color-primary)">{'{ }'}</text>
      <text x="90" y="142" textAnchor="middle" fontFamily="IBM Plex Mono, monospace" fontSize="11" letterSpacing="0.08em" fill="var(--confit-muted)">TEST FILE</text>

      <rect x="306" y="91" width="28" height="28" rx="6" fill="var(--confit-surface)" stroke="var(--ifm-color-primary)" strokeWidth="1.5" />
      <path d="M314 105 L327 105 M323 100 L328 105 L323 110" fill="none" stroke="var(--ifm-color-primary)" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      <text x="320" y="142" textAnchor="middle" fontFamily="IBM Plex Mono, monospace" fontSize="11" letterSpacing="0.08em" fill="var(--confit-muted)">HTTP CALL</text>

      <rect x="536" y="91" width="28" height="28" rx="6" fill="var(--confit-surface)" stroke="var(--confit-accent)" strokeWidth="1.5" />
      <path d="M 543.5,105 L 548,110 L 557,98" fill="none" stroke="var(--confit-accent)" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
      <text x="550" y="142" textAnchor="middle" fontFamily="IBM Plex Mono, monospace" fontSize="11" letterSpacing="0.08em" fill="var(--confit-muted)">MATCH</text>
    </svg>
  );
}

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={styles.heroBanner}>
      <div className={styles.eyebrow}>$ .NET integration testing_</div>
      <Heading as="h1" className={styles.title}>
        {siteConfig.tagline}
      </Heading>
      <p className={styles.subtitle}>
        Define tests in JSON or YAML — no boilerplate for the common case. ConfIT handles request execution, mock
        setup, response matching, variable extraction, and test filtering; you write the test definitions.
      </p>
      <div className={styles.buttons}>
        <Link className={styles.primaryButton} to="/docs/suite-setup">
          Get Started
        </Link>
        <Link className={styles.secondaryButton} to="https://github.com/techygarg/ConfIT">
          View on GitHub ↗
        </Link>
      </div>
      <NugetDownloads />
      <HeroDiagram />
    </header>
  );
}

export default function Home() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={siteConfig.title}
      description="ConfIT is a .NET library for declarative API integration testing -- tests are defined in JSON or YAML and executed against real or mocked HTTP services.">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
        <CreatorNote />
      </main>
    </Layout>
  );
}
