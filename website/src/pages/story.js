import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import styles from './story.module.css';

export default function Story() {
  return (
    <Layout
      title="Why I Built ConfIT"
      description="Component tests and integration tests are nearly identical in structure, differing only in what's real underneath. ConfIT gives both the same DSL to describe that structure, instead of two different ways of writing tests.">
      <main className={styles.storyMain}>
        <article className={styles.story}>
          <p className={styles.eyebrow}>A note from the creator</p>
          <Heading as="h1" className={styles.title}>
            Why I built ConfIT
          </Heading>
          <p className={styles.byline}>Rahul, creator of ConfIT</p>

          <div className={styles.prose}>
            <p>
              I wrote the first version of ConfIT in 2022, after watching the same thing happen on
              every API-heavy team I worked on: a component test and an integration test are
              nearly identical in structure, described twice. A developer writes one, running
              in-process against mocks, gated in CI before merge. QA writes the other against a real, deployed
              environment: same endpoint, same request shape, same kind of assertion, but usually in
              a completely different tool, a Postman collection, a separate framework, sometimes a
              separate repository. Two people solve the same problem twice, and the two versions
              drift the moment either one changes.
            </p>
            <p>
              So I pulled the part that's identical, method, path, headers, body, expected
              response, how to match it, out of code and into a file. A JSON test definition, and
              now YAML too, is the same DSL whether WireMock is standing behind the endpoint or the
              real service is. The two suites still live apart; what they share now is one format,
              not two different ways of writing the same behavior.
            </p>
            <p>
              That was the pitch in 2022: stop reinventing the same test-writing problem on both
              sides. It's a sharper pitch now, for a different reason. A skill that understands the
              DSL can read your OpenAPI spec, or just the controller, and write the JSON or YAML
              directly, no code, nothing to hand-maintain, a file that's easy to read in review.
              That's a fundamentally cheaper problem to solve than asking an AI to generate and
              maintain imperative test code, where there's scaffolding to get subtly wrong and
              assertion logic to hallucinate.
            </p>
            <p>
              And the code being tested is increasingly agent-written too, which changes what a
              test is even for. A unit test written by the same model, in the same pass, that wrote
              the implementation isn't an independent check: a wrong assumption baked into the code
              tends to get baked into the test verifying it, so the two agree with each other while
              being wrong together. A black-box API test doesn't know or care how the endpoint is
              implemented, or even what language it's written in, only what the contract says
              should happen. That independence is worth more when the code on the other side of the
              wire was written by something that can be confidently, fluently wrong.
            </p>
            <p>
              That's what the three agent skills in this repository are for:{' '}
              <code>confit-suite-setup</code> wires up a suite, <code>confit-component-tests</code>{' '}
              reads a controller and the mocks behind it and writes the developer-side spec,{' '}
              <code>confit-integration-tests</code> reads a spec, a collection, or a live endpoint
              and writes the QA-side one. Same DSL, two personas, because, as the skills doc puts
              it, the same lifecycle scenario comes out to 89 lines as a component test and 64 as an
              integration test, different only by a <code>mock:</code> block. Writing that by hand,
              twice, in two different frameworks, was already wasted effort in 2022. Having a skill
              write each version directly, from the controller on one side and the spec or a live
              endpoint on the other, is what makes fixing that finally cheap enough to stick.
            </p>
          </div>

          <section className={styles.feedbackSection}>
            <Heading as="h2" className={styles.sectionTitle}>
              Where I want your feedback
            </Heading>
            <p>I'm more interested in the seams than the happy path:</p>
            <ul className={styles.feedbackList}>
              <li>
                Does the shared DSL actually hold once your API gets weird (deep nesting, GraphQL,
                pagination, non-JSON bodies), or does it start fighting you where a hand-written
                test wouldn't?
              </li>
              <li>
                Does splitting authoring by persona, developer versus QA, match how your team
                actually works, or does one side end up owning both anyway?
              </li>
              <li>
                When an agent writes the spec from your controller or your spec and collection, do
                you trust the result enough to merge it after a light read, or are you re-deriving it
                by hand regardless?
              </li>
              <li>
                If you're driving this from a different agent host than Claude Code, does the plugin
                model feel native there, or does it feel ported?
              </li>
            </ul>
            <p>
              I'd rather hear "this DSL doesn't hold for X" than "looks good." Tell me on{' '}
              <Link to="https://github.com/techygarg/ConfIT/discussions">GitHub Discussions</Link>.
              That's where I'm watching.
            </p>
          </section>

          <section className={styles.tryItSection}>
            <Heading as="h2" className={styles.sectionTitle}>
              One thing to try
            </Heading>
            <p className={styles.tryItLead}>Before you write the next test by hand</p>
            <p>
              Install the plugin, point <code>confit-component-tests</code> at a controller you
              already have, and let it read the mocks behind it:
            </p>
            <pre className={styles.codeBlock}>
              <code>{`/plugin marketplace add techygarg/ConfIT\n/plugin install confit@confit`}</code>
            </pre>
            <p>
              Compare what it writes to what you would have written yourself, not to check whether
              it's perfect, but to see where the gap is. That gap is the actual feedback I need.
            </p>
            <p>Try it on an endpoint you actually own, not a toy, and tell me where it breaks.</p>
            <p className={styles.signoff}>Rahul</p>
          </section>

          <div className={styles.actionLinks}>
            <Link className={styles.primaryButton} to="https://github.com/techygarg/ConfIT/discussions">
              Give feedback on Discussions ↗
            </Link>
            <Link className={styles.secondaryLink} to="/docs/ai-skills">
              See how the skills work
            </Link>
            <Link className={styles.secondaryLink} to="https://github.com/techygarg/ConfIT">
              Browse the repo ↗
            </Link>
          </div>
        </article>
      </main>
    </Layout>
  );
}
