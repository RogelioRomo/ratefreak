import styles from "./home-intro.module.css";

export function HomeIntro() {
  return (
    <>
      <section className={styles.intro} aria-labelledby="home-title">
        <p className={styles.eyebrow}><span aria-hidden="true">✳</span> FOR THE LOVE OF A GOOD FIND</p>
        <h1 id="home-title">Your taste.<br /><span>Your collection.</span></h1>
        <p className={styles.description}>The films you keep thinking about. The albums on repeat.
          The games you get lost in. A home for everything you love.</p>
        <ul className={styles.media} aria-label="Media on RateFreak">
          <li>Movies</li><li>TV</li><li>Albums</li><li>Games</li>
        </ul>
      </section>
      <section className={styles.upcoming} aria-labelledby="upcoming-title">
        <div className={styles.sectionHeading}>
          <h2 id="upcoming-title">Make it yours.</h2>
          <span className={styles.badge}>Coming soon</span>
        </div>
        <div className={styles.features}>
          <article><span className={styles.number}>01 / KEEP TRACK</span><h3>A library of your own</h3><p>Remember what you loved. Keep a place for what comes next.</p></article>
          <article><span className={styles.number}>02 / HAVE YOUR SAY</span><h3>Ratings with a point of view</h3><p>Rate your favorites and put the feeling into words with reviews.</p></article>
          <article><span className={styles.number}>03 / CONNECT THE DOTS</span><h3>Collections without limits</h3><p>A weekend watchlist. A soundtrack to a season. Bring your favorites together.</p></article>
        </div>
      </section>
    </>
  );
}
