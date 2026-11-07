"use client";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-error";
import { TextField } from "@/components/ui/text-field";
import { Button } from "@/components/ui/button";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { getPendingExternal, registerExternal } from "../api/external";
import { useSensitiveMutation } from "../hooks/use-sensitive-mutation";
import { useSession } from "../hooks/use-session";
import type { ExternalRegistrationRequest } from "../interfaces/external-registration-request";
import { AuthPanel } from "./auth-panel";
import { AuthEntry } from "./auth-entry";
import { FormError, fieldErrors } from "./form-error";
import styles from "./auth.module.css";
export function ExternalRegistrationScreen() {
  const session = useSession(); const router = useRouter(); const [expired, setExpired] = useState(false);
  const pending = useQuery({ queryKey: ["external", "pending"], queryFn: ({ signal }) => getPendingExternal(signal), staleTime: 0, retry: false, enabled: session.account === null });
  const mutation = useSensitiveMutation<ExternalRegistrationRequest>(async input => {
    try { await session.changeIdentity(() => registerExternal(input)); router.replace("/"); }
    catch (error) { if (error instanceof ApiError && error.status === 401) setExpired(true); throw error; }
  });
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    void mutation.submit({ handle: String(data.get("handle") ?? ""), displayName: String(data.get("displayName") ?? "") }).catch(() => {});
  }
  const ended = expired || (!pending.isPending && !pending.error && !pending.data);
  return <AuthPanel title="Make yourself at home." description="Choose how you’ll appear on RateFreak."><AuthEntry>
    {ended ? <div className={styles.success}><p>Your sign-in has expired. Start again to create your account.</p><Link href="/login">Back to login</Link></div>
      : pending.isPending ? <LoadingState label="Checking your provider sign-in…" />
      : pending.error ? <ErrorState title="We couldn’t check your sign-in" description="Please try again." onRetry={() => { void pending.refetch(); }} />
      : <form aria-label="Finish creating account" className={styles.form} onSubmit={submit}>
        <p>Continue with {pending.data?.provider === "google" ? "Google" : "GitHub"}. No separate password is needed.</p>
        <FormError error={mutation.error} />
        {mutation.error instanceof ApiError && mutation.error.status === 409 && <p>If you already have an account, log in to it and link this provider from account settings.</p>}
        <fieldset disabled={mutation.isPending || session.isTransitioning}>
          <TextField name="displayName" label="Display name" autoComplete="nickname" required maxLength={80} errors={fieldErrors(mutation.error, "displayName")} />
          <TextField name="handle" label="Handle" autoComplete="username" required minLength={3} maxLength={30} pattern="[A-Za-z0-9_]{3,30}" hint="3–30 letters, numbers or underscores." errors={fieldErrors(mutation.error, "handle")} />
          <Button type="submit">{mutation.isPending ? "Creating account…" : "Create account"}</Button>
        </fieldset>
        <p className={styles.footer}>You’ll sign in and recover access through your provider. <Link href="/login">Back to login</Link>.</p>
      </form>}
  </AuthEntry></AuthPanel>;
}
