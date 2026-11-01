"use client";

import type { ErrorStateProps } from "./interfaces/error-state-props";
import { Button } from "./button";
import styles from "./feedback.module.css";

export function ErrorState({ title = "Something went wrong.", description, onRetry }: ErrorStateProps) {
  return (
    <div className={`${styles.state} ${styles.errorState}`}>
      <div role="alert"><h2>{title}</h2>{description && <p>{description}</p>}</div>
      {onRetry && <Button variant="secondary" onClick={onRetry}>Retry</Button>}
    </div>
  );
}
