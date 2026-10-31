"use client";

import { ErrorState } from "@/components/ui/error-state";
import { LoadingState } from "@/components/ui/loading-state";
import { useSession } from "../hooks/use-session";
import styles from "./auth.module.css";

export function AccountDetails() {
  const session = useSession();
  if (session.error) return <ErrorState title="We couldn’t load your account" description="Please try again." onRetry={() => { void session.refresh(); }} />;
  if (!session.account) return <LoadingState label="Checking your session…" />;
  const account = session.account;
  return <div className={styles.details}>
    <dl><dt>Display name</dt><dd>{account.displayName}</dd><dt>Handle</dt><dd>@{account.handle}</dd><dt>Email</dt><dd>{account.email}</dd></dl>
    <p>This information is private to your account. Logging out signs you out on all devices.</p>
  </div>;
}
