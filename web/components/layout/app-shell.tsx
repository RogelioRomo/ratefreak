import Link from "next/link";
import type { AppShellProps } from "./interfaces/app-shell-props";
import { SiteHeader } from "./site-header";
import { ThemeControl } from "./theme-control";
import styles from "./app-shell.module.css";

export function AppShell({ children, className }: AppShellProps) {
  return (
    <div className={styles.shell}>
      <a className={styles.skipLink} href="#main-content">Skip to content</a>
      <SiteHeader />
      <main id="main-content" tabIndex={-1} className={className}>{children}</main>
      <footer className={styles.footer}>
        <p>Made with <a href="https://github.com/RogelioRomo/ratefreak" target="_blank" rel="noopener noreferrer">RateFreak</a></p>
        <Link className={styles.credits} href="/credits">Credits</Link>
        <ThemeControl />
      </footer>
    </div>
  );
}
