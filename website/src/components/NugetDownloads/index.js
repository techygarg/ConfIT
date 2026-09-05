import {useEffect, useState} from 'react';
import Link from '@docusaurus/Link';
import styles from './styles.module.css';

// Public, CORS-open (Access-Control-Allow-Origin: *), no auth required -- the same endpoint
// nuget.org's own frontend uses to look up a package's live download count.
const NUGET_SEARCH_URL = 'https://azuresearch-usnc.nuget.org/query?q=packageid:ConfIT&semVerLevel=2.0.0';

// Official NuGet mark (simple-icons, MIT) -- recolored via currentColor to fit the site's palette
// instead of NuGet's own brand blue.
function NugetLogo() {
  return (
    <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true">
      <path d="M1.998.342a1.997 1.997 0 1 0 0 3.995 1.997 1.997 0 0 0 0-3.995zm9.18 4.34a6.156 6.156 0 0 0-6.153 6.155v6.667c0 3.4 2.756 6.154 6.154 6.154h6.667c3.4 0 6.154-2.755 6.154-6.154v-6.667a6.154 6.154 0 0 0-6.154-6.155zm-1.477 2.8a2.496 2.496 0 1 1 0 4.993 2.496 2.496 0 0 1 0-4.993zm7.968 6.16a3.996 3.996 0 1 1-.002 7.992 3.996 3.996 0 0 1 .002-7.992z" />
    </svg>
  );
}

export default function NugetDownloads() {
  const [downloads, setDownloads] = useState(null);

  useEffect(() => {
    let cancelled = false;

    fetch(NUGET_SEARCH_URL)
      .then((res) => (res.ok ? res.json() : null))
      .then((json) => {
        const total = json?.data?.[0]?.totalDownloads;
        if (!cancelled && typeof total === 'number' && total > 0) {
          setDownloads(total);
        }
      })
      .catch(() => {
        // Ad-blocker, offline, endpoint down -- render nothing rather than a broken stat.
      });

    return () => {
      cancelled = true;
    };
  }, []);

  if (downloads === null) {
    return null;
  }

  return (
    <Link className={styles.stat} to="https://www.nuget.org/packages/ConfIT/">
      <span className={styles.icon}>
        <NugetLogo />
      </span>
      <span className={styles.count}>{downloads.toLocaleString()}</span>
      <span className={styles.label}>downloads on NuGet</span>
    </Link>
  );
}
