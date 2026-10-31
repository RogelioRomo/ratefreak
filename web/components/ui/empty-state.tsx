import type { EmptyStateProps } from "./interfaces/empty-state-props";
import styles from "./feedback.module.css";

export function EmptyState({ title, description }: EmptyStateProps) {
  return (
    <div className={styles.state}>
      <h2>{title}</h2>
      {description && <p>{description}</p>}
    </div>
  );
}
