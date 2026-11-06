"use client";

import { useState, type FormEvent } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { ApiError } from "@/shared/api/api-error";
import { useSession } from "../hooks/use-session";
import { useSensitiveMutation } from "../hooks/use-sensitive-mutation";
import type { AccountProfile } from "../interfaces/account-profile";
import type { GenerateRecoveryCodeRequest } from "../interfaces/generate-recovery-code-request";
import type { RecoveryEditorProps } from "../interfaces/recovery-editor-props";
import { sessionQueryKey } from "../session/query-keys";
import { generateRecoveryCode, getRecoveryStatus } from "../api/recovery";
import { RecoveryCodeDisplay } from "./recovery-code-display";
import styles from "./auth.module.css";
import recoveryStyles from "./recovery.module.css";

export function RecoverySettings() {
  const session = useSession(); const client = useQueryClient();
  const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  return cached ? <RecoveryEditor key={cached.id} accountId={cached.id} available={!!session.account && !session.error && !session.isTransitioning} /> : null;
}
function RecoveryEditor({ accountId, available }: RecoveryEditorProps) {
  const session = useSession();
  const [code, setCode] = useState<string | null>(null);
  const [issued, setIssued] = useState(false);
  const status = useQuery({ queryKey: ["private", accountId, "recovery-status"], queryFn: ({ signal }) => getRecoveryStatus(signal), enabled: available, retry: false });
  const mutation = useSensitiveMutation<GenerateRecoveryCodeRequest>(input => session.runExclusive(async () => {
    const result = await generateRecoveryCode(input); setCode(result.recoveryCode); setIssued(true);
  }));
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!available || mutation.isPending || code) return;
    const form = event.currentTarget; const data = new FormData(form);
    (form.elements.namedItem("password") as HTMLInputElement).value = "";
    void mutation.submit({ password: String(data.get("password") ?? "") }).catch(() => {});
  }
  return <section hidden={!available} inert={!available} className={recoveryStyles.settings} aria-label="Password recovery">
    {code ? <RecoveryCodeDisplay code={code} title="Save your recovery code." doneLabel="Done" onDone={() => { setCode(null); mutation.reset(); }} /> : <>
      <h2>Password recovery</h2>
      {status.isPending ? <p>Checking recovery settings…</p> : status.isError ? <><p role="alert">We couldn’t load recovery settings.</p><Button variant="secondary" onClick={() => { void status.refetch(); }}>Retry recovery settings</Button></> : !status.data.hasPassword
        ? <p>Recover access through your linked sign-in provider. This account has no local password.</p>
        : <form aria-label="Recovery code settings" className={styles.form} onSubmit={submit}>
          <p>{status.data.hasRecoveryCode || issued ? "Replace your code if you lost it or think someone else has it. Your previous code will stop working immediately." : "You haven’t saved a recovery code yet. Generate one so you can reset a forgotten password without email."}</p>
          {mutation.error && <p role="alert" className={styles.error}>{mutation.error instanceof ApiError && mutation.error.status === 400
            ? "Check your current password. After several failed attempts, wait 15 minutes before trying again."
            : mutation.error instanceof ApiError && mutation.error.status === 429 ? "Too many attempts. Please wait before trying again."
              : "We couldn’t confirm the new code. Your previous code may no longer work. Confirm your session and generate another code to save."}</p>}
          <fieldset disabled={!available || mutation.isPending}>
            <TextField name="password" label="Current password" type="password" autoComplete="current-password" required maxLength={128} />
            <Button type="submit">{mutation.isPending ? "Generating code…" : status.data.hasRecoveryCode || issued ? "Replace recovery code" : "Generate recovery code"}</Button>
          </fieldset>
        </form>}
    </>}
  </section>;
}
