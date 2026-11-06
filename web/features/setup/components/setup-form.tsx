"use client";

import { type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { ApiError } from "@/shared/api/api-error";
import { useSensitiveMutation } from "@/features/auth/hooks/use-sensitive-mutation";
import { fieldErrors } from "@/features/auth/components/form-error";
import type { SetupRequest } from "../interfaces/setup-request";
import type { SetupFormProps } from "../interfaces/setup-form-props";
import { completeInstallation } from "../api/installation";
import styles from "@/features/auth/components/auth.module.css";

export function SetupForm({ onCreated, onCheckStatus }: SetupFormProps) {
  const mutation = useSensitiveMutation<SetupRequest>(async input => {
    try { const account = await completeInstallation(input); onCreated(account.recoveryCode); }
    catch (error) { if (error instanceof ApiError && error.status === 409) onCheckStatus(); throw error; }
  });
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (mutation.isPending) return;
    const form = event.currentTarget; const data = new FormData(form);
    const input = { setupSecret: String(data.get("setupSecret") ?? ""), displayName: String(data.get("displayName") ?? ""), handle: String(data.get("handle") ?? ""), email: String(data.get("email") ?? ""), password: String(data.get("password") ?? "") };
    for (const name of ["setupSecret","password"]) (form.elements.namedItem(name) as HTMLInputElement).value = "";
    void mutation.submit(input).catch(() => {});
  }
  const uncertain = mutation.error && (!(mutation.error instanceof ApiError) || mutation.error.status >= 500);
  return <form aria-label="Create initial administrator" className={styles.form} onSubmit={submit}>
    <p>This account will administer this installation. Later accounts will be ordinary users. Save your recovery code after setup; we don’t send password reset emails.</p>
    {mutation.error && <div className={styles.error} role="alert"><p>{uncertain
      ? "We couldn’t confirm setup. Check the installation status before trying again. If setup completed, log in with these account details and generate a recovery code in your account settings."
      : mutation.error instanceof ApiError && mutation.error.status === 429 ? "Too many setup attempts. Please wait 15 minutes before trying again."
        : mutation.error instanceof ApiError && mutation.error.status === 409 ? "Setup is already complete. Checking the installation…"
          : "Unable to complete setup. Check your deployment secret and account details."}</p></div>}
    {uncertain && <Button variant="secondary" onClick={onCheckStatus}>Check installation status</Button>}
    <fieldset disabled={mutation.isPending}>
      <TextField name="setupSecret" label="Deployment secret" type="password" autoComplete="off" spellCheck={false} required minLength={64} maxLength={64} hint="Use the secret configured by the person deploying this installation." />
      <TextField name="displayName" label="Display name" autoComplete="nickname" required maxLength={80} errors={fieldErrors(mutation.error,"displayName")} />
      <TextField name="handle" label="Handle" autoComplete="username" required minLength={3} maxLength={30} pattern="[A-Za-z0-9_]{3,30}" errors={fieldErrors(mutation.error,"handle")} />
      <TextField name="email" label="Email" type="email" autoComplete="email" required maxLength={254} errors={fieldErrors(mutation.error,"email")} />
      <TextField name="password" label="Password" type="password" autoComplete="new-password" required minLength={12} maxLength={128} hint="12–128 characters, including uppercase, lowercase and a number; use at least four different characters." errors={fieldErrors(mutation.error,"password")} />
      <Button type="submit">{mutation.isPending ? "Creating administrator…" : "Create administrator"}</Button>
    </fieldset>
  </form>;
}
