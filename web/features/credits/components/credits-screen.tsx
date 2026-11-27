/* The official provider mark must retain its original SVG and aspect ratio. */
/* eslint-disable @next/next/no-img-element */
import styles from "./credits.module.css";

export function CreditsScreen() {
  return <div className={styles.screen}>
    <header className={styles.intro}><h1>Credits</h1><p>The people and projects behind the catalog.</p></header>
    <section className={styles.section} aria-labelledby="tmdb-credit">
      <h2 id="tmdb-credit">TMDB</h2>
      <a className={styles.logoLink} href="https://www.themoviedb.org"><img src="/branding/tmdb.svg" alt="TMDB" width="112" height="15" /></a>
      <p>Movie and television metadata and artwork are provided by <a href="https://www.themoviedb.org">The Movie Database</a>.</p>
      <p className={styles.notice}>This product uses the TMDB API but is not endorsed or certified by TMDB.</p>
    </section>
    <section className={styles.section} aria-labelledby="musicbrainz-credit">
      <h2 id="musicbrainz-credit">MusicBrainz</h2>
      <p>Album and song metadata is provided by <a href="https://musicbrainz.org">MusicBrainz</a>, an open music encyclopedia maintained by its community and the MetaBrainz Foundation.</p>
      <p>RateFreak imports selected core metadata under <a href="https://musicbrainz.org/doc/About/Data_License">CC0</a>. MusicBrainz data licensing does not cover album artwork.</p>
      <p>Album covers are delivered by the <a href="https://coverartarchive.org">Cover Art Archive</a>, a MusicBrainz and Internet Archive project. Artwork remains subject to its owners’ rights; see the <a href="https://musicbrainz.org/doc/Cover_Art_Archive">Cover Art Archive policy</a>.</p>
    </section>
    <section className={styles.section} aria-labelledby="igdb-credit">
      <h2 id="igdb-credit">IGDB</h2>
      <p>Video game metadata and artwork are provided by <a href="https://www.igdb.com">IGDB</a>.</p>
    </section>
    <section className={styles.section} aria-labelledby="openlibrary-credit">
      <h2 id="openlibrary-credit">Open Library</h2>
      <p>Book metadata and cover references are provided by <a href="https://openlibrary.org">Open Library</a>, an initiative of the Internet Archive maintained by its community.</p>
      <p>Imported books link to their original work records. Contributed descriptions and cover artwork may carry existing rights; see <a href="https://openlibrary.org/developers/licensing">Open Library’s licensing statement</a>.</p>
    </section>
    <section className={styles.section} aria-labelledby="software-credit">
      <h2 id="software-credit">Open-source software &amp; type</h2>
      <p>RateFreak is built with .NET, PostgreSQL, Next.js, React and other open-source projects. Interface icons are by Tabler. Fraunces and DM Sans are distributed under the SIL Open Font License.</p>
      <ul className={styles.links}>
        <li><a href="/notices/THIRD-PARTY-NOTICES.txt">Third-party software notices</a></li>
        <li><a href="/notices/LICENSE.txt">RateFreak MIT license</a></li>
        <li><a href="/fonts/Fraunces-OFL.txt">Fraunces license</a></li>
        <li><a href="/fonts/DM-Sans-OFL.txt">DM Sans license</a></li>
      </ul>
    </section>
    <p className={styles.rights}>Provider content and artwork remain subject to their respective rights and terms. RateFreak’s software license does not license that content.</p>
  </div>;
}
