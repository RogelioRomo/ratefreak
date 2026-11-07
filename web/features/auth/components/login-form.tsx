"use client";

import type { LoginContinuationProps } from "../interfaces/login-continuation";
import { safePreviewReturnTo } from "../query/return-to";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { FormEvent } from "react";
import type { LoginRequest } from "../interfaces/login-request";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { useLogin } from "../hooks/use-login";
import { useSession } from "../hooks/use-session";
import { FormError, fieldErrors } from "./form-error";
import { ProviderOptions } from "./provider-options";
import styles from "./auth.module.css";

export function LoginForm({ returnTo }: LoginContinuationProps = {}) {
  const mutation = useLogin();
  const session = useSession();
  const router = useRouter();
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const input: LoginRequest = { email: String(data.get("email") ?? ""), password: String(data.get("password") ?? "") };
    (form.elements.namedItem("password") as HTMLInputElement).value = "";
    void mutation.submit(input).then(() => router.replace(safePreviewReturnTo(returnTo ?? null) ?? "/account")).catch(() => {});
  }
  return (
    <form aria-label="Log in" onSubmit={submit} className={styles.form}>
      <FormError error={mutation.error} />
      <fieldset disabled={mutation.isPending || session.isTransitioning}>
        <TextField name="email" label="Email" type="email" autoComplete="email" required maxLength={254} errors={fieldErrors(mutation.error, "email")} />
        <TextField name="password" label="Password" type="password" autoComplete="current-password" required maxLength={128} errors={fieldErrors(mutation.error, "password")} />
        <Button type="submit">{mutation.isPending ? "Logging in…" : "Log in"}</Button>
      </fieldset>
      <ProviderOptions />
      <p><Link href="/reset-password">Forgot your password?</Link></p>
      <p className={styles.footer}>New here? <Link href="/register">Create an account</Link>.</p>
    </form>
  );
}
