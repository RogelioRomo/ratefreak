import styles from "./home-plans.module.css";

export function HomePlans() {
  return (
    <section className={styles.plans} id="taking-shape" aria-labelledby="plans-title">
      <div className={styles.introduction}>
        <h2 id="plans-title">More than a list.<br /> <span>A point of view.</span></h2>
        <p className={styles.status}>A place for your interests</p>
        <p>Keep track, put your feelings into words, and make room for what comes next.</p>
      </div>
      <dl className={styles.features}>
        <div>
          <dt>Your library</dt>
          <dd>A film for Friday. An album for the walk home. Keep what you’ve
            loved close, and what’s next within reach.</dd>
        </div>
        <div>
          <dt>Your take</dt>
          <dd>Five stars or a few honest words. Make room for your own opinion
            with ratings and reviews.</dd>
        </div>
        <div>
          <dt>Your connections</dt>
          <dd>Collections are coming. A soundtrack to a season. Stories that share a feeling. Bring
            movies, shows, albums and games into collections of your own.</dd>
        </div>
      </dl>
    </section>
  );
}
