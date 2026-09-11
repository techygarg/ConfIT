import Link from '@docusaurus/Link';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

export default function CreatorNote() {
  return (
    <section className={styles.section}>
      <div className={styles.inner}>
        <p className={styles.eyebrow}>A note from the creator</p>
        <Heading as="h2" className={styles.heading}>
          Why this exists
        </Heading>
        <p className={styles.quote}>
          A component test and an integration test are nearly identical in shape: same request,
          same response, same kind of assertion. They still live separately, the component test
          in your repository with the developers, the integration test wherever QA runs it, but
          ConfIT gives both sides the same way to write that shape, a JSON or YAML file instead of
          a different hand-built setup each time. Increasingly, a skill writes that file for you,
          straight from your OpenAPI spec or your controller.
        </p>
        <p className={styles.supporting}>
          Built from watching that duplicated effort happen on every API team since 2022. It
          matters even more now that the code on both sides of the test might be agent-written
          too.
        </p>
        <Link className={styles.link} to="/story">
          Why I built ConfIT, and where I want your feedback →
        </Link>
      </div>
    </section>
  );
}
