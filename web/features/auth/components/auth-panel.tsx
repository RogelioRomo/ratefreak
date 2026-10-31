import type { AuthPanelProps } from "../interfaces/auth-panel-props";
import styles from "./auth.module.css";

export function AuthPanel({ title, description, children }: AuthPanelProps) {
  return (
    <section className={styles.panel}>
      <header className={styles.heading}><p className={styles.eyebrow}>YOUR CORNER OF RATEFREAK</p><h1>{title}</h1><p>{description}</p></header>
      {children}
    </section>
  );
}
