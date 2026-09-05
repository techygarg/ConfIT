import Heading from '@theme/Heading';
import styles from './styles.module.css';

function DeclarativeFileIcon() {
  return (
    <svg width="52" height="52" viewBox="0 0 56 56" aria-hidden="true">
      <rect x="10" y="8" width="36" height="40" rx="3" fill="var(--ifm-background-color)" stroke="var(--confit-muted)" strokeWidth="1.5" opacity="0.9" />
      <path d="M22 16 C18 16 18 20 18 22 C18 24 16 24 16 26 C16 28 18 28 18 30 C18 32 18 36 22 36" fill="none" stroke="var(--ifm-color-primary)" strokeWidth="2" strokeLinecap="round" />
      <path d="M34 16 C38 16 38 20 38 22 C38 24 40 24 40 26 C40 28 38 28 38 30 C38 32 38 36 34 36" fill="none" stroke="var(--ifm-color-primary)" strokeWidth="2" strokeLinecap="round" />
      <line x1="24" y1="42" x2="32" y2="42" stroke="var(--confit-muted)" strokeWidth="1.5" opacity="0.6" />
    </svg>
  );
}

function TwoLevelsIcon() {
  return (
    <svg width="52" height="52" viewBox="0 0 56 56" aria-hidden="true">
      <rect x="8" y="14" width="28" height="28" rx="4" fill="none" stroke="var(--confit-muted)" strokeWidth="1.5" strokeDasharray="4 3" opacity="0.75" />
      <rect x="20" y="14" width="28" height="28" rx="4" fill="none" stroke="var(--ifm-color-primary)" strokeWidth="2" />
      <circle cx="28" cy="28" r="3" fill="var(--confit-accent)" />
    </svg>
  );
}

function MatchLoopIcon() {
  return (
    <svg width="52" height="52" viewBox="0 0 56 56" aria-hidden="true">
      {/* Radius 16 loop, same construction as a feedback-loop arc: hand-rotated arrowhead
          at the arc's own tangent (-70deg) rather than a path marker. */}
      <path d="M 25.2,12.2 A 16,16 0 1 1 13,22.5" fill="none" stroke="var(--confit-accent)" strokeWidth="3" strokeLinecap="round" />
      <polygon points="0,-3.5 7,0 0,3.5" fill="var(--confit-accent)" transform="translate(13,22.5) rotate(-70)" />
      <path d="M20 30 L26 36 L38 22" fill="none" stroke="var(--ifm-color-primary)" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const FeatureList = [
  {
    title: 'Declarative DSL',
    Icon: DeclarativeFileIcon,
    description: (
      <>
        JSON or YAML test files replace hundreds of lines of C# that build a request, send it, and assert the
        response the same way every time.
      </>
    ),
  },
  {
    title: 'One Format, Two Levels',
    Icon: TwoLevelsIcon,
    description: (
      <>
        The same test file runs as a component test (mocked via WireMock) and an integration test (real services) —
        only the fixture configuration changes.
      </>
    ),
  },
  {
    title: 'Rich Matchers & Data Flow',
    Icon: MatchLoopIcon,
    description: (
      <>
        <span className={styles.inlineCode}>ignore</span>, <span className={styles.inlineCode}>pattern</span>, and{' '}
        <span className={styles.inlineCode}>semantic</span> matchers handle dynamic fields declaratively;{' '}
        <span className={styles.inlineCode}>extract</span> and <span className={styles.inlineCode}>{'{{inject}}'}</span>{' '}
        pass values between tests.
      </>
    ),
  },
];

function Feature({Icon, title, description, index}) {
  return (
    <div className={styles.card}>
      <div className={styles.cardIndex}>{String(index + 1).padStart(2, '0')} //</div>
      <Icon />
      <Heading as="h3" className={styles.cardTitle}>
        {title}
      </Heading>
      <p className={styles.cardDescription}>{description}</p>
    </div>
  );
}

export default function HomepageFeatures() {
  return (
    <section className={styles.features}>
      <div className={styles.grid}>
        {FeatureList.map((props, idx) => (
          <Feature key={idx} index={idx} {...props} />
        ))}
      </div>
    </section>
  );
}
