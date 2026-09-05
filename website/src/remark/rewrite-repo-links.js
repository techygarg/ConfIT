// Docusaurus reads `doc/` in place (see docusaurus.config.js `docs.path`) so the source
// markdown must stay byte-for-byte unchanged -- the AI skills and GitHub's own rendering
// both depend on its relative links exactly as written (e.g. `../example/User.Api/...`,
// `../skills/confit-suite-setup`). Those links resolve fine on github.com (same repo) but
// would 404 on the hosted site, since nothing under `example/`, `skills/`, or `tools/` is
// part of this Docusaurus build. This plugin rewrites only the links that escape `doc/` --
// at build time, in memory -- to the equivalent github.com blob/tree URL.
//
// Anything that stays inside `doc/` (`./suite-setup.md`, `matchers-and-patterns.md`, ...) is
// left untouched; Docusaurus's own docs plugin resolves those natively.

import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
// website/src/remark -> website/src -> website -> repo root
const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const DOC_ROOT = path.join(REPO_ROOT, 'doc');
const GITHUB_REPO_URL = 'https://github.com/techygarg/ConfIT';
const GITHUB_BRANCH = 'main';

// Scheme-prefixed (http:, mailto:), pure in-page anchors, and site-absolute paths are all
// left alone. Site-absolute paths (`/ConfIT/docs/...`) show up here already resolved --
// Docusaurus's own docs-link plugin runs before this one and rewrites in-repo `./other-doc.md`
// links to routes before this plugin ever sees them.
const SKIP_URL_RE = /^([a-z][a-z0-9+.-]*:|#|\/)/i;

function visitLinks(node, visitor) {
  if (node.type === 'link') visitor(node);
  if (Array.isArray(node.children)) {
    for (const child of node.children) visitLinks(child, visitor);
  }
}

export default function rewriteRepoLinks() {
  return (tree, file) => {
    const fileDir = file.dirname ?? path.dirname(file.path);

    visitLinks(tree, (node) => {
      const url = node.url;
      if (!url || SKIP_URL_RE.test(url)) return;

      const [rawPath, hash] = url.split('#');
      if (!rawPath) return; // pure in-page anchor, e.g. "#some-heading"

      const absoluteTarget = path.resolve(fileDir, rawPath);
      const relativeToDocRoot = path.relative(DOC_ROOT, absoluteTarget);
      // '' means the link points at DOC_ROOT itself (e.g. `../doc` from a file inside doc/) --
      // that's not a page Docusaurus can route to either, so it needs rewriting too.
      const escapesDocRoot =
        relativeToDocRoot === '' ||
        relativeToDocRoot.startsWith('..') ||
        path.isAbsolute(relativeToDocRoot);
      if (!escapesDocRoot) return; // stays within doc/ -- let Docusaurus resolve it natively

      const exists = fs.existsSync(absoluteTarget);
      if (!exists) {
        // doc/*.md has pre-existing stale references (example/ was restructured after these
        // were written) -- warn loudly instead of failing the whole site build over content
        // drift that's independent of this website. Still rewritten below (best-effort blob
        // guess) so the build's own broken-link checker doesn't also trip on a dangling
        // relative path; the resulting GitHub URL will 404 until the doc content is fixed.
        console.warn(
          `[rewrite-repo-links] WARNING: broken repo-relative link "${url}" in ${file.path} -- ` +
            `no file or directory at ${absoluteTarget}`,
        );
      }

      const repoRelativePath = path
        .relative(REPO_ROOT, absoluteTarget)
        .split(path.sep)
        .join('/');
      const kind = exists && fs.statSync(absoluteTarget).isDirectory() ? 'tree' : 'blob';
      node.url = `${GITHUB_REPO_URL}/${kind}/${GITHUB_BRANCH}/${repoRelativePath}${
        hash ? `#${hash}` : ''
      }`;
    });
  };
}
