import Link from "next/link";
import { Suspense } from "react";
import { AuthNavigation } from "@/features/auth/components/auth-navigation";
import styles from "./site-header.module.css";

export function SiteHeader() {
  return (
    <header className={styles.header}>
      <Link className={styles.brand} href="/" aria-label="RateFreak home">
        <span className={styles.mark} aria-hidden="true">r<span>f</span></span>
        RateFreak<span className={styles.dot}>.</span>
      </Link>
      <nav aria-label="Main navigation">
        <Suspense fallback={<Link className={styles.home} href="/">Home</Link>}><AuthNavigation /></Suspense>
      </nav>
    </header>
  );
}
