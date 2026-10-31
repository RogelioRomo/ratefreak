"use client";

import Link from "next/link";
import type { FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import type { RegisterRequest } from "../interfaces/register-request";
import { useRegister } from "../hooks/use-register";
import { useSession } from "../hooks/use-session";
import { FormError, fieldErrors } from "./form-error";
import styles from "./auth.module.css";

export function RegisterForm() {
  const mutation = useRegister();
  const session = useSession();
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const input: RegisterRequest = {
      displayName: String(data.get("displayName") ?? ""), handle: String(data.get("handle") ?? ""),
      email: String(data.get("email") ?? ""), password: String(data.get("password") ?? ""),
    };
    (form.elements.namedItem("password") as HTMLInputElement).value = "";
    void mutation.submit(input).catch(() => {});
  }
  if (mutation.isSuccess) return <div className={styles.success}><h2>Your account is ready.</h2><p>Log in to start making RateFreak yours.</p><Link href="/login">Log in</Link></div>;
  return (
    <form aria-label="Create account" onSubmit={submit} className={styles.form}>
      <FormError error={mutation.error} />
      <fieldset disabled={mutation.isPending || session.isTransitioning}>
        <TextField name="displayName" label="Display name" autoComplete="nickname" required maxLength={80} errors={fieldErrors(mutation.error, "displayName")} />
        <TextField name="handle" label="Handle" autoComplete="username" required minLength={3} maxLength={30} pattern="[A-Za-z0-9_]{3,30}" hint="3–30 letters, numbers or underscores." errors={fieldErrors(mutation.error, "handle")} />
        <TextField name="email" label="Email" type="email" autoComplete="email" required maxLength={254} errors={fieldErrors(mutation.error, "email")} />
        <TextField name="password" label="Password" type="password" autoComplete="new-password" required minLength={12} maxLength={128} hint="12–128 characters, including uppercase, lowercase and a number; use at least four different characters." errors={fieldErrors(mutation.error, "password")} />
        <Button type="submit">{mutation.isPending ? "Creating account…" : "Create account"}</Button>
      </fieldset>
      <p className={styles.footer}>Already have an account? <Link href="/login">Log in</Link>.</p>
    </form>
  );
}
