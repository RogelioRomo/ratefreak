import type { ButtonProps } from "./interfaces/button-props";
import styles from "./button.module.css";

export function Button({ type = "button", variant = "primary", className, ...props }: ButtonProps) {
  return <button {...props} type={type} className={[styles.button, styles[variant], className].filter(Boolean).join(" ")} />;
}
