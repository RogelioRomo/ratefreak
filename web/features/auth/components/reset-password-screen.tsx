"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { ApiError } from "@/shared/api/api-error";
import { useSession } from "../hooks/use-session";
import { useSensitiveMutation } from "../hooks/use-sensitive-mutation";
import type { ResetPasswordRequest } from "../interfaces/reset-password-request";
import { resetPassword } from "../api/recovery";
import { AuthPanel } from "./auth-panel";
import { AuthEntry } from "./auth-entry";
import { RecoveryCodeDisplay } from "./recovery-code-display";
import { fieldErrors } from "./form-error";
import styles from "./auth.module.css";

export function ResetPasswordScreen() {
  return <AuthPanel title="Find your way back." description="Use your saved recovery code to choose a new password."><ResetForm /></AuthPanel>;
}
function ResetForm() {
  const session = useSession();
  const [code, setCode] = useState<string | null>(null);
  const [confirmationError, setConfirmationError] = useState(false);
  const mutation = useSensitiveMutation<ResetPasswordRequest>(input => session.changeIdentity(async () => {
    const result = await resetPassword(input);
    setCode(result.recoveryCode);
    return null;
  }));
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (mutation.isPending || session.isTransitioning) return;
    const form = event.currentTarget; const data = new FormData(form);
    if (data.get("password") !== data.get("confirmPassword")) { setConfirmationError(true); return; }
    setConfirmationError(false);
    const input = { handle: String(data.get("handle") ?? ""), recoveryCode: String(data.get("recoveryCode") ?? ""), password: String(data.get("password") ?? "") };
    for (const name of ["password", "confirmPassword", "recoveryCode"]) (form.elements.namedItem(name) as HTMLInputElement).value = "";
    void mutation.submit(input).catch(() => {});
  }
  if (code) return <RecoveryCodeDisplay code={code} title="Password reset. Save your new code." doneLabel="Continue to login" onDone={() => setCode(null)} />;
  if (mutation.isSuccess) return <div className={styles.success}><h2>Your password is reset.</h2><p>All devices have been signed out.</p><Link href="/login">Log in</Link></div>;
  const uncertain = mutation.error && (!(mutation.error instanceof ApiError) || mutation.error.status >= 500);
  return <AuthEntry><form aria-label="Reset password" className={styles.form} onSubmit={submit}>
    <p>Enter your public handle and the code you saved from RateFreak. We don’t send password reset emails.</p>
    {confirmationError && <p role="alert" className={styles.error}>Passwords do not match.</p>}
    {mutation.error && <p role="alert" className={styles.error}>{uncertain
      ? "We couldn’t confirm the reset. Before trying again, try logging in with your new password. If it works, generate a new recovery code in your account settings."
      : mutation.error instanceof ApiError && mutation.error.status === 429 ? "Too many attempts. Please wait before trying again."
        : "Unable to reset your password. Check your handle, saved code and password requirements. If you have tried several times, wait 15 minutes before trying again."}</p>}
    <fieldset disabled={mutation.isPending || session.isTransitioning}>
      <TextField name="handle" label="Handle" autoComplete="username" required minLength={3} maxLength={30} pattern="[A-Za-z0-9_]{3,30}" />
      <TextField name="recoveryCode" label="Recovery code" autoComplete="off" required maxLength={64} spellCheck={false} autoCapitalize="characters" />
      <TextField name="password" label="New password" type="password" autoComplete="new-password" required minLength={12} maxLength={128} errors={fieldErrors(mutation.error, "password")} hint="12–128 characters, including uppercase, lowercase and a number; use at least four different characters." />
      <TextField name="confirmPassword" label="Confirm new password" type="password" autoComplete="new-password" required minLength={12} maxLength={128} />
      <Button type="submit">{mutation.isPending ? "Resetting password…" : "Reset password"}</Button>
    </fieldset>
    <p>If you no longer have a code, use a previously linked sign-in provider. Admins cannot recover access for you.</p>
    <p className={styles.footer}><Link href="/login">Back to login</Link></p>
  </form></AuthEntry>;
}
