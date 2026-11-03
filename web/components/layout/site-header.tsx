import Link from "next/link";
import { Suspense } from "react";
import { MobileNavigation } from "./mobile-navigation";
import { AuthNavigation } from "@/features/auth/components/auth-navigation";
import styles from "./site-header.module.css";

export function SiteHeader() {
  return (
    <header className={styles.header}>
      <Link className={styles.brand} href="/" aria-label="RateFreak home">
        RateFreak<span className={styles.dot}>.</span>
      </Link>
      <div className={styles.controls}>
        <nav aria-label="Main navigation">
          <Suspense fallback={<Link className={styles.home} href="/">Home</Link>}><AuthNavigation /></Suspense>
        </nav>
      </div>
      <MobileNavigation>
        <Suspense fallback={<Link className={styles.home} href="/">Home</Link>}><AuthNavigation /></Suspense>
      </MobileNavigation>
    </header>
  );
}
