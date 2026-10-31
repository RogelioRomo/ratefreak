import type { LoadingStateProps } from "./interfaces/loading-state-props";
import styles from "./feedback.module.css";

export function LoadingState({ label = "Loading…" }: LoadingStateProps) {
  return <div className={styles.state} role="status"><p>{label}</p></div>;
}
