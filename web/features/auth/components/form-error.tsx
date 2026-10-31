import { ApiError } from "@/shared/api/api-error";
import type { FormErrorProps } from "../interfaces/form-error-props";
import styles from "./auth.module.css";

export function fieldErrors(error: Error | null, name: string): string[] | undefined {
  if (!(error instanceof ApiError)) return;
  const fields = error.problem?.errors;
  const key = Object.keys(fields ?? {}).find((field) => field.toLowerCase() === name.toLowerCase());
  return key ? fields?.[key] : undefined;
}

export function FormError({ error }: FormErrorProps) {
  if (!error) return null;
  let message = "We couldn’t complete that request. Please try again.";
  if (error instanceof ApiError) {
    if (error.status === 401) message = "Email or password is incorrect.";
    if (error.status === 409) message = "That email or handle is unavailable.";
    if (error.status === 400) message = "Please check your details and try again.";
    if (error.status === 429) {
      const seconds = Number(error.retryAfter);
      message = error.retryAfter && /^\d+$/.test(error.retryAfter) && seconds > 0 && seconds <= 86400
        ? `Too many attempts. Please wait ${seconds} seconds before trying again.`
        : "Too many attempts. Please wait before trying again.";
    }
  }
  return <div className={styles.error} role="alert"><p>{message}</p></div>;
}
