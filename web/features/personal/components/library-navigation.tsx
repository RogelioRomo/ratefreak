import Link from "next/link";
import type { LibraryNavigationProps } from "../interfaces/history";
import styles from "./history.module.css";
export function LibraryNavigation({ current }: LibraryNavigationProps) {
  return <nav aria-label="Library sections" className={styles.navigation}>
    <Link href="/library" aria-current={current === "library" ? "page" : undefined}>Library</Link>
    <Link href="/library/history" aria-current={current === "history" ? "page" : undefined}>History</Link>
  </nav>;
}
