import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { ThemeControl } from "@/components/layout/theme-control";
import shell from "@/components/layout/app-shell.module.css";
import styles from "./setup.module.css";

export function SetupShell({ children }: ChildrenProps) {
  return <div className={shell.shell}>
    <a className={shell.skipLink} href="#main-content">Skip to content</a>
    <header className={styles.header}><span className={styles.brand}>RateFreak.</span><ThemeControl /></header>
    <main id="main-content" tabIndex={-1}>{children}</main>
  </div>;
}
