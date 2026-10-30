import styles from "./page.module.css";

export default function Home() {
  return (
    <main className={styles.main}>
      <p className={styles.eyebrow}>RateFreak</p>
      <h1>Your taste. Your collection.</h1>
      <p className={styles.description}>
        A home for the movies, shows, and albums you love.
        Track your library, share your reviews, and make collections your own.
      </p>
      <p className={styles.status}>The web app is taking shape. Come back soon.</p>
    </main>
  );
}
