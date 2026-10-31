import type { AppShellProps } from "./interfaces/app-shell-props";
import { SiteHeader } from "./site-header";
import styles from "./app-shell.module.css";

export function AppShell({ children, className }: AppShellProps) {
  return (
    <div className={styles.shell}>
      <a className={styles.skipLink} href="#main-content">Skip to content</a>
      <SiteHeader />
      <main id="main-content" tabIndex={-1} className={className}>{children}</main>
      <footer className={styles.footer}>
        <span>RateFreak</span>
        <p>A little more you. A little less algorithm.</p>
      </footer>
    </div>
  );
}
