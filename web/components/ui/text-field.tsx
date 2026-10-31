import type { TextFieldProps } from "./interfaces/text-field-props";
import styles from "./text-field.module.css";

export function TextField({ name, label, hint, errors, ...props }: TextFieldProps) {
  const id = `account-${name}`;
  const description = [hint && `${id}-hint`, errors?.length && `${id}-error`].filter(Boolean).join(" ") || undefined;
  return (
    <div className={styles.field}>
      <label htmlFor={id}>{label}</label>
      <input {...props} id={id} name={name} aria-invalid={errors?.length ? true : undefined} aria-describedby={description} />
      {hint && <p id={`${id}-hint`} className={styles.hint}>{hint}</p>}
      {!!errors?.length && <p id={`${id}-error`} className={styles.error}>{errors.join(" ")}</p>}
    </div>
  );
}
