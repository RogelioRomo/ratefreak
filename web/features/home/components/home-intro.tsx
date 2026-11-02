import Link from "next/link";
import { IconArrowUpRight } from "@tabler/icons-react";
import { HomePlans } from "./home-plans";
import { CultureArtwork } from "./culture-artwork";
import styles from "./home-intro.module.css";

export function HomeIntro() {
  return (
    <div className={styles.homepage} data-homepage>
      <section className={styles.lead} aria-labelledby="home-title">
        <div className={styles.intro}>
          <h1 id="home-title">Good taste<br /> deserves<br /> <span>a home.</span></h1>
          <p className={styles.description}>The films that linger. The records on repeat.
            The games you lose yourself in. A place for everything you’re into.</p>
          <div className={styles.actions}>
            <Link href="/register" className={styles.primary}>Create your account <IconArrowUpRight size={18} stroke={1.75} aria-hidden="true" /></Link>
            <Link href="/catalog" className={styles.secondary}>Browse the catalog <IconArrowUpRight size={16} stroke={1.75} aria-hidden="true" /></Link>
          </div>
        </div>
        <figure className={styles.visual}>
          <CultureArtwork />
          <figcaption className={styles.formats}>
            <span>Movies</span><span>Television</span><span>Music</span><span>Games</span>
          </figcaption>
        </figure>
      </section>
      <HomePlans />
    </div>
  );
}
